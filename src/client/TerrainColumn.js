import * as THREE from 'three';
import { assert } from 'chai';

import GeometryUtils from '../GeometryUtils';

import GameMaterials from './GameMaterials';
import MarchingCubes from './MarchingCubes';
import { MeshLambertMaterial, Geometry } from 'three';
import Battlefield from './Battlefield';

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

  clear() {
    if (this.mesh) {
      const { terrainGroup } = this.battlefield;
      terrainGroup.remove(this.mesh);
      this.mesh.geometry.dispose();
      this.mesh = null;
    }
    if (this.physObject) {
      const { physics } = this.battlefield;
      physics.removeObject(this.physObject);
      this.physObject = null;
    }
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

  regenerate() {
    this.clear();

    const { rigidBodyLattice, terrainGroup } = this.battlefield;
    const nodeCubeCells = rigidBodyLattice.getTerrainColumnCubeCells(this);
    const triangles = [];
    const otherAffectedTerrainCols = MarchingCubes.convertTerrainColumnToTriangles(this, nodeCubeCells, triangles);
    if (triangles.length === 0) { return otherAffectedTerrainCols; }
    const geometry = GeometryUtils.buildBufferGeometryFromTris(triangles);
    if (geometry.getAttribute('position').count === 0) { return otherAffectedTerrainCols; } // Empty terrain column

    geometry.computeBoundingBox();
    const {boundingBox} = geometry;
    boundingBox.getCenter(tempVec3);
    geometry.translate(-tempVec3.x, -tempVec3.y, -tempVec3.z);

    const debugColour = this.debugColour();
    debugColour.setRGB(debugColour.b, debugColour.g, debugColour.r).multiplyScalar(0.25);
    //const material = new MeshLambertMaterial({color:0xcccccc});//{emissive:debugColour});
    this.mesh = new THREE.Mesh(geometry, this.material.three);
    this.mesh.castShadow = true;
    this.mesh.receiveShadow = false;

    this.mesh.translateX(tempVec3.x);
    this.mesh.translateY(tempVec3.y);
    this.mesh.translateZ(tempVec3.z);
    this.mesh.updateMatrixWorld();

    terrainGroup.add(this.mesh);

    const { physics } = this.battlefield;
    const config = {
      gameObject: this,
      material: GameMaterials.materials[GameMaterials.MATERIAL_TYPE_ROCK].cannon, // TODO FIX THIS
      mesh: this.mesh,
    };
    this.physObject = physics.addTerrain(config);
    if (this.physObject === null) {
      this.clear();
    }

    return otherAffectedTerrainCols;
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