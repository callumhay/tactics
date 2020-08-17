import * as THREE from 'three';

import LandingRange from './LandingRange';
import Debug from '../debug';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }

  constructor(battlefield, u, v, materialGroups) {
    this.battlefield = battlefield;
    this.xIndex = u;
    this.zIndex = v;
    this.landingRanges = [];
    materialGroups.forEach(matGrp => {
      const {type, landingRanges} = matGrp;
      landingRanges.forEach(landingRange => {
        const [startY, endY] = landingRange;
        this.landingRanges.push(new LandingRange(this, {type, startY, endY}));
      });
    });

    // If there are any zero/negative sized ranges they must be removed
    this.landingRanges = this.landingRanges.filter(range => range.height > 0);
    // Sort the landing ranges by their starting ("startY") coordinate
    this.landingRanges.sort((a,b) => a.startY-b.startY);
    // Clean-up: Merge together any overlaps
    this.landingRanges = TerrainColumn.mergeLandingRanges(this.landingRanges);
  }

  // NOTE: landingRanges must be sorted by the starting element of each range in ascending order,
  // ranges must also all have a > EPSILON height.
  static mergeLandingRanges(landingRanges) {
    if (landingRanges.length === 0) { return landingRanges; }
    const stack = [landingRanges[0]];
    let top = null;

    // Start from the next interval and merge if needed
    for (let i = 1; i < landingRanges.length; i++) {
      const currRng = landingRanges[i];
      top = stack[stack.length - 1]; // Get the top element

      // If the current interval doesn't overlap with the stack top element, 
      // OR it does slightly BUT the two ranges have different materials 
      // then we add the currRng to the stack
      if (top.endY <= currRng.startY) {
        if (Math.abs(top.endY - currRng.startY) > TerrainColumn.EPSILON || 
            top.materialType !== currRng.materialType) {
          stack.push(currRng);
        }
        else {
          console.log(`Merging landing ranges [${top},${currRng}] in TerrainColumn (${this.xIndex},${this.zIndex}).`);
          // Materials are the same and they are both on top of one another, 
          // merge them into the top and discard currRng
          top.endY = currRng.startY;
          currRng.clear();
          top.regenerate();
          //stack.pop(); stack.push(top);
        }
      }
      else if ((currRng.endY - top.endY) > TerrainColumn.EPSILON) {
        if ((currRng.startY - top.startY) > TerrainColumn.EPSILON) {
          // currRng is completely inside of the top range
          if (currRng.materialType === top.materialType) {
            // If the materials are the same then we just remove the currRng
            currRng.clear();
          }
          else {
            // ...otherwise, we split up the top into two ranges that straddle the currRng on top and bottom
            const bottom = new LandingRange(this, {startY:top.startY, endY:currRng.startY, type:top.type});
            top.startY = currRng.endY;
            top.regenerate();
            stack.pop(); stack.push(bottom); stack.push(currRng); stack.push(top);
          }
        }
        else {
          // There is an overlap of the top and currRng
          if (currRng.materialType === top.materialType) {
            // If the materials are the same then merge the two ranges
            top.endY = currRng.endY;
            top.regenerate();
            currRng.clear();
            //stack.pop(); stack.push(top);
          }
          else {
            // ... otherwise, shorten the current range and add it to the stack
            currRng.startY = top.endY;
            currRng.regenerate();
            stack.push(currRng);
          }
        }
      }
    }
    return stack;
  }

  clear() {
    this.landingRanges.forEach(landingRange => {
      landingRange.clear();
    });
    this.landingRanges = [];

    this.xIndex = -1;
    this.zIndex = -1;
  
    this._clearDebugAABBGroup();
  }

  calcAABBs(epsilon=TerrainColumn.EPSILON) {
    return this.landingRanges.map(landingRange => landingRange.calcAABB(epsilon));
  }
  containsPoint(pt, aabbs=[]) {
    const boundingBoxes = aabbs && aabbs.length > 0 ? aabbs : this.calcAABBs();
    for (let i = 0; i < boundingBoxes.length; i++) {
      if (boundingBoxes[i].containsPoint(pt)) { return true; }
    }
    return false;
  }
  landingRangesContainingPoint(pt) {
    const result = [];
    const boundingBoxes = this.calcAABBs();
    for (let i = 0; i < boundingBoxes.length; i++) {
      if (boundingBoxes[i].containsPoint(pt)) {
        result.push(this.landingRanges[i])
      }
    }
    return result;
  }

  detachLandingRange(rangeIdx) {
    const rangeToRemove = this.landingRanges[rangeIdx];
    console.log(`Removing landing range ${rangeToRemove} in TerrainColumn ${this}.`);

    // We'll need to convert the landing range into debris that is physics-based
    const {height, material, mesh:detachedMesh, physicsObj:detachedPhysObj} = rangeToRemove;
    this.landingRanges.splice(rangeIdx, 1);

    // We need to remove the old physical object for the terrain
    const {physics} = this.battlefield;
    physics.removeObject(detachedPhysObj);

    // Make sure there are no longer any nodes associated with the detached range - it's possible that this
    // is being called during initialization (in which case there isn't a lattice yet so we don't need to worry about it)
    const {rigidBodyLattice} = this.battlefield;
    if (rigidBodyLattice) { rigidBodyLattice.removeNodesInsideLandingRange(rangeToRemove); }

    // TODO: Do we treat the debris as a box? a mesh? does it break apart? etc.

    // NOTE: Removing a small epsilon from the side size is important too avoid
    // friction and collision anomolies with the rest of the terrain
    const sideWidthEpsilon = TerrainColumn.SIZE - TerrainColumn.EPSILON;
    const size = [sideWidthEpsilon, height - TerrainColumn.EPSILON, sideWidthEpsilon];
    const mass = material.density * size[0] * size[1] * size[2];
    const config = {
      physicsBodyType: 'dynamic',
      mass: mass,
      shape: "box",
      size: size,
      material: material.cannonMaterial,
    };
    rangeToRemove.physicsObj = this.battlefield.convertTerrainToDebris(detachedMesh, config);
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    const {boundingBox} = subtractGeometry;
    // Figure out what terrain geometry will be affected in this column
    const collidingRanges = this.landingRanges.filter(range => range.startY <= boundingBox.max.y && range.endY >= boundingBox.min.y);
    // BLOW IT UP!
    collidingRanges.forEach(landingRange => {
      landingRange.blowupTerrain(subtractGeometry);
    });
  }

  toString() {
    const {xIndex,zIndex} = this;
    return `(${xIndex}, ${zIndex})`;
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