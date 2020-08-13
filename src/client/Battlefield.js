import * as THREE from 'three';
//import * as CANNON from 'cannon';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice'

export default class Battlefield {
  constructor(scene) {
    this._scene = scene;

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

    //this._buildDebugTerrain(10, 10);
  }

  setTerrain(terrain) {
    this._clearTerrain();
    this._terrain = terrain;
    const terrainSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(0, -terrainSize / 2, 0, -terrainSize / 2);

    // TODO: Do a basic terrain check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
    // remove them from the terrain and turn them into physics objects

    this._buildRigidbodyLattice();
  }

  _buildRigidbodyLattice() {
    if (this.rigidBodyLattice) { this.rigidBodyLattice.clear(); }
    this.rigidBodyLattice = new RigidBodyLattice(this.terrainGroup);
    this.rigidBodyLattice.buildFromTerrain(this._terrain);
    //this.rigidBodyLattice.debugDrawNodes();
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
    
    const terrainCols = [];
    for (let x = minX; x <= maxX; x++) {
      const terrainZ = this._terrain[x];
      const minZ = clamp(Math.floor(boundingBox.min.z), 0, terrainZ.length-1);
      const maxZ = clamp(Math.floor(boundingBox.max.z), 0, terrainZ.length-1);
      for (let z = minZ; z <= maxZ; z++) {
        terrainCols.push(terrainZ[z]);
      }
    }

    terrainCols.forEach(terrainCol => {
      terrainCol.blowupTerrain(subtractGeometry);
    });
  }

  _preloadCleanupTerrain() {
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        const terrainCol = this._terrain[x][z];

        const neighbours = [];
        if (x > 0) { neighbours.push(this._terrain[x-1][z]); }
        if (x < this._terrain.length-1) { neighbours.push(this._terrain[x+1][z]); }
        if (z > 0) { neighbours.push(this._terrain[x][z-1]); }
        if (z < this._terrain[x].length-1) { neighbours.push(this._terrain[x][z+1]); }

        neighbours.forEach(neighbour => {
          if (neighbour) {
            
          }
        });
      }
    }
  }


  _buildDebugTerrain(width, depth) {
    const terrain = new Array(width);
    for (let x = 0; x < width; x++) {
      const terrainZ = terrain[x] = new Array(depth).fill(null);
      for (let z = 0; z < depth; z++) {
        const firstLandingEnd = Math.floor(1 + Math.random() * 2);
        const secondLandingStart = Math.floor(firstLandingEnd + 1 + Math.random() * 2);
        terrainZ[z] = new TerrainColumn(this.terrainGroup, x, z, [[0, firstLandingEnd], [secondLandingStart, secondLandingStart + Math.floor(1 + Math.random() * 3)]]);
      }
    }
    this.setTerrain(terrain);
  }
}