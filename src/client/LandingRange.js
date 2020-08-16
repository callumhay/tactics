import * as THREE from 'three';
import CSG from 'three-csg';

import GameMaterials from './GameMaterials';
import TerrainColumn from './TerrainColumn';

class LandingRange {
  static get defaultPhysicsConfig() {
    return {
      physicsBodyType: 'kinematic',
      shape: "box",
      size: [1, 1, 1],
    };
  }

  constructor(terrainColumn, config) {
    this.terrainColumn = terrainColumn;

    const { type, startY, endY } = config;
    this.materialType = type;

    this.material = GameMaterials.materials[this.materialType];
    this.mesh = null;
    this.physicsObj = null;

    this.startY = startY;
    this.endY = endY;

    // Don't build anything if the landing range is degenerative - it will be cleaned up
    if (this.startY < this.endY) {
      this.regenerate();
    }
    else {
      console.warn(`Degenerative landing range found (${this.startY},${this.endY}). This will be cleaned up.`);
    }
  }

  get height() {
    return this.endY - this.startY;
  }

  clear() {
    if (this.mesh) {
      const { terrainGroup } = this.terrainColumn.battlefield;
      terrainGroup.remove(this.mesh);
      this.mesh.geometry.dispose();
      this.mesh = null;
    }
    if (this.physicsObj) {
      const { physics } = this.terrainColumn.battlefield;
      physics.removeObject(this.physicsObj);
      this.physicsObj = null;
    }
  }
  regenerate(geometry = null) {
    this.clear();
    this.mesh = this._buildTerrainMesh(geometry || new THREE.BoxBufferGeometry(TerrainColumn.SIZE, this.height * TerrainColumn.SIZE, TerrainColumn.SIZE));
    const { terrainGroup } = this.terrainColumn.battlefield;
    terrainGroup.add(this.mesh);
    this.physicsObj = this._buildPhysicsObj();
  }

  getTerrainSpaceTranslation() {
    const { xIndex, zIndex } = this.terrainColumn;
    return new THREE.Vector3(
      xIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE,
      this.startY + this.height / 2,
      zIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE
    );
  }

  collidesAABB(aabb) {
    const { startY, endY } = this;
    return startY <= aabb.max.y && endY >= aabb.min.y;
  }

  blowupTerrain(subtractGeometry) {
    const collidingMesh = this.mesh;
    const collidingGeometry = collidingMesh.geometry;

    // The geometry needs to be placed into the terrain space
    const translation = this.getTerrainSpaceTranslation();
    collidingGeometry.translate(translation.x, translation.y, translation.z);

    const csgGeometry = CSG.subtract([collidingGeometry, subtractGeometry]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    newGeometry.translate(-translation.x, -translation.y, -translation.z);

    this.regenerate(newGeometry);
  }

  calcAABB(epsilon) {
    const { xIndex, zIndex } = this.terrainColumn;
    const { startY, endY } = this;
    const startXPos = xIndex * TerrainColumn.SIZE;
    const startZPos = zIndex * TerrainColumn.SIZE;
    const endXPos = startXPos + TerrainColumn.SIZE;
    const endZPos = startZPos + TerrainColumn.SIZE;

    return new THREE.Box3(
      new THREE.Vector3(startXPos - epsilon, startY - epsilon, startZPos - epsilon),
      new THREE.Vector3(endXPos + epsilon, endY + epsilon, endZPos + epsilon)
    );
  }

  toString() {
    const { startY, endY } = this;
    return `(${startY},${endY})`;
  }

  _buildTerrainMesh(geometry) {
    const translation = this.getTerrainSpaceTranslation();
    const mesh = new THREE.Mesh(geometry, this.material.threeMaterial);
    mesh.translateX(translation.x);
    mesh.translateY(translation.y);
    mesh.translateZ(translation.z);
    mesh.terrainLandingRange = this;
    return mesh;
  }

  _buildPhysicsObj() {
    const { physics } = this.terrainColumn.battlefield;
    const config = LandingRange.defaultPhysicsConfig;
    config.material = this.material;
    const { size } = config;
    size[1] = this.height;

    return physics.addObject(this.mesh, config);
  }
}

export default LandingRange;