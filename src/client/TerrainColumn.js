import * as THREE from 'three';
import CSG from 'three-csg';
//import * as CANNON from 'cannon';

import Debug from '../debug';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }

  constructor(terrainGroup, u, v, landingRanges) {
    this.terrainGroup = terrainGroup;

    this.xIndex = u;
    this.zIndex = v;
    this.landingRanges = landingRanges;

    this.geometries = [];
    this.meshes = [];
    this.material = new THREE.MeshLambertMaterial({color: 0xffffff});

    this.landingRanges.forEach((range,rangeIdx) => {
      const [startY, endY] = range;
      const height = endY-startY;

      const geometry = new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height*TerrainColumn.SIZE, TerrainColumn.SIZE);
      const mesh = this._buildTerrainMesh(geometry, rangeIdx);
      
      this.geometries.push(geometry);
      this.meshes.push(mesh);

      this.terrainGroup.add(mesh);
    });
  }

  clear() {
    this.meshes.forEach(mesh => {
      this.terrainGroup.remove(mesh);
    });
    this.meshes = [];

    this.geometries.forEach(geometry => {
      geometry.dispose();
    });
    this.geometries = [];

    this.material = null;
    this.xIndex = -1;
    this.zIndex = -1;
    this.landingRanges = [];

    this._clearDebugAABBGroup();
  }

  getTerrainSpaceTranslation(rangeIdx) {
    const [startY, endY] = this.landingRanges[rangeIdx];
    const height = endY - startY;
    return new THREE.Vector3(
      this.xIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE,
      startY + height / 2,
      this.zIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE
    );
  }

  _buildTerrainMesh(geometry, rangeIdx) {
    const translation = this.getTerrainSpaceTranslation(rangeIdx);
    const mesh = new THREE.Mesh(geometry, this.material);
    mesh.translateX(translation.x);
    mesh.translateY(translation.y);
    mesh.translateZ(translation.z);
    mesh.terrainColumn = this;
    mesh.terrainLandingRangeIdx = rangeIdx;
    return mesh;
  }

  calcAABBs(epsilon=TerrainColumn.EPSILON) {
    const aabbs = [];
    const startXPos = this.xIndex * TerrainColumn.SIZE;
    const startZPos = this.zIndex * TerrainColumn.SIZE;
    const endXPos = startXPos + TerrainColumn.SIZE;
    const endZPos = startZPos + TerrainColumn.SIZE;
    for (let i = 0; i < this.landingRanges.length; i++) {
      const [startY, endY] = this.landingRanges[i];
      
      aabbs.push(new THREE.Box3(
        new THREE.Vector3(startXPos - epsilon, startY - epsilon, startZPos - epsilon),
        new THREE.Vector3(endXPos + epsilon, endY + epsilon, endZPos + epsilon)
      ));
    }
    return aabbs;
  }

  containsPoint(pt, aabbs=[]) {
    const boundingBoxes = aabbs && aabbs.length > 0 ? aabbs : this.calcAABBs();
    for (let i = 0; i < boundingBoxes.length; i++) {
      if (boundingBoxes[i].containsPoint(pt)) {
        return true;
      }
    }
    return false;
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    const {boundingBox} = subtractGeometry;

    // Figure out what terrain geometry will be affected in this column
    let collidingIndices = [];
    const minY = Math.floor(boundingBox.min.y);
    const maxY = Math.floor(boundingBox.max.y);
    for (let i = 0; i < this.landingRanges.length; i++) {
      const [startY, endY] = this.landingRanges[i];
      if (startY >= minY && startY <= maxY || 
          endY >= minY && endY <= maxY || 
          startY <= minY && endY >= maxY) { 
        collidingIndices.push(i);
      }
    }

    collidingIndices.forEach(index => {
      const collidingGeometry = this.geometries[index];
      const collidingMesh = this.meshes[index];

      // The geometry needs to be placed into the terrain space
      const translation = this.getTerrainSpaceTranslation(index);
      collidingGeometry.translate(translation.x, translation.y, translation.z);

      const csgGeometry = CSG.subtract([collidingGeometry, subtractGeometry]);
      const newGeometry = CSG.BufferGeometry(csgGeometry);
      newGeometry.translate(-translation.x, -translation.y, -translation.z);
      const newMesh = this._buildTerrainMesh(newGeometry, index);

      // Clean-up and replace the appropriate THREE objects
      this.terrainGroup.remove(collidingMesh);
      collidingGeometry.dispose();
      this.terrainGroup.add(newMesh);

      this.geometries[index] = newGeometry;
      this.meshes[index] = newMesh;
    });
  }

  _clearDebugAABBGroup() {
    if (this.debugAABBGroup) {
      this.terrainGroup.remove(this.debugAABBGroup);
      this.debugAABBGroup.children.forEach(child => {
        child.geometry.dispose();
      });
      this.debugAABBGroup = null;
    }
  }
  debugDrawAABBs(show=true) {
    if (show && this.debugAABBGroup || !show && !this.debugAABBGroup) { return; }

    this._clearDebugAABBGroup();

    if (show) {
      const aabbs = this.calcAABBs();
      const currCenter = new THREE.Vector3();
      aabbs.forEach(aabb => {
        const edges = new THREE.EdgesGeometry(new THREE.BoxBufferGeometry(aabb.max.x - aabb.min.x, aabb.max.y - aabb.min.y, aabb.max.z - aabb.min.z));
        const mesh = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0xFF00FF, linewidth: 0.1, depthFunc: THREE.AlwaysDepth }));

        aabb.getCenter(currCenter)
        mesh.translateX(currCenter.x);
        mesh.translateZ(currCenter.z);
        mesh.translateY(currCenter.y);
        this.debugAABBGroup.add(mesh);
      });
      this.debugAABBGroup.renderOrder = Debug.TERRAIN_AABB_RENDER_ORDER;
      this.terrainGroup.add(this.debugAABBGroup);
    }
  }
}

export default TerrainColumn;