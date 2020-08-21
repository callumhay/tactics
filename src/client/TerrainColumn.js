import * as THREE from 'three';
import CSG from 'three-csg';

import LandingRange from './LandingRange';
import Debug from '../debug';
import Battlefield from './Battlefield';

class TerrainColumn {
  static get SIZE() { return 1; }
  static get HALF_SIZE() { return TerrainColumn.SIZE/2; }
  static get EPSILON() { return 1e-6; }
  
  constructor(battlefield, u, v, materialGroups) {
    this.battlefield = battlefield;
    this.xIndex = u;
    this.zIndex = v;
    this.landingRanges = [];

    if (materialGroups && Object.keys(materialGroups).length > 0) { 
      materialGroups.forEach(matGrp => {
        const {type, landingRanges} = matGrp;
        for (const landingRange of landingRanges) {
          const [startY, endY] = landingRange;
          this.landingRanges.push(new LandingRange(this, {type, startY, endY}));
        }
      });

      // If there are any zero/negative sized ranges they must be removed
      this.landingRanges = this.landingRanges.filter(range => range.height > 0);
      // Sort the landing ranges by their starting ("startY") coordinate
      this.landingRanges.sort((a, b) => a.startY - b.startY);
      // Clean-up: Merge together any overlaps
      this.landingRanges = TerrainColumn.mergeLandingRanges(this.landingRanges);
    }
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
          const newTopGeometry = LandingRange.buildBasicGeometry(currRng.startY-top.startY);
          currRng.clear();
          top.regenerate(top.startY, newTopGeometry);
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
            const newTopGeometry = LandingRange.buildBasicGeometry(top.endY-currRng.endY);
            top.regenerate(currRng.endY, newTopGeometry);
            stack.pop(); stack.push(bottom); stack.push(currRng); stack.push(top);
          }
        }
        else {
          // There is an overlap of the top and currRng
          if (currRng.materialType === top.materialType) {
            // If the materials are the same then merge the two ranges
            const newTopGeometry = LandingRange.buildBasicGeometry(currRng.endY-top.startY);
            top.regenerate(top.startY, newTopGeometry);
            currRng.clear();
            //stack.pop(); stack.push(top);
          }
          else {
            // ... otherwise, shorten the current range and add it to the stack
            const newCurrRngGeometry = LandingRange.buildBasicGeometry(currRng.endY-top.endY);
            currRng.regenerate(top.endY, newCurrRngGeometry);
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

  getTerrainSpaceTranslation() {
    const { xIndex, zIndex } = this;
    return new THREE.Vector3(
      xIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE, 0, zIndex * TerrainColumn.SIZE + TerrainColumn.HALF_SIZE
    );
  }

  computeBoundingBoxes(epsilon=TerrainColumn.EPSILON) {
    return this.landingRanges.map(landingRange => landingRange.computeBoundingBox(epsilon));
  }

  landingRangesContainingPoint(pt) {
    const result = [];
    const boundingBoxes = this.computeBoundingBoxes();
    for (let i = 0; i < boundingBoxes.length; i++) {
      if (boundingBoxes[i].containsPoint(pt)) {
        result.push(this.landingRanges[i])
      }
    }
    return result;
  }

  detachLandingRange(rangeToRemove) {
    const rangeIdx = this.landingRanges.indexOf(rangeToRemove);
    if (rangeIdx < 0) { return; }
    console.log(`Removing landing range ${rangeToRemove} in TerrainColumn ${this}.`);
    // We'll need to convert the landing range into debris that is physics-based
    const { height, material, mesh: detachedMesh, physicsObj: detachedPhysObj } = rangeToRemove;
    this.landingRanges.splice(rangeIdx, 1);
    rangeToRemove.terrainColumn = null;
  }

  attachDebris(debris) {
    const {mesh} = debris;
    const {geometry} = mesh;

    // Find the upper and lower bounds of the y-coordinates in this column
    const boundingBox = geometry.boundingBox.clone();

    // Find the closest existing landing range under the given one
    const { min: bbMin, max: bbMax } = boundingBox;
    let mergeLandingRange = null;
    for (const range of this.landingRanges) {
      const {endY} = range;
      if (endY >= (bbMin.y - TerrainColumn.EPSILON)) {
        mergeLandingRange = range;
        break;
      }
    }

    // We need to only take the part of the debris that's in this column:
    // Use an CSG intersection with a tall bounding box representing this column
    const columnBoxHeight = Battlefield.MAX_HEIGHT + TerrainColumn.SIZE;
    const columnBox = new THREE.BoxBufferGeometry(TerrainColumn.SIZE + TerrainColumn.EPSILON, columnBoxHeight, TerrainColumn.SIZE + TerrainColumn.EPSILON);
    const translation = this.getTerrainSpaceTranslation();
    translation.y = columnBoxHeight / 2 - TerrainColumn.HALF_SIZE;
    columnBox.translate(translation.x, translation.y, translation.z);
    // Debugging for visualizing the columnBox
    //const {terrainGroup} = this.battlefield;
    //const temp = new THREE.Mesh(columnBox, new MeshBasicMaterial({color:0x0000CC, transparent: true, opacity: 0.25}));
    //terrainGroup.add(temp);

    const csgGeometry = CSG.intersect([columnBox, geometry]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    // Debugging for visualizing the resulting CSG geometry
    //const testNewGeom = new THREE.Mesh(newGeometry, new THREE.MeshBasicMaterial({color:0x0000CC, transparent: true, opacity: 0.25, depthFunc:THREE.AlwaysDepth}));
    //const testBB = new THREE.Box3Helper(boundingBox);
    //terrainGroup.add(testNewGeom);
    //terrainGroup.add(testBB);

    // Take the chunk that becomes part of this column out of the debris 
    debris.subtractGeometry(columnBox);

    // Merge the new geometry into a landing range or create a new one
    const {rigidBodyLattice} = this.battlefield;
    if (mergeLandingRange) {

    }
    else {
      // TODO: WHAT IF THERE ARE MULTIPLE LEVELS IN THE GEOMETRY?

      const newLandingRange = LandingRange.buildFromGeometry(newGeometry, this, debris.material);
      this.landingRanges.push(newLandingRange);
      rigidBodyLattice.addLandingRangeNodes(newLandingRange, true);
    }

    // Re-traverse the rigid body node lattice, find out if anything is no longer attached to the ground
    rigidBodyLattice.traverseGroundedNodes();
    rigidBodyLattice.debugDrawNodes(true);
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    const {boundingBox} = subtractGeometry;
    // Figure out what terrain geometry will be affected in this column
    const collidingRanges = this.landingRanges.map((range,idx) => [idx, range]).filter(idxRangePair => idxRangePair[1].startY <= boundingBox.max.y && idxRangePair[1].endY >= boundingBox.min.y);
    
    // BLOW IT UP!
    const lrIndicesToRemove = [];
    for (const idxRangePair of collidingRanges) {
      const [idx, landingRange] = idxRangePair;
      landingRange.blowupTerrain(subtractGeometry);
      if (landingRange.isEmpty()) {
        lrIndicesToRemove.push(idx);
      }
    }
    for (const lrIdx of lrIndicesToRemove) {
      this.landingRanges.splice(lrIdx, 1);
    }

    // Re-traverse the rigid body node lattice, find out if anything is no longer attached to the ground
    const {rigidBodyLattice} = this.battlefield;
    rigidBodyLattice.traverseGroundedNodes();
    
    const islands = rigidBodyLattice.traverseIslands(this.landingRanges);
    console.log(islands);
    /*
    // Figure out what's no longer attached and detach it as a separated landing range
    for (const islandNodeSet of islands) {
      
    }
    */

    rigidBodyLattice.debugDrawNodes(true);
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
    this._clearDebugAABBGroup();
    if (show) {
      this.debugAABBGroup = new THREE.Group();
      const {terrainGroup} = this.battlefield;
      const aabbs = this.computeBoundingBoxes();
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