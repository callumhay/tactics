import * as THREE from 'three';
import { assert } from 'chai';

import GeometryUtils from '../GeometryUtils';

import GameMaterials from './GameMaterials';
import MarchingCubes from './MarchingCubes';

const tempVec3 = new THREE.Vector3();

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }
  
  constructor(battlefield, u, v, materialGroups) {
    this.battlefield = battlefield;
    this.xIndex = u;
    this.zIndex = v;
    this.maxY = 0;

    this.mesh = null;
    this.physObject = null;

    const {rigidBodyLattice} = this.battlefield;

    if (materialGroups && Object.keys(materialGroups).length > 0) {
      for (const matGrp of Object.values(materialGroups)) {
        const {material, geometry} = matGrp;
        const gameMaterial = GameMaterials.materials[material];

        // TODO: Materials??
        this.material = gameMaterial;

        for (const geomPiece of geometry) {
          const {type} = geomPiece;
          switch (type) {
            case "box":
              const [startY, endY] = geomPiece.range;
              this.maxY = Math.max(this.maxY, endY);
              rigidBodyLattice.addTerrainColumnBox(this, {startY, endY, material:gameMaterial});
              break;
            default:
              throw `Invalid geometry type ${type} found.`;
          }
        }
        
        
      }
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

  regenerate() {
    this.clear();

    const { rigidBodyLattice, terrainGroup } = this.battlefield;
    const nodeCubeCells = rigidBodyLattice.getTerrainColumnCubeCells(this);
    const triangles = [];
    for (const nodeCubeCell of nodeCubeCells) {
      MarchingCubes.polygonizeNodeCubeCell(nodeCubeCell, triangles);
    }
    if (triangles.length === 0) { return; } // This is an empty terrain column, nothing else to do here.

    const geometry = GeometryUtils.buildBufferGeometryFromTris(triangles);
    geometry.computeBoundingBox();
    const {boundingBox} = geometry;
    boundingBox.getCenter(tempVec3);
    geometry.translate(-tempVec3.x, -tempVec3.y, -tempVec3.z);

    this.mesh = new THREE.Mesh(geometry, new THREE.MeshLambertMaterial({color:0xcccccc}));
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
      material: this.material,
      mesh: this.mesh,
    };
    this.physObject = physics.addTerrain(config);
  }

  getTerrainSpaceTranslation() {
    const { xIndex, zIndex } = this;
    return new THREE.Vector3(
      xIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE, 0, zIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE
    );
  }

  attachDebris(debris) {
    //const {mesh} = debris;
    //const {geometry} = mesh;

    // TODO:
    // 1. Find and activate all the nodes in this terrain column that are contained in the settled debris
    // 2. Rebuild this column's geometry
    // 3. Rebuild the geometry for this and recalculate landing ranges (regenerate)

    // Re-traverse the rigid body node lattice, find out if anything is no longer attached to the ground
    rigidBodyLattice.traverseGroundedNodes();
    rigidBodyLattice.debugDrawNodes(true);
    this.debugDrawAABBs(true);
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