import * as THREE from 'three';
import {assert} from 'chai'

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, columnsAndLandingRanges) {
    assert(xIdx >= 0, "xIdx of a LatticeNode must be at least zero.");
    assert(yIdx >= 0, "yIdx of a LatticeNode must be at least zero.");
    assert(zIdx >= 0, "zIdx of a LatticeNode must be at least zero.");

    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.columnsAndLandingRanges = columnsAndLandingRanges;
    this.grounded = false;
  }

  hasNoColumnsAndLandingRanges() {
    return Object.keys(this.columnsAndLandingRanges).length === 0;
  }

  hasLandingRange(landingRange) {
    const {terrainColumn} = landingRange;
    const landingRanges = this.columnsAndLandingRanges[terrainColumn];
    if (landingRanges) {
      for (const lr of landingRanges) {
        if (lr === landingRange) { return true; }
      }
    }
    return false;
  }

  // Get a set of all landing ranges associated with this node
  getLandingRanges() {
    const result = new Set();
    for (const landingRanges of Object.values(this.columnsAndLandingRanges)) {
      landingRanges.forEach(lr => result.add(lr));
    }
    return result;
  }

  addLandingRange(landingRange) {
    const {terrainColumn} = landingRange;
    const landingRanges = this.columnsAndLandingRanges[terrainColumn];
    if (landingRanges) {
      // No duplicate landing ranges allowed!
      for (const lr of landingRanges) {
        if (lr === landingRange) { return; }
      }
      landingRanges.push(landingRange);
    }
    else { this.columnsAndLandingRanges[terrainColumn] = [landingRange]; } 
  }

  removeLandingRange(landingRange) {
    const { terrainColumn } = landingRange;
    let landingRanges = this.columnsAndLandingRanges[terrainColumn];
    if (!landingRanges) { return; }
    landingRanges = landingRanges.filter(range => range !== landingRange);
    if (landingRanges.length === 0) {
      delete this.columnsAndLandingRanges[terrainColumn];
    }
    else {
      this.columnsAndLandingRanges[terrainColumn] = landingRanges;
    }
  }

  debugColourForLandingRanges() {
    const colour = new THREE.Color(0,0,0);
    let count = 0;
    const columnsLandingRanges = Object.values(this.columnsAndLandingRanges);
  
    for (const landingRanges of columnsLandingRanges) {
      for (const landingRange of landingRanges) {
        colour.add(landingRange.debugColour());
        count++;
      }
    }
    colour.multiplyScalar(1/Math.max(1, count));
    return colour;
  }
}

const DEFAULT_NODES_PER_TERRAIN_SQUARE_UNIT = 5;
const TRAVERSAL_UNVISITED_STATE     = 1;
const TRAVERSAL_FINISHED_STATE      = 2;

export default class RigidBodyLattice {
  constructor(terrainGroup) {
    this.terrainGroup = terrainGroup;
    this.clear();
  }

  get unitsBetweenNodes() {
    return TerrainColumn.SIZE / (this.numNodesPerUnit-1);
  }

  clear() {
    this.nodes = [];
    this.numNodesPerUnit = 0;
    this.nextNodeId = 0;
    this._clearDebugDraw();
  }

  numNodes() {
    let count = 0;
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        count += this.nodes[x][z].length;
      }
    }
    return count;
  }

  buildFromTerrain(terrain, numNodesPerUnit=DEFAULT_NODES_PER_TERRAIN_SQUARE_UNIT) {
    this.clear();

    this.numNodesPerUnit = numNodesPerUnit;

    // Nodes are built to reflect the same coordinate system as the terrain
    const numNodesX = terrain.length * numNodesPerUnit + 1 - terrain.length;
    this.nodes = new Array(numNodesX).fill(null);

    for (let x = 0; x < numNodesX; x++) {
      const nodeXPos = x*this.unitsBetweenNodes;

      // The node might be associated with more than one part of the terrain
      const currTerrainXIndices = [];
      const floorXIdx = Math.max(0,Math.floor((x-1)/(numNodesPerUnit-1)));
      const terrainZ = terrain[floorXIdx];
      currTerrainXIndices.push(floorXIdx);
      if (x % (numNodesPerUnit - 1) === 0 && x > 0 && floorXIdx+1 < terrain.length) {
        currTerrainXIndices.push(floorXIdx+1);
      }

      const numNodesZ = terrainZ.length * numNodesPerUnit + 1 - terrainZ.length;
      const nodesZ = this.nodes[x] = new Array(numNodesZ).fill(null);
      
      for (let z = 0; z < numNodesZ; z++) {
        const nodeZPos = z*this.unitsBetweenNodes;

        const currTerrainZIndices = [];
        const floorZIdx = Math.max(0, Math.floor((z - 1) / (numNodesPerUnit - 1)));
        currTerrainZIndices.push(floorZIdx);
        if (z % (numNodesPerUnit-1) === 0 && z > 0 && floorZIdx+1 < terrainZ.length) {
          currTerrainZIndices.push(floorZIdx+1);
        }

        // Get all the squares associated with the current column of nodes
        const columns = [];
        for (let i = 0; i < currTerrainXIndices.length; i++) {
          for (let j = 0; j < currTerrainZIndices.length; j++) {
            // NOTE: The terrain column might not exist if the map is uneven
            const currColumn = terrain[currTerrainXIndices[i]][currTerrainZIndices[j]];
            if (currColumn) {
              columns.push(currColumn);
            }
          }
        }
        
        // TODO: Deal with irregular (non-rectangular prism) geometry

        let maxHeight = 0;
        for (let i = 0; i < columns.length; i++) {
          const {landingRanges} = columns[i];
          maxHeight = landingRanges.length === 0 ? maxHeight : Math.max(landingRanges[landingRanges.length-1].endY, maxHeight);
        }
        const numNodesY = maxHeight * numNodesPerUnit;
        const nodesY = nodesZ[z] = new Array(numNodesY).fill(null);

        for (let y = 0; y < numNodesY; y++) {
          const nodeYPos = y*this.unitsBetweenNodes;
          const currNodePos = new THREE.Vector3(nodeXPos, nodeYPos, nodeZPos);

          const columnsAndLandingRanges = {};
          let landingRangesExist = false;
          for (const column of columns) {
            const landingRanges = column.landingRangesContainingPoint(currNodePos);
            if (landingRanges.length > 0) {
              if (!(column in columnsAndLandingRanges)) { columnsAndLandingRanges[column] = []; }
              columnsAndLandingRanges[column].push(...landingRanges);
              landingRangesExist = true;
            }
          }
          if (landingRangesExist) {
            nodesY[y] = new LatticeNode(this.nextNodeId++, x, z, y, currNodePos, columnsAndLandingRanges);
          }
        }
      }
    }

    // NOTE: No need for edges, we assume that every node is connected to all its neighbors immediate
    // orthogonal neighbours (i.e., +/- x,y,z)

    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    this.traverseGroundedNodes();
    this.debugDrawNodes(true);
  }

  _getNodeIndicesForLandingRange(landingRange, searchBoundingBox=null) {
    const { terrainColumn } = landingRange;
    const { xIndex, zIndex } = terrainColumn;
    const startY = searchBoundingBox ? Math.max(0,searchBoundingBox.min.y) : landingRange.startY;
    const endY = searchBoundingBox ? Math.max(0,searchBoundingBox.max.y) : landingRange.endY;

    const numNodesPerUnitMinusOne = (this.numNodesPerUnit - 1);
    const nodeXIdxStart = xIndex * numNodesPerUnitMinusOne;
    const nodeZIdxStart = zIndex * numNodesPerUnitMinusOne;
    return {
      nodeXIdxStart,
      nodeXIdxEnd: nodeXIdxStart + numNodesPerUnitMinusOne,
      nodeZIdxStart,
      nodeZIdxEnd: nodeZIdxStart + numNodesPerUnitMinusOne,
      nodeYIdxStart: Math.trunc(startY * numNodesPerUnitMinusOne),
      nodeYIdxEnd: Math.trunc(endY * numNodesPerUnitMinusOne)
    };
  }

  getNodesInLandingRange(landingRange) {
    const { 
      nodeXIdxStart, nodeXIdxEnd, 
      nodeZIdxStart, nodeZIdxEnd, 
      nodeYIdxStart, nodeYIdxEnd
    } = this._getNodeIndicesForLandingRange(landingRange);

    // Find all of the nodes that would be within the landing range 
    // (but might be tied to other landing ranges as well)
    const result = [];
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
          const node = this.nodes[x][z][y];
          if (node && node.hasLandingRange(landingRange)) { 
            result.push(node);
          }
        }
      }
    }

    return result;
  }

  removeNodesWithLandingRange(landingRange) {
    const {terrainColumn} = landingRange;
    // Exaustive search all nodes for the given landing range and remove that landing range
    // if the node no longer has any landing ranges, remove that node
    // Initialize the node traversal info
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node) {
            node.removeLandingRange(landingRange);
            if (node.hasNoColumnsAndLandingRanges()) {
              this.nodes[x][z][y] = null;
            }
          }
        }
      }
    }
    // The below code wasn't getting all the nodes??
    // TODO: Optimize later?
    // const nodes = this.getNodesInLandingRange(landingRange);
    // for (const node of nodes) {
    //   const {columnsAndLandingRanges} = node;
    //   if (node) {
    //     if (columnsAndLandingRanges[terrainColumn].length === 1 && 
    //         columnsAndLandingRanges[terrainColumn][0] === landingRange) {
    //       const { xIdx, zIdx, yIdx } = node;
    //       this.nodes[xIdx][zIdx][yIdx] = null;
    //     }
    //     else { node.removeLandingRange(landingRange); }
    //   }
    // }
  }

  addLandingRangeNodes(landingRange, searchBoundingBox=null, attachSides=false) {
    const {
      nodeXIdxStart, nodeXIdxEnd,
      nodeZIdxStart, nodeZIdxEnd,
      nodeYIdxStart, nodeYIdxEnd
    } = this._getNodeIndicesForLandingRange(landingRange, searchBoundingBox);

    const {terrainColumn} = landingRange;
    const allLandingRangeNodes = [];
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        const nodesY = this.nodes[x][z];
        // Make sure the slots exist for the potential nodes
        while (nodesY.length <= nodeYIdxEnd) { nodesY.push(null); }

        for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
          let node = nodesY[y];
          if (node) {
            // If the node is not the bottom most node or it is and has no pre-attached
            // columns/landing ranges then we add the landing range to it
            if (attachSides || y === nodeYIdxStart || node.hasNoColumnsAndLandingRanges()) {
              node.addLandingRange(landingRange);
            }
          }
          else {
            // Add a new node
            const nodePos = new THREE.Vector3(x,y,z);
            nodePos.multiplyScalar(this.unitsBetweenNodes);
            node = new LatticeNode(this.nextNodeId++, x, z, y, nodePos, { [terrainColumn]: [landingRange] });
            nodesY[y] = node;
          }
          allLandingRangeNodes.push(node);
        }
      }
    }

    return allLandingRangeNodes;
  }

  // Updates (adds and removes) nodes for the given landing range,
  // if the searchBoundingBox is provided, then this will search for and update
  // all nodes within that bounding box.
  updateNodesForLandingRange(landingRange, searchBoundingBox=null) {
    const {mesh, terrainColumn} = landingRange;
    const nodes = this.addLandingRangeNodes(landingRange, searchBoundingBox);

    const raycaster = new THREE.Raycaster();
    raycaster.ray.direction.set(0,1,0);

    const temp = mesh.material.side;
    mesh.material.side = THREE.DoubleSide;

    const remove = (node, landingRange) => {
      const {xIdx,zIdx,yIdx} = node;
      node.removeLandingRange(landingRange);
      if (node.hasNoColumnsAndLandingRanges()) {
        this.nodes[xIdx][zIdx][yIdx] = null;
      }
    };

    let rayPosTransform = null;
    if (mesh.parent) {
      rayPosTransform = mesh.parent.matrixWorld;
    }
    else {
      rayPosTransform = new THREE.Matrix4();
    }

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      assert(node !== null, "LatticeNode should not be null");

      let intersections = [];
      const {pos} = node;
      raycaster.ray.origin.set(pos.x,pos.y,pos.z).applyMatrix4(rayPosTransform);

      const temp = new THREE.ArrowHelper(raycaster.ray.direction, raycaster.ray.origin, 0.1);
      temp.line.material.depthFunc=THREE.AlwaysDepth;
      temp.cone.material.depthFunc = THREE.AlwaysDepth;
      terrainColumn.battlefield._scene.add(temp);

      mesh.raycast(raycaster, intersections);
      if (intersections.length === 0) {
        // If there are zero intersections then we should remove the node
        remove(node, landingRange);
      }
      else {
        intersections = intersections.filter(obj => obj.distance > 0);
        if (intersections.length > 0) { 
          // Sort the intersections by their distance in ascending order
          intersections.sort((a,b) => a.distance-b.distance);
          const {face} = intersections[0];
          // If the closest intersection is the backface of a triangle then we're still inside the
          // landing range's mesh and we should keep the node. 
          if (raycaster.ray.direction.dot(face.normal) < 0) {
            remove(node, landingRange);
          }
        }
      }
    }
    
    mesh.material.side = temp;
  }

  getNeighboursForNode(node) {
    const {xIdx, yIdx, zIdx} = node;
    // There are 6 potential neighbours...
    const neighbours = [];
    if (xIdx > 0 && this.nodes[xIdx - 1][zIdx]) {
      neighbours.push(this.nodes[xIdx - 1][zIdx][yIdx]);
    }
    if (xIdx < this.nodes.length - 1 && this.nodes[xIdx + 1][zIdx]) {
      neighbours.push(this.nodes[xIdx + 1][zIdx][yIdx]);
    }
    if (zIdx > 0 && this.nodes[xIdx][zIdx - 1]) {
      neighbours.push(this.nodes[xIdx][zIdx - 1][yIdx]);
    }
    if (zIdx < this.nodes[xIdx].length - 1 && this.nodes[xIdx][zIdx + 1]) {
      neighbours.push(this.nodes[xIdx][zIdx + 1][yIdx]);
    }
    if (yIdx > 0) {
      neighbours.push(this.nodes[xIdx][zIdx][yIdx - 1]);
    }
    if (yIdx < this.nodes[xIdx][zIdx].length - 1) {
      neighbours.push(this.nodes[xIdx][zIdx][yIdx + 1])
    }
    return neighbours;
  }

  traverseGroundedNodes() {
    const traversalInfo = {};
    const queue = [];

    // Initialize the node traversal info
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node) {
            node.grounded = false;
            traversalInfo[node.id] = { visitState: TRAVERSAL_UNVISITED_STATE };
            // Fill the queue with all the ground nodes
            if (y <= 0) { queue.push(node); }
          }
        }
      }
    }

    while (queue.length > 0) {
      const node = queue.shift();
      if (node) {
        const nodeTraversalInfo = traversalInfo[node.id];
        if (nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
          node.grounded = true;
          nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
          queue.push(...this.getNeighboursForNode(node));
        }
      }
    }
  }
  // Find islands in this lattice (i.e., isolated node regions that are not grounded),
  // optional landingRanges parameter will limit the serach to only nodes in those ranges.
  traverseIslands() {
    const traversalInfo = {};
    const islands = [];

    const depthFirstSearch = (node, islandNum, islandNodes) => {
      const nodeTraversalInfo = traversalInfo[node.id];
      nodeTraversalInfo.islandNum = islandNum;
      nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
      islandNodes.add(node);

      const neighbours = this.getNeighboursForNode(node);
      for (const neighbour of neighbours) {
        if (neighbour && traversalInfo[neighbour.id] && traversalInfo[neighbour.id].visitState === TRAVERSAL_UNVISITED_STATE) {
          depthFirstSearch(neighbour, islandNodes, islandNodes);
        }
      }
    }
    
    // Find the ungrounded nodes...
    const ungroundedNodes = new Set();
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node && !node.grounded) {
            ungroundedNodes.add(node);
          }
        }
      }
    }


    // Initialize the traversal info for all ungrounded nodes
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node && !node.grounded) {
            ungroundedNodes.add(node);
            traversalInfo[node.id] = { islandNum: -1, visitState: TRAVERSAL_UNVISITED_STATE };
          }
        }
      }
    }
    
    for (const node of ungroundedNodes) {
      if (node) {
        const nodeTraversalInfo = traversalInfo[node.id];
        if (nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
          const islandNodes = new Set();
          depthFirstSearch(node, islands.length, islandNodes);
          islands.push(islandNodes);
        }
      }
    }

    return islands;
  }

  _clearDebugDraw() {
    if (this.debugNodePoints) {
      this.terrainGroup.remove(this.debugNodePoints);
      this.debugNodePoints.geometry.dispose();
      this.debugNodePoints = null;
    }
  }
  debugDrawNodes(show=true) {
    this._clearDebugDraw();
    if (show) {
      const nodeGeometry = new THREE.BufferGeometry();
      const vertices = [];
      const colours  = [];
      for (let x = 0; x < this.nodes.length; x++) {
        for (let y = 0; y < this.nodes[x].length; y++) {
          for (let z = 0; z < this.nodes[x][y].length; z++) {
            const currNode = this.nodes[x][y][z];
            if (currNode) {
              const nodePos = currNode.pos;
              vertices.push(nodePos.x); vertices.push(nodePos.y); vertices.push(nodePos.z);
              if (!currNode.grounded) {
                colours.push(0); colours.push(0); colours.push(0); // Detached nodes are black
              }
              else {
                const c = currNode.debugColourForLandingRanges();
                colours.push(c.r); colours.push(c.g); colours.push(c.b);
              }
            }
          }
        }
      }

      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(vertices), 3));
      nodeGeometry.setAttribute('color', new THREE.BufferAttribute(new Float32Array(colours), 3));
      this.debugNodePoints = new THREE.Points(nodeGeometry, new THREE.PointsMaterial({color:0xFFFFFF, vertexColors:true, size:this.unitsBetweenNodes/5, depthFunc:THREE.AlwaysDepth}));
      this.debugNodePoints.renderOrder = Debug.NODE_DEBUG_RENDER_ORDER;
      this.terrainGroup.add(this.debugNodePoints);
    }
  }




}
