import * as THREE from 'three';
import * as CANNON from 'cannon';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice'
import GameTypes from './GameTypes';

export default class Battlefield {
  constructor(scene, physics) {
    this._scene = scene;
    this.physics = physics;

    this.debris = [];

    this.sunLight = new THREE.DirectionalLight(0xffffff, 0.5);
    //this.sunLight.castShadow = true;
    this.sunLight.position.set(0, 1, -1);
    this.sunLight.position.normalize();
    this._scene.add(this.sunLight);

    this.fillLight = new THREE.PointLight(0xffffff, 0.5);
    this.fillLight.position.set(0,10,10);
    this._scene.add(this.fillLight);

    this.terrainGroup = new THREE.Group();
    this._scene.add(this.terrainGroup);
  }

  setTerrain(terrain) {
    this._clearTerrain();
    this._terrain = terrain;
    const terrainSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(-terrainSize / 2, 0, -terrainSize / 2);
    this._preloadCleanupTerrain();
    this._buildRigidbodyLattice();
  }

  _buildRigidbodyLattice() {
    if (this.rigidBodyLattice) { this.rigidBodyLattice.clear(); }
    this.rigidBodyLattice = new RigidBodyLattice(this.terrainGroup);
    this.rigidBodyLattice.buildFromTerrain(this._terrain);
  }

  clear() {
    this._clearTerrain();
  }
  _clearTerrain() {
    if (!this._terrain) { return; }
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].clear();
      }
    }
    this._terrain = [];
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    if (!subtractGeometry.boundingBox) subtractGeometry.computeBoundingBox();
    const {boundingBox} = subtractGeometry;
    const {clamp} = THREE.MathUtils;

    // Get all the terrain columns that might be modified
    const minX = clamp(Math.floor(boundingBox.min.x), 0, this._terrain.length-1);
    const maxX = clamp(Math.floor(boundingBox.max.x), 0, this._terrain.length-1);

    const minZ = Math.max(0, Math.floor(boundingBox.min.z));
    const floorMaxZ = Math.floor(boundingBox.max.z);
    
    const terrainCols = [];
    for (let x = minX; x <= maxX; x++) {
      const terrainZ = this._terrain[x];
      const maxZ = Math.min(terrainZ.length - 1, floorMaxZ);
      if (maxZ >= 0 && minZ < terrainZ.length) {
        for (let z = minZ; z <= maxZ; z++) {
          terrainCols.push(terrainZ[z]);
        }
      }
    }

    terrainCols.forEach(terrainCol => {
      terrainCol.blowupTerrain(subtractGeometry);
    });
  }

  convertTerrainToDebris(debrisMesh, config) {
    const debrisObj = this.physics.addObject(debrisMesh, config);
    this.debris.push(debrisObj);
    return debrisObj;
  }
  convertDebrisToTerrain(debrisObj) {
    if (debrisObj.gameType !== GameTypes.DETACHED_TERRAIN) {
      console.error("Attempting to convert invalid game type back into terrain, ignoring."); return;
    }

    this.debris.splice(this.debris.indexOf(debrisObj), 1);

    // We need to reattach the landing range associated with this piece of detached terrain
    const {gameObject:landingRange, mesh, body} = debrisObj;
    const {position, rotation} = mesh;

    const ANGLE_SNAP_EPSILON = 20 * Math.PI / 180;
    const PI_OVER_2 = Math.PI / 2;

    // Start with the simplest case: a box
    if (body.shapes.length === 1 && body.shapes[0].type === CANNON.Shape.types.BOX) {

      // The landing range may have shifted its position, figure out what terrain column it's closest to in the grid...
      const closestXIdx = Math.floor(position.x/TerrainColumn.SIZE);
      const closestZIdx = Math.floor(position.z/TerrainColumn.SIZE);
      const closestTerrainCol = this._terrain[closestXIdx][closestZIdx];
      if (!closestTerrainCol) {
        // There is no terrain column where this will work...
        console.error("Failed to find terrain column for placement, removing terrain.");
        landingRange.clear();
        return;
      }

      // Snap the position to the nearest terrain column
      const closestCenter = closestTerrainCol.getTerrainSpaceTranslation();
      position.x = closestCenter.x;
      position.z = closestCenter.z;
      position.y = Math.round((position.y + Number.EPSILON) * 100) / 100; // Make sure the y-coordinate snaps within a precision of 1mm of the environment 

      // We will need to clean up the position and orientation so that the box lies cleanly in the terrain column
      for (const coord of ['x','y','z']) {
        // Snap the rotation to the nearest 90 degree angle
        const truncDiv = Math.trunc(rotation[coord]/PI_OVER_2);
        const snapped  = truncDiv*PI_OVER_2;
        if (Math.abs(rotation[coord] - snapped) <= ANGLE_SNAP_EPSILON) {
          rotation[coord] = snapped;
        }
      }
      mesh.setRotationFromEuler(rotation);
      mesh.updateMatrixWorld();

      closestTerrainCol.attachLandingRange(landingRange);
    }
    else {
      // TODO: Deal with cases where the mesh is too big for just one terrain column, split it up, etc.
      console.warn("Need to write code to deal with this type of debris-to-terrain situation.");
    }

  }

  // Do a basic terrain check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  _preloadCleanupTerrain() {
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {

        const terrainCol = this._terrain[x][z];
        const {landingRanges} = terrainCol;
        if (landingRanges.length === 0) {
          continue;
        }

        const neighbours = [];
        if (x > 0) { neighbours.push(this._terrain[x-1][z]); }
        if (x < this._terrain.length-1) { neighbours.push(this._terrain[x+1][z]); }
        if (z > 0) { neighbours.push(this._terrain[x][z-1]); }
        if (z < this._terrain[x].length-1) { neighbours.push(this._terrain[x][z+1]); }

        // For each of the non-grounded terrainCol ranges, count the overlaps of neighbouring ranges
        // This is a 2D array where each element is 2 values: [rangeIdx_in_terrainCol, count]
        const rangeCounts = [];
        let prevRangeGrounded = false;
        for (let i = 0; i < landingRanges.length; i++) {
          const {startY, endY} = landingRanges[i];
          if (startY === 0) {
            prevRangeGrounded = true;
          }
          else {
            rangeCounts.push([i, 0]);
          }
        }

        neighbours.forEach(neighbour => {
          if (neighbour) {
            // Is there an overlap of this neighbours ranges with any of the ungrounded ranges of terrainCol?
            neighbour.landingRanges.forEach(neighbourRange => {
              const {startY:neighbourRangeStart, endY:neighbourRangeEnd} = neighbourRange;
              for (let i = 0; i < rangeCounts.length; i++) {
                const currRangeCountPair = rangeCounts[i];
                const {startY:currRangeStart, endY:currRangeEnd} = landingRanges[currRangeCountPair[0]];
                if (currRangeStart > neighbourRangeEnd) { break; } // No possible overlap (assuming sorted, mutually exclusive ranges)
                if (currRangeStart < neighbourRangeEnd && currRangeEnd > neighbourRangeStart) {
                  currRangeCountPair[1]++;
                }
              }
            });
          }
        });

        // If any of the terrainCol ranges have no overlaps with neighbours and they aren't grounded
        // then they are stranded and should be removed
        rangeCounts.forEach(rangeCountPair => {
          const [rangeIdx, count] = rangeCountPair;
          if (count === 0) {
            terrainCol.detachLandingRange(rangeIdx);
          }
        });
      }
    }
  }
}