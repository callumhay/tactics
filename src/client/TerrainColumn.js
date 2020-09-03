import * as THREE from 'three';

import GeometryUtils from '../GeometryUtils';

import GameMaterials from './GameMaterials';
import MarchingCubes from './MarchingCubes';
import Battlefield from './Battlefield';
import { assert } from 'chai';
import { TetrahedronGeometry } from 'three';

const tempVec3 = new THREE.Vector3();

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }
  
  constructor(battlefield, u, v, materialGroups) {
    this.battlefield = battlefield;
    this.xIndex = u;
    this.zIndex = v;

    this.mesh = null;
    this.physObject = null;
    this.material = null;

    const {rigidBodyLattice} = this.battlefield;

    if (materialGroups && Object.keys(materialGroups).length > 0) {
      for (const matGrp of Object.values(materialGroups)) {
        const {material, geometry} = matGrp;
        const gameMaterial = GameMaterials.materials[material];

        // TODO: Materials??
        if (gameMaterial) { this.material = gameMaterial; }

        for (const geomPiece of geometry) {
          const {type} = geomPiece;
          switch (type) {
            case "box":
              const [startY, endY] = geomPiece.range;
              rigidBodyLattice.addTerrainColumnBox(this, {startY, endY, material:gameMaterial});
              break;
            default:
              throw `Invalid geometry type ${type} found.`;
          }
        }
      }
    }
    if (!this.material) {
      this.material = GameMaterials.materials[GameMaterials.MATERIAL_TYPE_ROCK];
    }
  }

  get id() {
    return `${this.xIndex},${this.zIndex}`;
  }
  _clearPhysics() {
    if (this.physObject) {
      const { physics } = this.battlefield;
      physics.removeObject(this.physObject);
      this.physObject = null;
    }
  }
  clear() {
    if (this.mesh) {
      const { terrainGroup } = this.battlefield;
      terrainGroup.remove(this.mesh);
      this.mesh.geometry.dispose();
      this.mesh = null;
      this.cachedCubeIdToTris = null;
    }
    this._clearPhysics();
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
    const cubeIdsExist = (cubeIds && cubeIds.size > 0);
    const { rigidBodyLattice, terrainGroup } = this.battlefield;

    const nodeCubeCells = cubeIdsExist ?  
      rigidBodyLattice.getAllAssociatedCubeCellsForCubeIds(cubeIds) : 
      rigidBodyLattice.getTerrainColumnCubeCells(this);
    
    const cubeIdToTriMap = {};
    const affectedTcMap = MarchingCubes.convertTerrainColumnToTriangles(this, nodeCubeCells, cubeIdToTriMap);
    
    // If there's geometry already present then we rebuild it's geometry with the updated cube triangles
    const maxTris = Math.floor(2 * Math.pow(rigidBodyLattice.numNodesPerUnit * TerrainColumn.SIZE - 1, 2) *
      (rigidBodyLattice.numNodesPerUnit * Battlefield.MAX_HEIGHT - 1));
    const maxVertices = 3 * maxTris;

    let geometry = null;
    if (this.mesh) {
      const cubeIdToTriMapEntries = Object.entries(cubeIdToTriMap); 
      if (cubeIdToTriMapEntries.length === 0) { return affectedTcMap; } // Nothing was changed

      // Update our cached cube to triangle mapping with the changed cubes/tris
      for (const cubeIdToTriEntry of cubeIdToTriMapEntries) {
        const [id, triangles] = cubeIdToTriEntry;
        this.cachedCubeIdToTris[id] = triangles;
      }

      this._clearPhysics();
      geometry = this.mesh.geometry;
      const drawCount = GeometryUtils.updateBufferGeometryFromCubeTriMap(
        geometry, this.cachedCubeIdToTris, maxVertices);
      if (drawCount === 0) { return affectedTcMap; }
    }
    else {
      this.clear();
      if (Object.values(cubeIdToTriMap).filter(tris => tris.length > 0).length === 0) { return affectedTcMap; } // Empty geometry
      this.cachedCubeIdToTris = cubeIdToTriMap;
      geometry = GeometryUtils.buildBufferGeometryFromCubeTriMap(this.cachedCubeIdToTris, maxVertices);
      if (geometry.drawRange.count === 0) { geometry.dispose(); return affectedTcMap; } // Empty geometry
      this.mesh = new THREE.Mesh(geometry, this.material.three);
      this.mesh.castShadow = true;
      this.mesh.receiveShadow = true;
      terrainGroup.add(this.mesh);
    }

    geometry.computeBoundingBox();
    const {boundingBox} = geometry;
    boundingBox.getCenter(tempVec3);
    geometry.center();
    //const debugColour = this.debugColour();
    //debugColour.setRGB(debugColour.b, debugColour.g, debugColour.r).multiplyScalar(0.25);
    this.mesh.position.copy(tempVec3);    
    this.mesh.updateMatrixWorld();

    //terrainGroup.add(new THREE.Box3Helper(boundingBox.clone().applyMatrix4(this.mesh.matrixWorld)));

    const { physics } = this.battlefield;
    const config = {
      gameObject: this,
      material: this.material.cannon,
      mesh: this.mesh,
    };
    this.physObject = physics.addTerrain(config);
    if (this.physObject === null) {
      this.clear();
    }

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