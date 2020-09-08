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

  static findGeometryIslands(geometry, tolerance=1e-6) {
    const {index} = geometry;
    assert(index.count % 3 === 0, "Geometry must be composed of triangles!");
    const positions = geometry.getAttribute('position');
    
    tolerance = Math.max(tolerance, Number.EPSILON);
    const decimalShift = Math.log10(1 / tolerance);
    const shiftMultiplier = Math.pow(10, decimalShift);
    const makeValueHash = (value) => {
      return `${~ ~(value * shiftMultiplier)}`;
    };
    const makeVertexHash = (x, y, z) => {
      return `${makeValueHash(x)},${makeValueHash(y)},${makeValueHash(z)}`
    }

    const UNVISTED_STATE = 0;
    const VISTING_STATE  = 1;
    const FINISHED_STATE = 2;

    const numTris = index.count/3;
    const triangles = new Array(numTris).fill(null);
    const indexToTris = {};
    let triCount = 0;
    for (let i = 0; i < index.count; i+=3) {
      const idx0 = index.getX(i), idx1 = index.getX(i + 1), idx2 = index.getX(i + 2);
      const currTT = triangles[triCount] = { 
        triIndex: triCount,
        indices: [idx0, idx1, idx2],
      };

      for (const idx of currTT.indices) {
        if (!indexToTris[idx]) {
          indexToTris[idx] = [];
        }
        indexToTris[idx].push(currTT);
      }

      triCount++;
    }

    // Create a mapping of all unique vertices to their respective indices
    const vertexToIndices = {};
    const traversalInfo = {};
    for (let i = 0; i < index.count; i++) {
      const idx = index.getX(i);
      traversalInfo[idx] = {
        idx,
        state: UNVISTED_STATE,
        islandIdx: -1,
      };
      const hash = makeVertexHash(positions.getX(idx), positions.getY(idx), positions.getZ(idx));
      if (!vertexToIndices[hash]) {
        vertexToIndices[hash] = [];
      }
      vertexToIndices[hash].push(idx);
    }

    for (let i = 0; i < index.count; i++) {
      const posX = positions.getX(i);
      const posY = positions.getY(i);
      const posZ = positions.getZ(i);
      const hash = makeVertexHash(posX, posY, posZ);
      traversalInfo[hash] = { 
        posIdx: i, 
        state: UNVISTED_STATE,
        islandIdx: -1,
        vertexHash: hash,
      };
    }

    const depthFirstSearch = (islandIdx, tInfo, islandSet) => {
      tInfo.state = VISTING_STATE;

      const { idx } = tInfo;
      const posX = positions.getX(idx);
      const posY = positions.getY(idx);
      const posZ = positions.getZ(idx);
      const hash = makeVertexHash(posX, posY, posZ);

      // Find all the indices that have approx the same vertex position 
      // as the current index being traversed
      const indicesWithSameVertex = vertexToIndices[hash];
      for (const index of indicesWithSameVertex) {
        // For each index we find the triangles that index is a part of
        const tris = indexToTris[index];
        for (const tri of tris) {
          // We then traverse all the indices that make up each triangle
          for (const triVertexIdx of tri.indices) {
            const nextTInfo = traversalInfo[triVertexIdx];
            if (nextTInfo.state === UNVISTED_STATE) {
              depthFirstSearch(islandIdx, nextTInfo, islandSet);
            }
          }
        }
      }

      tInfo.islandIdx = islandIdx;
      islandSet.add(tInfo);
      tInfo.state = FINISHED_STATE;
    };

    const islands = [];
    for (let i = 0; i < index.count; i++) {
      const tInfo = traversalInfo[index.getX(i)];
      if (tInfo.state === UNVISTED_STATE) {
        const islandSet = new Set();
        depthFirstSearch(islands.length, tInfo, islandSet);
        islands.push(islandSet);
      }
    }

    return islands;
  }

  static clampGeometryToBoundingBox(geometry, boundingBox) {
    const {clamp} = THREE.MathUtils;
    const {min, max} = boundingBox;
    const { index } = geometry;
    const positions = geometry.getAttribute('position');
    for (let i = 0; i < index.count; i++) {
      const idx = index.getX(i);
      positions.setX(idx, clamp(positions.getX(idx), min.x, max.x));
      positions.setY(idx, clamp(positions.getY(idx), min.y, max.y));
      positions.setZ(idx, clamp(positions.getZ(idx), min.z, max.z));
    }
    geometry.computeVertexNormals();
  }


  // TODO...
  cleanUpStrayFaces(mesh) {
    const raycaster = new THREE.Raycaster();
    const {geometry} = mesh;

    // Go through each face and check to see if the -normal of the face hits the back of another face
    // only keep faces that pass that check



  }


}

export default GeometryUtils;