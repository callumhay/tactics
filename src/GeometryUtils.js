import * as THREE from 'three';
import { assert } from 'chai';

import MathUtils from './MathUtils';

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

  static buildBufferGeometryFromTris(triangles, smoothingAngle=40*Math.PI/180, tolerance=1e-4) {
    tolerance = Math.max(tolerance, Number.EPSILON);
    const decimalShift = Math.log10(1/tolerance);
    const shiftMultiplier = Math.pow(10, decimalShift);
    const makeValueHash = (value) => {
      return `${~ ~(value * shiftMultiplier)}`; // ~ ~ truncates the value
    }
    const makeVertexHash = (vertex) => {
      const {x,y,z} = vertex;
      return `${makeValueHash(x)},${makeValueHash(y)},${makeValueHash(z)}`;
    }

    const tempVec3 = new THREE.Vector3();

    const faces = [];
    const vertexMap = {};
    for (const triangle of triangles) {
      const {a,b,c} = triangle
      const normal = triangle.getNormal(tempVec3).clone();
      const face = [0,0,0];
      faces.push(face);
      [a,b,c].forEach((vertex,idx) => {
        const hash = makeVertexHash(vertex);
        let vertexLookup = vertexMap[hash];
        if (!vertexLookup) {
          vertexLookup = vertexMap[hash] = {vertex: vertex, normals:[], faces:[]};
        }
        vertexLookup.normals.push(normal);
        vertexLookup.faces.push([face,idx])
      });
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
    let indexCount = 0;
    faces.forEach(face => {
      indices[indexCount++] = face[0];
      indices[indexCount++] = face[1];
      indices[indexCount++] = face[2];
    });
  
    let threeGeometry = new THREE.BufferGeometry();
    threeGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(vertices), 3));
    threeGeometry.setAttribute('normal', new THREE.BufferAttribute(new Float32Array(normalVecs), 3, true));
    threeGeometry.setIndex(indices);
    return threeGeometry;
    //const simplifier = new SimplifyModifier();
    //return simplifier.modify(threeGeometry, Math.floor(threeGeometry.getAttribute('position').count*0.25));
  }



}

export default GeometryUtils;