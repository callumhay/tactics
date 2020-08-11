import * as THREE from 'three';

import Debug from '../debug';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 0.0001; }

  constructor(terrainGrp, u, v, landingRanges) {
    this.xIndex = u;
    this.zIndex = v;
    this.landingRanges = landingRanges;

    this.geometries = [];
    this.meshes = [];
    this.material = new THREE.MeshLambertMaterial({color: 0xffffff});

    this.landingRanges.forEach(range => {
      const [startY, endY] = range;
      const height = endY-startY;

      const geometry = new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height*TerrainColumn.SIZE, TerrainColumn.SIZE);
      const mesh = new THREE.Mesh(geometry, this.material);
      mesh.translateX(this.xIndex*TerrainColumn.SIZE + TerrainColumn.HALF_SIZE);
      mesh.translateZ(this.zIndex*TerrainColumn.SIZE + TerrainColumn.HALF_SIZE);
      mesh.translateY(startY + height/2);
      mesh.terrainColumn = this;

      this.geometries.push(geometry);
      this.meshes.push(mesh);

      terrainGrp.add(mesh);
    });

    // Debug AABBs
    /*
    const aabbs = this.calcAABBs();
    const currCenter = new THREE.Vector3();
    aabbs.forEach(aabb => {
      const edges = new THREE.EdgesGeometry(new THREE.BoxBufferGeometry(aabb.max.x-aabb.min.x, aabb.max.y-aabb.min.y, aabb.max.z-aabb.min.z));
      const mesh = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0xFF00FF, linewidth: 0.1, depthFunc: THREE.AlwaysDepth}));
      
      aabb.getCenter(currCenter)
      mesh.translateX(currCenter.x);
      mesh.translateZ(currCenter.z);
      mesh.translateY(currCenter.y);
      terrainGrp.add(mesh);
    });
    */
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

  debugDrawAABBs(terrainGrp, show=true) {
    if (show && this.debugAABBGroup || !show && !this.debugAABBGroup) { return; }

    if (this.debugAABBGroup) {
      terrainGrp.remove(this.debugAABBGroup);
      this.debugAABBGroup.children.forEach(child => {
        child.geometry.dispose();
      });
      this.debugAABBGroup = null;
    }

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
      terrainGrp.add(this.debugAABBGroup);
    }
  }
}

export default TerrainColumn;