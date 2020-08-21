import * as THREE from 'three';
import CSG from 'three-csg';

import MathUtils from '../MathUtils';

import GameMaterials from './GameMaterials';
import TerrainColumn from './TerrainColumn';
import GameTypes from './GameTypes';

const tempTargetVec3 = new THREE.Vector3();

class LandingRange {
  static buildBasicGeometry(height) {
    return new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height * TerrainColumn.SIZE, TerrainColumn.SIZE);
  }
  static buildFromGeometry(geometry, terrainColumn, material) {
    const landingRange = new LandingRange(terrainColumn, {material, startY:0, endY:0});
    // The geometry will need to be translated into the local space of the landing range
    geometry.computeBoundingBox();
    const {boundingBox} = geometry;
    const startY = MathUtils.roundToDecimal(boundingBox.min.y, 2);
    const height = MathUtils.roundToDecimal(boundingBox.max.y - startY, 2);
    const translation = terrainColumn.getTerrainSpaceTranslation();
    translation.y = boundingBox.min.y + height / 2;
    translation.multiplyScalar(-1);
    geometry.translate(translation.x, translation.y, translation.z);
    landingRange.regenerate(startY, geometry);
    return landingRange;
  } 

  constructor(terrainColumn, config) {
    this.terrainColumn = terrainColumn;

    const { type, startY, endY, material } = config;
    this.materialType = type;

    this.material = material || GameMaterials.materials[this.materialType];
    this.mesh = null;
    this.physicsObj = null;

    // Build the bounding box (aabb) for this
    const height = (endY-startY) || 0;
    // Don't build anything if the landing range is degenerative
    if (height > 0) {
      this.regenerate(startY, LandingRange.buildBasicGeometry(height));
    }
  }

  get height() {
    return this.endY - this.startY;
  }
  get startY() {
    return this.boundingBox.min.y;
  }
  get endY() {
    return this.boundingBox.max.y;
  }

  isEmpty() {
    return (this.mesh === null);
  }

  clearGeometry(terrainColumn = this.terrainColumn) {
    if (this.mesh) {
      const { terrainGroup } = terrainColumn.battlefield;
      this.mesh.geometry.dispose();
      terrainGroup.remove(this.mesh);
    }
    this.mesh = null;
    this.boundingBox = null;
  }

  clear(terrainColumn=this.terrainColumn) {
    this.clearGeometry(terrainColumn);
    if (this.physicsObj) {
      const { physics } = terrainColumn.battlefield;
      physics.removeObject(this.physicsObj);
    }
    this.physicsObj = null;
  }

  regenerate(startY, geometry) {
    if (this.mesh && geometry === this.mesh.geometry) {
      console.error(`Attemting to regenerate LandingRange ${this} with same geometry.`);
      return;
    }

    const {terrainGroup, rigidBodyLattice} = this.terrainColumn.battlefield;

    // Check for tiny/negligable geometry
    const bbSize = new THREE.Vector3();
    geometry.computeBoundingBox();
    geometry.boundingBox.getSize(bbSize);
    // If the resulting height/width/depth is smaller than the inter-node distance then we need to destroy this asap
    if (bbSize.x < rigidBodyLattice.unitsBetweenNodes || bbSize.y < rigidBodyLattice.unitsBetweenNodes || bbSize.z < rigidBodyLattice.unitsBetweenNodes) {
      rigidBodyLattice.removeNodesInsideLandingRange(this);
      this.clear();
      return;
    }

    this.clear();

    this._createTerrainMesh(startY, geometry);
    terrainGroup.add(this.mesh);
    this.physicsObj = this.buildPhysicsObj();

    // We need to remove/add rigid body nodes based on the new geometry
    rigidBodyLattice.updateNodesForLandingRange(this); // TODO: THIS IS BUGGY, IF THE LANDING RANGE GEOMETRY HAS CHANGED THEN THIS WILL LEAVE NODES BEHIND DUE TO CHANGED STARTY/ENDY VALUES... MAYBE DO IT FOR THE ENTIRE TERRAINCOLUMN?
  }

  getTerrainSpaceTranslation(startY, height) {
    const translation = this.terrainColumn.getTerrainSpaceTranslation();
    translation.y = startY + height / 2;
    return translation;
  }

  blowupTerrain(subtractGeometry) {
    const {geometry, matrix} = this.mesh;
    const invMatrix = new THREE.Matrix4();
    invMatrix.getInverse(matrix);

    geometry.applyMatrix4(matrix); // Geometry needs to be placed into the terrain space
    const csgGeometry = CSG.subtract([geometry, subtractGeometry]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    newGeometry.computeBoundingBox();

    const bbSize = new THREE.Vector3();
    newGeometry.boundingBox.getSize(bbSize);

    const newStartY = newGeometry.boundingBox.min.y;
    newGeometry.applyMatrix4(invMatrix); // New geometry needs to be moved back into local space
    newGeometry.center();
    this.regenerate(newStartY, newGeometry);
  }

  toString() {
    const { startY, endY } = this;
    return `(${startY},${endY})`;
  }

  debugColour() {
    const { startY } = this;
    const { xIndex, zIndex, battlefield, landingRanges } = this.terrainColumn; 
    return new THREE.Color(
      (xIndex + 1) / battlefield._terrain.length,
      startY / landingRanges[landingRanges.length-1].startY,
      (zIndex+1) / battlefield._terrain[xIndex].length,
    );
  }

  buildPhysicsObj() {
    const { physics } = this.terrainColumn.battlefield;
    const config = {
      gameType: GameTypes.TERRAIN,
      material: this.material,
      mesh: this.mesh,
    };
    return physics.addTerrain(config);
  }

  _createTerrainMesh(startY, geometry) {
    // IMPORTANT: Order matters here!
    this.mesh = new THREE.Mesh(geometry, this.material.threeMaterial);
    this.mesh.castShadow = true;
    this.mesh.receiveShadow = false;
    geometry.computeBoundingBox();

    const height = geometry.boundingBox.getSize(tempTargetVec3).y;
    const translation = this.getTerrainSpaceTranslation(startY, height);

    this.mesh.translateX(translation.x);
    this.mesh.translateY(translation.y);
    this.mesh.translateZ(translation.z);
    this.mesh.updateMatrixWorld(); // IMPORTANT: We may use the matrix of this mesh sometime before rendering
    this.mesh.terrainLandingRange = this;

    this.boundingBox = geometry.boundingBox.clone();
    this.boundingBox.applyMatrix4(this.mesh.matrixWorld);
  }

  computeBoundingBox(epsilon = 0) {
    const result = this.boundingBox.clone();
    result.min.subScalar(epsilon);
    result.max.addScalar(epsilon);
    return result;
  }
}

export default LandingRange;