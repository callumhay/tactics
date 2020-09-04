import * as THREE from 'three';

import MathUtils from './MathUtils';
import { assert } from 'chai';

const tempVec3 = new THREE.Vector3();
const tempVec3a = new THREE.Vector3();
const tempVec3b = new THREE.Vector3();

class GeometryUtils {
  static roundVertices(geometry, decimals=4) {
    // Go through all the positions in the geometry and round them as specified
    const positions = geometry.getAttribute('position');
    for (let i = 0; i < positions.count; i++) {
      positions.setX(i, MathUtils.roundToDecimal(positions.getX(i), decimals));
      positions.setY(i, MathUtils.roundToDecimal(positions.getY(i), decimals));
      positions.setZ(i, MathUtils.roundToDecimal(positions.getZ(i), decimals));
    }
  }

  static intersectPlanes(plane1, plane2) {
    const dir = tempVec3.crossVectors(plane1.normal, plane2.normal).clone();
    const denom = dir.dot(dir);
    if (denom < 1e-6) { return null; }

    tempVec3a.copy(plane1.normal);
    tempVec3a.multiplyScalar(plane2.constant);
    tempVec3b.copy(plane2.normal);
    tempVec3b.multiplyScalar(plane1.constant);
    tempVec3b.sub(tempVec3a);

    const point = tempVec3.crossVectors(tempVec3b, dir).divideScalar(denom).clone();
    return new THREE.Line3(point, dir.add(point));
  }

  static buildBuffersFromCubeTriangleAndMaterialMap(cubeIdToTriMatObjMap, smoothingAngle=40*Math.PI/180, tolerance=1e-4) {
    tolerance = Math.max(tolerance, Number.EPSILON);
    const decimalShift = Math.log10(1/tolerance);
    const shiftMultiplier = Math.pow(10, decimalShift);
    const makeValueHash = (value) => {
      return `${~ ~(value * shiftMultiplier)}`; // ~ ~ truncates the value
    };
    const makeVertexHash = (vertex) => {
      const {x,y,z} = vertex;
      return `${makeValueHash(x)},${makeValueHash(y)},${makeValueHash(z)}`;
    };

    let numFaces = 0;
    const faces = []; // indexed by material index
    const vertexMap = {};
    const materials = [];
    for (const triMatObjs of Object.values(cubeIdToTriMatObjMap)) {
      for (const triMatObj of triMatObjs) {

        const {triangle, material} = triMatObj;
        assert(material !== null);
        let materialIdx = materials.indexOf(material);
        if (materialIdx === -1) {
          materialIdx = materials.length;
          materials.push(material);
          faces.push([]);
        }

        const {a,b,c} = triangle
        const normal = triangle.getNormal(new THREE.Vector3());

        // If the normal is zero then we get rid of the triangle
        if (normal.lengthSq() < tolerance*tolerance) {
          assert(false);
          continue;
        }

        const face = [0,0,0];
        faces[materialIdx].push(face);
        numFaces++;

        [a,b,c].forEach((vertex,idx) => {
          const hash = makeVertexHash(vertex);
          let vertexLookup = vertexMap[hash];
          if (!vertexLookup) {
            vertexLookup = vertexMap[hash] = { vertex, normals: [], faces: []};
          }
          vertexLookup.normals.push(normal);
          vertexLookup.faces.push([face,idx]);
        });
      }
    }

    const vertices = [];
    const normalVecs = [];

    // Go over the vertex map and clean up any duplicate vertices based on normal angle
    for (const vertexObj of Object.values(vertexMap)) {
      const {vertex, normals, faces} = vertexObj;
      const ungroupedNormals = normals.map(() => true);
      const groupedNormals = [];
      
      for (let i = 0; i < normals.length; i++) {
        if (ungroupedNormals[i]) {
          const currGroup = [i];
          ungroupedNormals[i] = false;
          
          for (let j = i; j < normals.length; j++) {
            if (ungroupedNormals[j]) {
              const iNorm = normals[i];
              const jNorm = normals[j];
              if (iNorm.angleTo(jNorm) <= smoothingAngle) {
                currGroup.push(j);
                ungroupedNormals[j] = false;
              }
            }
          }
          
          groupedNormals.push(currGroup);
        }
      }

      // Go through each of the grouped normals, average each group and insert them as duplicates of the vertices
      for (let i = 0; i < groupedNormals.length; i++) {
        const currNormalIndices = groupedNormals[i];

        const avgNormal = new THREE.Vector3();
        for (let j = 0; j < currNormalIndices.length; j++) {
          const currNormal = normals[currNormalIndices[j]];
          avgNormal.add(currNormal);
        }
        avgNormal.normalize();

        for (let j = 0; j < currNormalIndices.length; j++) {
          const currIndex = currNormalIndices[j];
          const [face, idx] = faces[currIndex];

          face[idx] = Math.floor(vertices.length/3);

          ['x', 'y', 'z'].forEach((axis) => {
            vertices.push(vertex[axis]);
            normalVecs.push(avgNormal[axis]);
          });
        }
      }
    }

    const indices = new Array(numFaces*3);
    let indexCount = 0;
    const groups = [];
    for (let i = 0; i < faces.length; i++) {
      const materialFaces = faces[i];
      const startIndex = indexCount;
      for (const face of materialFaces) {
        indices[indexCount++] = face[0];
        indices[indexCount++] = face[1];
        indices[indexCount++] = face[2];
      }
      groups.push({start: startIndex, count: materialFaces.length*3, materialIndex:i});
    }

    return { vertices, normals: normalVecs, indices, groups, materials };
  }

  static buildBufferGeometryFromCubeTriMap(cubeIdToTriMatObjMap, maxVertices=-1) {
    const { vertices, normals, indices, groups, materials } = 
      GeometryUtils.buildBuffersFromCubeTriangleAndMaterialMap(cubeIdToTriMatObjMap);
    const useArraySizes = maxVertices === - 1;

    const geometry = new THREE.BufferGeometry();
    const fullVertices = new Float32Array(useArraySizes ? vertices.length : maxVertices * 3);
    fullVertices.set(vertices, 0);
    const fullNormals = new Float32Array(useArraySizes ? normals.length : maxVertices * 3);
    fullNormals.set(normals, 0);
    const fullIndices = new Uint32Array(useArraySizes ? indices.length : maxVertices * 3);
    fullIndices.set(indices, 0);

    const posAttr = new THREE.BufferAttribute(fullVertices, 3);
    posAttr.count = vertices.length/3;
    const normalAttr = new THREE.BufferAttribute(fullNormals, 3, true);
    normalAttr.count = normals.length/3;
    const indexAttr = new THREE.Uint32BufferAttribute(fullIndices, 1);
    indexAttr.count = indices.length;

    geometry.setAttribute('position', posAttr);
    geometry.setAttribute('normal', normalAttr);
    geometry.setIndex(indexAttr);
    geometry.setDrawRange(0,indices.length);
    
    for (const group of groups) {
      const {start, count, materialIndex} = group;
      geometry.addGroup(start, count, materialIndex);
    }
    
    return {geometry, materials};
  }

  static updateBufferGeometryFromCubeTriMap(geometry, cubeIdToTriMatObjMap, maxVertices) {
    const { vertices, normals, indices, groups, materials} =
      GeometryUtils.buildBuffersFromCubeTriangleAndMaterialMap(cubeIdToTriMatObjMap);
    assert(vertices.length / 3 <= maxVertices, "Maximum vertices exceeded. This shouldn't happen, check your math!");
    
    const positionAttr = geometry.getAttribute('position'),
      normalAttr = geometry.getAttribute('normal'),
      indexAttr = geometry.index;

    positionAttr.array.set(vertices,0);
    positionAttr.count = vertices.length/3;
    positionAttr.needsUpdate = true;

    normalAttr.array.set(normals, 0);
    normalAttr.count = normals.length/3;
    normalAttr.needsUpdate = true;

    indexAttr.array.set(indices, 0);
    indexAttr.count = indices.length;
    indexAttr.needsUpdate = true;
    
    geometry.setDrawRange(0, indices.length);

    geometry.clearGroups();
    for (const group of groups) {
      const {start, count, materialIndex} = group;
      geometry.addGroup(start, count, materialIndex);
    }

    return {geometry, materials};
  }

}

export default GeometryUtils;