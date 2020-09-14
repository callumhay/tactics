import * as THREE from 'three';
import { assert } from 'chai';

import GeometryUtils from '../GeometryUtils';

import GameMaterials from './GameMaterials';
import Battlefield from './Battlefield';
import GameTypes from './GameTypes';

const tempVec3 = new THREE.Vector3();

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }
  
  constructor(battlefield, u, v, materialGroups) {
    this.battlefield = battlefield;
    this.xIndex = u;
    this.zIndex = v;

    this.terrainMesh = null;
    this.cachedTerrainCubeIdTriMatObjs = null;
    this.waterMesh = null;
    this.cachedWaterCubeIdTriMatObjs = null;

    this.physObject = null;

    const {terrainNodeLattice, waterNodeLattice} = this.battlefield;

    if (materialGroups && Object.keys(materialGroups).length > 0) {
      for (const matGrp of Object.values(materialGroups)) {
        const {material, geometry} = matGrp;
        const gameMaterial = GameMaterials.materials[material];
        assert(gameMaterial, "All material groups / geometry should have an associated material!");

        let nodeLattice = null;
        switch (gameMaterial.gameType) {
          case GameTypes.WATER:
            nodeLattice = waterNodeLattice;
            break;
          default:
            nodeLattice = terrainNodeLattice;
            break;
        }

        for (const geomPiece of geometry) {
          const {type} = geomPiece;
          switch (type) {
            case "box":
              const [startY, endY] = geomPiece.range;
              nodeLattice.addTerrainColumnBox(this, {startY, endY, material:gameMaterial});
              break;
            default:
              throw `Invalid geometry type ${type} found.`;
          }
        }
      }
    }
  }

  get id() {
    return `${this.xIndex},${this.zIndex}`;
  }

  _clearTerrainPhysics() {
    if (this.physObject) {
      const { physics } = this.battlefield;
      physics.removeObject(this.physObject);
      this.physObject = null;
    }
  }
  _clearTerrain() {
    if (this.terrainMesh) {
      const { terrainGroup } = this.battlefield;
      terrainGroup.remove(this.terrainMesh);
      this.terrainMesh.geometry.dispose();
      this.terrainMesh = null;
      this.cachedTerrainCubeIdTriMatObjs = null;
    }
    this._clearTerrainPhysics();
  }
  _clearWaterPhysics() {}
  _clearWater() {
    if (this.waterMesh) {
      const { terrainGroup } = this.battlefield;
      terrainGroup.remove(this.waterMesh);
      this.waterMesh.geometry.dispose();
      this.waterMesh = null;
      this.cachedWaterCubeIdTriMatObjs = null;
    }
  }

  clear() {
    this._clearTerrain();
    this._clearWater();
  }

  getBoundingBox() {
    return new THREE.Box3(
      new THREE.Vector3(this.xIndex, 0, this.zIndex), 
      new THREE.Vector3(this.xIndex+TerrainColumn.SIZE, Battlefield.MAX_HEIGHT, this.zIndex+TerrainColumn.SIZE)
    );
  }
  getCenter(target) {
    target.set(
      this.xIndex + TerrainColumn.HALF_SIZE, 
      Battlefield.HALF_MAX_HEIGHT, 
      this.zIndex + TerrainColumn.HALF_SIZE
    );
  }

  // Regenerate the geometry and associated data structures for this terrain column
  // If a set of cubeIds are provided then only those cubes will be examined and regenerated.
  regenerate(cubeIds=null) {
    const terrainTcAffectedMap = this._regenerateTerrain(cubeIds);
    const waterTcAffectedMap = this._regenerateWater(cubeIds);

    // Combine the two maps...
    for (const entry of Object.entries(waterTcAffectedMap)) {
      const [tcId, {terrainColumn, cubeIds:entryCubeIds}] = entry;
      if (!(tcId in terrainTcAffectedMap)) {
        terrainTcAffectedMap[tcId] = {terrainColumn, cubeIds:entryCubeIds};
      }
      else { 
        terrainTcAffectedMap[tcId].cubeIds = new Set([...terrainTcAffectedMap[tcId].cubeIds, ...entryCubeIds]);
      }
    } 

    return terrainTcAffectedMap;
  }

  _regenerateTerrain(cubeIds) {
    const { terrainNodeLattice, terrainGroup } = this.battlefield;
    const cubeIdToTriMatMap = {};
    const affectedTcMap = terrainNodeLattice.convertToTriangles(this, this.cachedTerrainCubeIdTriMatObjs, cubeIdToTriMatMap, cubeIds);
    
    // If there's geometry already present then we rebuild it's geometry with the updated cube triangles
    const maxTris = Math.floor(3 * Math.pow(terrainNodeLattice.numNodesPerUnit * TerrainColumn.SIZE - 1, 2) *
      (terrainNodeLattice.numNodesPerUnit * Battlefield.MAX_HEIGHT - 1));
    const maxVertices = 3 * maxTris;

    let material = null;
    if (this.terrainMesh) {
      const cubeIdToTriMapEntries = Object.entries(cubeIdToTriMatMap); 
      if (cubeIdToTriMapEntries.length === 0) { return affectedTcMap; } // Nothing was changed

      // Update our cached cube to triangle mapping with the changed cubes/tris
      for (const cubeIdToTriEntry of cubeIdToTriMapEntries) {
        const [id, triMatObjs] = cubeIdToTriEntry;
        this.cachedTerrainCubeIdTriMatObjs[id] = triMatObjs;
      }

      this._clearTerrainPhysics();
      const {geometry} = this.terrainMesh;
      const {materials} = GeometryUtils.updateBufferGeometryFromCubeTriMap(
        geometry, this.cachedTerrainCubeIdTriMatObjs, maxVertices);
      this.terrainMesh.material = materials.map(m => m.three);
      if (geometry.index.count === 0 || materials.length === 0) { return affectedTcMap; }
      material = materials[0];
    }
    else {
      this._clearTerrain();
      if (Object.values(cubeIdToTriMatMap).filter(tris => tris.length > 0).length === 0) { return affectedTcMap; } // Empty geometry
      this.cachedTerrainCubeIdTriMatObjs = cubeIdToTriMatMap;
      const {geometry, materials} = GeometryUtils.buildBufferGeometryFromCubeTriMap(this.cachedTerrainCubeIdTriMatObjs, maxVertices);
      this.terrainMesh = new THREE.Mesh(geometry, materials.map(m => m.three));
      this.terrainMesh.castShadow = true;
      this.terrainMesh.receiveShadow = true;
      terrainGroup.add(this.terrainMesh);
      if (geometry.index.count === 0 || materials.length === 0) { return affectedTcMap; } // Empty geometry
      material = materials[0];
    }
    GeometryUtils.centerMeshGeometryToTranslation(this.terrainMesh);

    const { physics } = this.battlefield;
    const config = {
      gameObject: this,
      material: material.cannon,
      mesh: this.terrainMesh,
    };
    this.physObject = physics.addTerrain(config);
    if (this.physObject === null) {
      this._clearTerrain();
    }

    return affectedTcMap;
  }

  _regenerateWater(cubeIds) {
    const { waterNodeLattice, terrainGroup } = this.battlefield;
    const cubeIdToTriMatMap = {};
    const affectedTcMap = waterNodeLattice.convertToTriangles(this, this.cachedWaterCubeIdTriMatObjs, cubeIdToTriMatMap, cubeIds);

    // If there's geometry already present then we rebuild it's geometry with the updated cube triangles
    const maxTris = Math.floor(2 * Math.pow(waterNodeLattice.numNodesPerUnit * TerrainColumn.SIZE - 1, 2) *
      (waterNodeLattice.numNodesPerUnit * Battlefield.HALF_MAX_HEIGHT - 1));
    const maxVertices = 3 * maxTris;

    if (this.waterMesh) {
      const cubeIdToTriMapEntries = Object.entries(cubeIdToTriMatMap); 
      if (cubeIdToTriMapEntries.length === 0) { return affectedTcMap; } // Nothing was changed

      // Update our cached cube to triangle mapping with the changed cubes/tris
      for (const cubeIdToTriEntry of cubeIdToTriMapEntries) {
        const [id, triMatObjs] = cubeIdToTriEntry;
        this.cachedWaterCubeIdTriMatObjs[id] = triMatObjs;
      }

      this._clearWaterPhysics();
      const {geometry} = this.waterMesh;
      const {materials} = GeometryUtils.updateBufferGeometryFromCubeTriMap(
        geometry, this.cachedWaterCubeIdTriMatObjs, maxVertices);
      assert(materials.length <= 1);
      this.waterMesh.material = materials.map(m => m.three);
      if (geometry.index.count === 0 || materials.length === 0) { return affectedTcMap; }
      //material = materials[0];
    }
    else {
      this._clearWater();
      if (Object.values(cubeIdToTriMatMap).filter(tris => tris.length > 0).length === 0) { return affectedTcMap; } // Empty geometry
      this.cachedWaterCubeIdTriMatObjs = cubeIdToTriMatMap;
      const {geometry, materials} = GeometryUtils.buildBufferGeometryFromCubeTriMap(this.cachedWaterCubeIdTriMatObjs, maxVertices);
      this.waterMesh = new THREE.Mesh(geometry, materials.map(m => m.three));
      this.waterMesh.castShadow = true;
      this.waterMesh.receiveShadow = true;
      terrainGroup.add(this.waterMesh);
      if (geometry.index.count === 0 || materials.length === 0) { return affectedTcMap; } // Empty geometry
      //material = materials[0];
    }
    GeometryUtils.centerMeshGeometryToTranslation(this.waterMesh);

    return affectedTcMap;
  }

  

  getTerrainSpaceTranslation() {
    const { xIndex, zIndex } = this;
    return new THREE.Vector3(
      xIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE, 0, zIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE
    );
  }

  toString() {
    const {xIndex,zIndex} = this;
    return `(${xIndex}, ${zIndex})`;
  }

  debugColour() {
    const { xIndex, zIndex, battlefield } = this;
    const h = (xIndex + 1) / battlefield._terrain.length + (zIndex + 1) / battlefield._terrain[xIndex].length;
    return (new THREE.Color()).setHSL(h,1,0.5);
  }
}

export default TerrainColumn;