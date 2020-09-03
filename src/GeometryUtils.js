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

  static buildBuffersFromCubeTriMap(cubeIdToTriMap, smoothingAngle=40*Math.PI/180, tolerance=1e-4) {
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

    const faces = [];
    const vertexMap = {};
    for (const cubeIdToTris of Object.entries(cubeIdToTriMap)) {
      const [cubeId, triangles] = cubeIdToTris;
      for (const triangle of triangles) {
        const {a,b,c} = triangle
        const normal = triangle.getNormal(tempVec3).clone();

        // If the normal is zero then we get rid of the triangle
        if (normal.lengthSq() < tolerance*tolerance) {
          assert(false);
          continue;
        }

        const face = [0,0,0,cubeId];
        faces.push(face);
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

    const indices = new Array(faces.length*3);
    //const cubeIdMap = {}
    let indexCount = 0;
    faces.forEach((face/*,faceIdx*/) => {
      indices[indexCount++] = face[0];
      indices[indexCount++] = face[1];
      indices[indexCount++] = face[2];
      //if (!cubeIdMap[face[3]]) { cubeIdMap[face[3]] = []; }
      //cubeIdMap[face[3]].push(faceIdx);
    });

    return { vertices, normals: normalVecs, indices };
  }

  static buildBufferGeometryFromCubeTriMap(cubeIdToTriMap, maxVertices=-1) {
    const { vertices, normals, indices } = 
      GeometryUtils.buildBuffersFromCubeTriMap(cubeIdToTriMap);
    const useArraySizes = maxVertices === - 1;

    const threeGeometry = new THREE.BufferGeometry();
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

    threeGeometry.setAttribute('position', posAttr);
    threeGeometry.setAttribute('normal', normalAttr);
    threeGeometry.setIndex(indexAttr);
    threeGeometry.setDrawRange(0,indices.length);

    //threeGeometry.cubeIdMap = cubeIdMap;
    return threeGeometry;
  }

  static updateBufferGeometryFromCubeTriMap(bufferGeometry, cubeIdToTriMap, maxVertices) {
    const { vertices, normals, indices } =
      GeometryUtils.buildBuffersFromCubeTriMap(cubeIdToTriMap);
    assert(vertices.length / 3 <= maxVertices, "Maximum vertices exceeded. This shouldn't happen, check your math!");
    
    const positionAttr = bufferGeometry.getAttribute('position'),
      normalAttr = bufferGeometry.getAttribute('normal'),
      indexAttr = bufferGeometry.index;

    positionAttr.array.set(vertices,0);
    positionAttr.count = vertices.length/3;
    positionAttr.needsUpdate = true;

    normalAttr.array.set(normals, 0);
    normalAttr.count = normals.length/3;
    normalAttr.needsUpdate = true;

    indexAttr.array.set(indices, 0);
    indexAttr.count = indices.length;
    indexAttr.needsUpdate = true;
    
    bufferGeometry.setDrawRange(0, indices.length);

    return indices.length;
  }

}

export default GeometryUtils;