import * as THREE from 'three';
//import * as CANNON from 'cannon';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice'

export default class Battlefield {
  constructor(scene, config={width:10, depth:10}) {
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

    const {width, depth} = config;
    this._buildDebugTerrain(width, depth);
    this._buildRigidbodyLattice();

    this.terrainGroup.translateX(-width/2);
    this.terrainGroup.translateZ(-depth/2);
    this._scene.add(this.terrainGroup);

    //this.debugDrawRigidBodyLattice();
  }

  _buildDebugTerrain(width, depth) {
    this.terrain = new Array(width);
    for (let x = 0; x < width; x++) {
      const terrainZ = this.terrain[x] = new Array(depth).fill(null);
      for (let z = 0; z < depth; z++) {
        const firstLandingEnd = Math.floor(1 + Math.random()*2);
        const secondLandingStart = Math.floor(firstLandingEnd + 1 + Math.random()*2);
        terrainZ[z] = new TerrainColumn(this.terrainGroup, x, z, [[0, firstLandingEnd], [secondLandingStart, secondLandingStart + Math.floor(1 + Math.random()*3)]]);
      }
    }
  }

  _buildRigidbodyLattice() {
    this.rigidBodyLattice = new RigidBodyLattice();
    this.rigidBodyLattice.buildFromTerrain(this.terrain);
  }

  clear() {
    for (let x = 0; x < this.terrain.length; x++) {
      for (let z = 0; z < this.terrain[x].length; z++) {
        this.terrain[x][z].clear();
      }
    }
    this.terrain = [];
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    if (!subtractGeometry.boundingBox) subtractGeometry.computeBoundingBox();
    const {boundingBox} = subtractGeometry;
    const {clamp} = THREE.MathUtils;

    // Get all the terrain columns that might be modified
    const minX = clamp(Math.floor(boundingBox.min.x), 0, this.terrain.length-1);
    const maxX = clamp(Math.floor(boundingBox.max.x), 0, this.terrain.length-1);
    
    const terrainCols = [];
    for (let x = minX; x <= maxX; x++) {
      const terrainZ = this.terrain[x];
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

  debugDrawRigidBodyLattice(show=true) {
    this.rigidBodyLattice.debugDrawNodes(this.terrainGroup, show);
  }

  
}