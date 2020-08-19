import * as THREE from 'three';
import * as CANNON from 'cannon';
import CSG from 'three-csg';

import GameMaterials from './GameMaterials';
import TerrainColumn from './TerrainColumn';
import GameTypes from './GameTypes';

const tempTargetVec3 = new THREE.Vector3();

class LandingRange {
  static get defaultPhysicsConfig() {
    return {
      physicsBodyType: CANNON.Body.KINEMATIC,
      gameType: GameTypes.ATTACHED_TERRAIN,
      shape: "box",
      size: [1, 1, 1],
    };
  }

  static buildBasicGeometry(height) {
    return new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height * TerrainColumn.SIZE, TerrainColumn.SIZE);
  }

  constructor(terrainColumn, config) {
    this.terrainColumn = terrainColumn;

    const { type, startY, endY } = config;
    this.materialType = type;

    this.material = GameMaterials.materials[this.materialType];
    this.mesh = null;
    this.physicsObj = null;

    // Build the bounding box (aabb) for this
    const height = (endY-startY);

    // Don't build anything if the landing range is degenerative - it will be cleaned up
    if (height > 0) {
      this.regenerate(startY, LandingRange.buildBasicGeometry(height));
    }
    else {
      console.warn(`Degenerative landing range found (${this.startY},${this.endY}). This will be cleaned up.`);
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

    // IMPORTANT: Order matters here, various regeneration routines depend on others finishing first!!
    this.clear();

    this._createTerrainMesh(startY, geometry);
    const {terrainGroup, rigidBodyLattice} = this.terrainColumn.battlefield;
    terrainGroup.add(this.mesh);
    this.physicsObj = this.buildPhysicsObj();

    // We need to remove/add rigid body nodes based on the new geometry
    if (rigidBodyLattice) { rigidBodyLattice.updateNodesForLandingRange(this); }
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

    const heightBefore = this.height;

    geometry.applyMatrix4(matrix); // Geometry needs to be placed into the terrain space
    const csgGeometry = CSG.subtract([geometry, subtractGeometry]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    newGeometry.applyMatrix4(invMatrix); // New geometry needs to be moved back into local space

    // We have to maintain the proper local space translation so that that the geometry is centered
    newGeometry.computeBoundingBox();

    const heightAfter = newGeometry.boundingBox.getSize(tempTargetVec3).y;
    newGeometry.translate(0, (heightBefore - heightAfter) / 2, 0);

    this.regenerate(this.startY, newGeometry);
  }

  mergeTerrain(mergeGeometry) {
    const {geometry, matrix} = this.mesh;
    const invMatrix = new THREE.Matrix4();
    invMatrix.getInverse(matrix);

    const heightBefore = this.height;

    geometry.applyMatrix4(matrix); // Geometry needs to be placed into the terrain space
    const csgGeometry = CSG.union([geometry, mergeGeometry]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    newGeometry.applyMatrix4(invMatrix); // New geometry needs to be moved back into local space

    // We have to maintain the proper local space translation so that that the geometry is centered
    newGeometry.computeBoundingBox();
    const heightAfter = newGeometry.boundingBox.getSize(tempTargetVec3).y;
    newGeometry.translate(0, (heightBefore-heightAfter)/2, 0);

    this.regenerate(this.startY, newGeometry);
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
    const config = LandingRange.defaultPhysicsConfig;
    config.material = this.material;
    const { size } = config;
    size[1] = this.height;

    return physics.addObject(this.mesh, config);
  }

  _createTerrainMesh(startY, geometry) {
    // IMPORTANT: Order matters here!
    this.mesh = new THREE.Mesh(geometry, this.material.threeMaterial);
    geometry.computeBoundingBox();

    const height = geometry.boundingBox.getSize(tempTargetVec3).y;
    const translation = this.getTerrainSpaceTranslation(startY, height);

    this.boundingBox = geometry.boundingBox.clone();
    this.boundingBox.translate(translation);

    this.mesh.translateX(translation.x);
    this.mesh.translateY(translation.y);
    this.mesh.translateZ(translation.z);
    this.mesh.updateMatrixWorld(); // IMPORTANT: We may use the matrix of this mesh sometime before rendering
    this.mesh.terrainLandingRange = this;
  }

  computeBoundingBox(epsilon = 0) {
    const result = this.boundingBox.clone();
    result.min.subScalar(epsilon);
    result.max.addScalar(epsilon);
    return result;
  }
}

export default LandingRange;