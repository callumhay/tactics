import * as THREE from 'three';
import CSG from 'three-csg';
//import * as CANNON from 'cannon';

import Debug from '../debug';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }

  constructor(battlefield, u, v, landingRanges) {
    this.battlefield = battlefield;
    const {terrainGroup, physics} = this.battlefield;

    this.xIndex = u;
    this.zIndex = v;
    this.landingRanges = landingRanges;
    this.meshes = []; // Corresponding to each landingRange

    this.material = new THREE.MeshLambertMaterial({color: 0xffffff});

    // If there are any zero/negative sized ranges they must be removed
    this.landingRanges = this.landingRanges.filter(range => range[1] - range[0] > 0);
    // Sort the landing ranges by their starting ("startY") coordinate
    this.landingRanges.sort((a,b) => a[0]-b[0]);
    // Basic clean-up: Merge together any overlaps
    this.landingRanges = TerrainColumn.mergeLandingRanges(this.landingRanges);

    // Build the THREE geometry and meshes for each interval
    this.landingRanges.forEach((range,rangeIdx) => {
      const [startY, endY] = range;
      const height = endY-startY;
      const geometry = new THREE.BoxBufferGeometry(TerrainColumn.SIZE, height*TerrainColumn.SIZE, TerrainColumn.SIZE);
      const mesh = this._buildTerrainMesh(geometry, rangeIdx);
      this.meshes.push(mesh);
      terrainGroup.add(mesh);
    });

    // Build the physical representation of the terrain
    this.physicsObjs = [];
    const physicsConfig = {
      type: 'kinematic',
      shape: "box",
      size: [1,1,1],
    };
    for (let i = 0; i < this.landingRanges.length; i++) {
      const {size} = physicsConfig;
      const [startY, endY] = this.landingRanges[i];
      const mesh = this.meshes[i];
      size[1] = endY - startY;
      this.physicsObjs.push(physics.addObject(mesh, physicsConfig));
    }
  }

  // NOTE: landingRanges must be sorted by the starting element of each range in ascending order
  static mergeLandingRanges(landingRanges) {
    if (landingRanges.length === 0) { return landingRanges; }
    const stack = [landingRanges[0]];
    let top = null;

    // Start from the next interval and merge if needed
    for (let i = 1; i < landingRanges.length; i++) {
      top = stack[stack.length - 1]; // Get the top element

      // If the current interval doesn't overlap with the stack top element, push it to the stack
      if (top[1] < landingRanges[i][0]) {
        stack.push(landingRanges[i]);
      }
      else if (top[1] <= landingRanges[i][1]) {
        // Otherwise update the end value of the top element if end of current interval is higher
        top[1] = landingRanges[i][1];
        stack.pop();
        stack.push(top);
      }
    }
    return stack;
  }

  clear() {
    const {terrainGroup} = this.battlefield;
    this.meshes.forEach(mesh => {
      terrainGroup.remove(mesh);
      mesh.geometry.dispose();
    });
    this.meshes = [];

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

  detachLandingRange(rangeIdx) {
    const rangeToRemove = this.landingRanges[rangeIdx];
    console.log(`Removing landing range (${rangeToRemove[0]}, ${rangeToRemove[1]}) in TerrainColumn (${this.xIndex}, ${this.zIndex}).`);
    
    // We'll need to convert the landing range into debris that is physics-based
    const detachedMesh = this.meshes[rangeIdx];
    const detachedPhysObj = this.physicsObjs[rangeIdx];

    // Remove the range from this
    this.landingRanges.splice(rangeIdx, 1);
    this.meshes.splice(rangeIdx, 1);
    this.physicsObjs.splice(rangeIdx, 1);

    // We need to remove the old physical object for the terrain
    const {physics} = this.battlefield;
    physics.removeObject(detachedPhysObj);

    // TODO: Do we treat the debris as a box? a mesh? does it break apart? etc.
    const size = [1, rangeToRemove[1] - rangeToRemove[0], 1];
    const config = {
      type: 'dynamic',
      mass: size[0] * size[1] * size[2], // TODO: material mass??
      shape: "box",
      size: size,
    };
    this.battlefield.convertTerrainToDebris(detachedMesh, config);
  }


  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    const {terrainGroup} = this.battlefield;
    const {boundingBox} = subtractGeometry;

    // Figure out what terrain geometry will be affected in this column
    let collidingIndices = [];
    const minY = Math.floor(boundingBox.min.y);
    const maxY = Math.floor(boundingBox.max.y);
    for (let i = 0; i < this.landingRanges.length; i++) {
      const [startY, endY] = this.landingRanges[i];
      if (startY <= maxY && endY >= minY) {
        collidingIndices.push(i);
      }
    }

    collidingIndices.forEach(index => {
      const collidingMesh = this.meshes[index];
      const collidingGeometry = collidingMesh.geometry;

      // The geometry needs to be placed into the terrain space
      const translation = this.getTerrainSpaceTranslation(index);
      collidingGeometry.translate(translation.x, translation.y, translation.z);

      const csgGeometry = CSG.subtract([collidingGeometry, subtractGeometry]);
      const newGeometry = CSG.BufferGeometry(csgGeometry);
      newGeometry.translate(-translation.x, -translation.y, -translation.z);
      const newMesh = this._buildTerrainMesh(newGeometry, index);

      // Clean-up and replace the appropriate THREE objects
      terrainGroup.remove(collidingMesh);
      collidingGeometry.dispose();
      terrainGroup.add(newMesh);

      this.meshes[index] = newMesh;
    });
  }

  _clearDebugAABBGroup() {
    if (this.debugAABBGroup) {
      const {terrainGroup} = this.battlefield;
      terrainGroup.remove(this.debugAABBGroup);
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
      const {terrainGroup} = this.battlefield;
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
      terrainGroup.add(this.debugAABBGroup);
    }
  }
}

export default TerrainColumn;