import * as THREE from 'three';
import {assert} from 'chai'

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, terrainColumns, materials) {
    assert(xIdx >= 0, "xIdx of a LatticeNode must be at least zero.");
    assert(yIdx >= 0, "yIdx of a LatticeNode must be at least zero.");
    assert(zIdx >= 0, "zIdx of a LatticeNode must be at least zero.");
    assert(terrainColumns.length <= 4, "There should never be more than 4 TerrainColumns tied to a node.");

    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.attachedTerrainCols = terrainColumns; // The size of this array should never be 
    this.materials = materials;
    this.grounded = false;
    //this.isEmpty = isEmpty; 
  }

  get density() {
    let density = 0;
    for (const material of this.materials) {
      density += material.density;
    }
    return density;
  }

  hasTerrainColumn(terrainCol) {
    return this.attachedTerrainCols.indexOf(terrainCol) !== -1;
  }

  addTerrainColumn(terrainCol) {
    if (this.hasTerrainColumn(terrainCol)) { return; }
    this.attachedTerrainCols.push(terrainCol);
    assert(this.attachedTerrainCols.length <= 4, "There should never be more than 4 TerrainColumns tied to a node.");
  }

  removeTerrainColumn(terrainCol) {
    const index = this.attachedTerrainCols.indexOf(terrainCol);
    this.attachedTerrainCols.splice(index, 1);
    //this.materials.splice(index, 1);
  }
  clearTerrainColumns() {
    this.attachedTerrainCols = [];
  }

  debugColour() {
    const colour = new THREE.Color(0,0,0);
    for (const terrainCol of this.attachedTerrainCols) {
      colour.add(terrainCol.debugColour());
    }
    assert(this.attachedTerrainCols.length > 0, "There shouldn't be a node in existance that has no associated TerrainColumns.");
    colour.multiplyScalar(1/Math.max(1, this.attachedTerrainCols.length));
    return colour;
  }
}

const TRAVERSAL_UNVISITED_STATE     = 1;
const TRAVERSAL_FINISHED_STATE      = 2;

export default class RigidBodyLattice {
  constructor(terrainGroup) {
    this.terrainGroup = terrainGroup;
    this.numNodesPerUnit = 5;
    this.clear();
  }

  get unitsBetweenNodes() {
    return TerrainColumn.SIZE / (this.numNodesPerUnit-1);
  }

  clear() {
    this.nodes = [];
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

  _getIndexRangeForBoundingBox(boundingBox) {
    const {min,max} = boundingBox;
    const {clamp} = THREE.MathUtils;
    return {
      nodeXIdxStart: clamp(this._unitsToNodeIndex(min.x), 0, this.nodes.length-1),
      nodeXIdxEnd: clamp(this._unitsToNodeIndex(max.x), 0, this.nodes.length-1),
      nodeYIdxStart: Math.max(0, this._unitsToNodeIndex(min.y)),
      nodeYIdxEnd: Math.max(0, this._unitsToNodeIndex(max.y)),
      nodeZIdxStart: Math.max(0, this._unitsToNodeIndex(min.z)),
      nodeZIdxEnd: Math.max(0, this._unitsToNodeIndex(max.z))
    };
  }
  _getXZIndexRangeForTerrainColumn(terrainColumn) {
    const { xIndex, zIndex } = terrainColumn;
    const numNodesPerUnitMinusOne = (this.numNodesPerUnit - 1);
    const nodeXIdxStart = xIndex * numNodesPerUnitMinusOne;
    const nodeZIdxStart = zIndex * numNodesPerUnitMinusOne;
    return {
      nodeXIdxStart,
      nodeXIdxEnd: nodeXIdxStart + numNodesPerUnitMinusOne,
      nodeZIdxStart,
      nodeZIdxEnd: nodeZIdxStart + numNodesPerUnitMinusOne,
    };
  }
  _unitsToNodeIndex(unitVal) {
    return Math.floor(unitVal * (this.numNodesPerUnit - 1));
  }
  _nodeIndexToPosition(xIdx, yIdx, zIdx) {
    const nodePos = new THREE.Vector3(xIdx,yIdx,zIdx);
    nodePos.multiplyScalar(this.unitsBetweenNodes);
    return nodePos;
  }
  _removeNode(xIdx, zIdx, yIdx) {
    this.nodes[xIdx][zIdx][yIdx] = null;
  }

  addTerrainColumnBox(terrainColumn, config) {
    const {startY, endY, material} = config;
    const {nodeXIdxStart, nodeXIdxEnd, nodeZIdxStart, nodeZIdxEnd} = this._getXZIndexRangeForTerrainColumn(terrainColumn);
    const nodeYIdxStart = this._unitsToNodeIndex(startY);
    const nodeYIdxEnd = this._unitsToNodeIndex(endY);

    while (this.nodes.length <= nodeXIdxEnd) { this.nodes.push([]); }
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      const nodesX = this.nodes[x];
      while (nodesX.length <= nodeZIdxEnd) { nodesX.push([]); }

      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        const nodesZ = nodesX[z];
        while (nodesZ.length <= nodeYIdxEnd) { nodesZ.push(null); }
        for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
          const node = nodesZ[y];
          if (node) {
            node.addTerrainColumn(terrainColumn);
            //node.isEmpty = false;
          }
          else {
            nodesZ[y] = new LatticeNode(this.nextNodeId++, x, z, y, this._nodeIndexToPosition(x,y,z), [terrainColumn], [material]);
          }
        }
      }
    }
  }

  removeTerrainColumnFromNodes(terrainColumn, nodeSet) {
    for (const node of nodeSet) {
      node.removeTerrainColumn(terrainColumn);
      if (node.attachedTerrainCols.length === 0) {
        const {xIdx, zIdx, yIdx} = node;
        this._removeNode(xIdx, zIdx, yIdx);
      }
    }
  }

  /*
  getNodesInTerrainColumn(terrainColumn) {
    const {nodeXIdxStart, nodeXIdxEnd, nodeZIdxStart, nodeZIdxEnd} = this._getXZIndexRangeForTerrainColumn(terrainColumn);
    const result = [];
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        const nodesXZ = this.nodes[x][z];
        for (let y = 0; y < nodesXZ.length; y++) {
          const node = nodesXZ[y];
          if (node) { 
            assert(node.hasTerrainColumn(terrainColumn));
            result.push(node);
          }
        }
      }
    }
    return result;
  }
  */

  _getNodeCubePositions(xIdx, yIdx, zIdx) {
    return {
      xyzPt    : this._nodeIndexToPosition(xIdx,yIdx,zIdx),
      x1yzPt   : this._nodeIndexToPosition(xIdx+1,yIdx,zIdx),
      x1yz1Pt  : this._nodeIndexToPosition(xIdx+1,yIdx,zIdx+1),
      xyz1Pt   : this._nodeIndexToPosition(xIdx,yIdx,zIdx+1),
      xy1zPt   : this._nodeIndexToPosition(xIdx,yIdx+1,zIdx),
      x1y1zPt  : this._nodeIndexToPosition(xIdx+1,yIdx+1,zIdx),
      x1y1z1Pt : this._nodeIndexToPosition(xIdx+1,yIdx+1,zIdx+1),
      xy1z1Pt  : this._nodeIndexToPosition(xIdx,yIdx+1,zIdx+1),
    };
  }
  _makeNodeCubeCell(xIdx, yIdx, zIdx) {
    const {xyzPt, x1yzPt, x1yz1Pt, xyz1Pt, xy1zPt, x1y1zPt, x1y1z1Pt, xy1z1Pt} = this._getNodeCubePositions(xIdx,yIdx,zIdx);
    const xPlus1 = xIdx+1, yPlus1 = yIdx+1, zPlus1 = zIdx+1;

    const xOutside = (xIdx < 0 || xIdx >= this.nodes.length);
    const xPlus1Outside = (xPlus1 >= this.nodes.length);
    const zOutside = (zIdx < 0);
    const yOutside = (yIdx < 0);

    const yOutsideNode =  yIdx < 0 ? {} : null;

    const n0 = (xOutside || zOutside || zIdx >= this.nodes[xIdx].length || yOutside || yIdx >= this.nodes[xIdx][zIdx].length) ?
      {node: yOutsideNode, pos: xyzPt} : {node: this.nodes[xIdx][zIdx][yIdx], pos: xyzPt};
    const n1 = (xPlus1Outside || zOutside || zIdx >= this.nodes[xPlus1].length || yOutside || yIdx >= this.nodes[xPlus1][zIdx].length) ? 
      {node: yOutsideNode, pos: x1yzPt} : {node: this.nodes[xPlus1][zIdx][yIdx], pos: x1yzPt};
    const n2 = (xPlus1Outside || zPlus1 >= this.nodes[xPlus1].length || yOutside || yIdx >= this.nodes[xPlus1][zPlus1].length) ? 
      {node: yOutsideNode, pos: x1yz1Pt} : {node: this.nodes[xPlus1][zPlus1][yIdx], pos: x1yz1Pt};
    const n3 = (xOutside || zPlus1 >= this.nodes[xIdx].length || yOutside || yIdx >= this.nodes[xIdx][zPlus1].length) ? 
      {node: yOutsideNode, pos: xyz1Pt} : {node: this.nodes[xIdx][zPlus1][yIdx], pos: xyz1Pt};
    const n4 = (xOutside || zOutside || zIdx >= this.nodes[xIdx].length || yPlus1 >= this.nodes[xIdx][zIdx].length) ? 
      {node: null, pos: xy1zPt} : {node: this.nodes[xIdx][zIdx][yPlus1], pos: xy1zPt};
    const n5 = (xPlus1Outside || zOutside || zIdx >= this.nodes[xPlus1].length || yPlus1 >= this.nodes[xPlus1][zIdx].length) ? 
      {node: null, pos: x1y1zPt} : {node: this.nodes[xPlus1][zIdx][yPlus1], pos: x1y1zPt};
    const n6 = (xPlus1Outside || zPlus1 >= this.nodes[xPlus1].length || yPlus1 >= this.nodes[xPlus1][zPlus1].length) ? 
      {node: null, pos: x1y1z1Pt} : {node: this.nodes[xPlus1][zPlus1][yPlus1], pos: x1y1z1Pt};
    const n7 = (xOutside || zPlus1 >= this.nodes[xIdx].length || yPlus1 >= this.nodes[xIdx][zPlus1].length) ? 
      {node: null, pos: xy1z1Pt} : {node: this.nodes[xIdx][zPlus1][yPlus1], pos: xy1z1Pt};

    return [n0,n1,n2,n3,n4,n5,n6,n7];
  }

  getTerrainColumnCubeCells(terrainColumn) {
    const {nodeXIdxStart, nodeXIdxEnd, nodeZIdxStart, nodeZIdxEnd} = this._getXZIndexRangeForTerrainColumn(terrainColumn);
    const cubeCells = [];

    let maxYIdx = -2;
    for (let x = nodeXIdxStart-1; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart-1; z <= nodeZIdxEnd; z++) {
        if (this.nodes[x] && this.nodes[x][z]) {
          maxYIdx = Math.max(maxYIdx, this.nodes[x][z].length-1);
        }
      }
    }

    // We form cube cells for everything inside the terrain column
    for (let x = nodeXIdxStart-1; x <= nodeXIdxEnd; x++) {
      let isXBoundary = (x === nodeXIdxStart || x === nodeXIdxEnd);
      for (let z = nodeZIdxStart-1; z <= nodeZIdxEnd; z++) {
        const isBoundary = (z === nodeZIdxStart || z === nodeZIdxEnd) || isXBoundary;
        for (let y = -1; y <= maxYIdx; y++) {

          cubeCells.push(this._makeNodeCubeCell(x,y,z));
        }
      }
    }
    return cubeCells;
  }

  getNodeCubeCells(nodes) {
    const cubeCells = [];
    const positionsWithCubeCells = {};

    const positionHash = (x,y,z) => { return `${x},${y},${z}`; };
    // There are a total of 27 points to consider for each node: all the surrounding nodes and the center node
    const sampleIndices = [
      [-1, -1, -1], [-1, -1, 0], [-1, -1, 1], [-1, 0, -1], [-1, 0, 0 ], [-1, 0, 1 ], [-1, 1, -1 ], [-1, 1, 0 ], [-1, 1, 1 ],
      [0, -1, -1],  [ 0, -1, 0 ], [ 0, -1, 1 ], [ 0, 0, -1 ], [ 0, 0, 0 ], [ 0, 0, 1 ], [ 0, 1, -1 ], [ 0, 1, 0 ], [ 0, 1, 1 ], 
      [ 1, -1, -1 ], [ 1, -1, 0 ], [ 1, -1, 1 ], [ 1, 0, -1 ], [ 1, 0, 0 ], [ 1, 0, 1 ], [ 1, 1, -1 ], [ 1, 1, 0 ], [ 1, 1, 1 ]
    ];

    for (const node of nodes) {
      assert(node !== null, "Nodes given to getNodeCubeCells should not be null.");
      const {xIdx, zIdx, yIdx} = node;
      for (const indexInc of sampleIndices) {
        const currXIdx = xIdx + indexInc[0], currYIdx = yIdx + indexInc[1], currZIdx = zIdx + indexInc[2];
        const hash = positionHash(currXIdx, currYIdx, currZIdx);
        if (!positionsWithCubeCells[hash]) {
          cubeCells.push(this._makeNodeCubeCell(currXIdx, currYIdx, currZIdx));
          positionsWithCubeCells[hash] = true;
        }
      }
    }
    return cubeCells;
  }

  removeNodesInsideShape(shape) {
    // Get all possible nodes in the AABB of the shape...
    const boundingBox = new THREE.Box3();
    shape.getBoundingBox(boundingBox);
    const {nodeXIdxStart, nodeXIdxEnd, nodeYIdxStart, nodeYIdxEnd, nodeZIdxStart, nodeZIdxEnd} = this._getIndexRangeForBoundingBox(boundingBox);
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd && x < this.nodes.length; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd && z < this.nodes[x].length; z++) {
        for (let y = nodeYIdxStart; y <= nodeYIdxEnd && y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node && shape.containsPoint(node.pos)) {
            this._removeNode(x,z,y);
          }
        }
      }
    }
  }
  removeNodes(nodes) {
    for (const node of nodes) {
      const {xIdx, zIdx, yIdx} = node;
      this._removeNode(xIdx, zIdx, yIdx);
    }
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
          const neighbours = this.getNeighboursForNode(node);
          
          // TODO: If there are no neighbours, then the node is stranded and it should be removed.

          queue.push(...neighbours);
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
      const neighbours = this.getNeighboursForNode(node);
      // TODO: If there are no neighbours, then the node is stranded and it should be removed.

      const nodeTraversalInfo = traversalInfo[node.id];
      nodeTraversalInfo.islandNum = islandNum;
      nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
      islandNodes.add(node);

      for (const neighbour of neighbours) {
        if (neighbour && traversalInfo[neighbour.id] && traversalInfo[neighbour.id].visitState === TRAVERSAL_UNVISITED_STATE) {
          depthFirstSearch(neighbour, islandNodes, islandNodes);
        }
      }
    }
    
    // Initialize the traversal info for all ungrounded nodes
    const ungroundedNodes = new Set();
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
      const nodeTraversalInfo = traversalInfo[node.id];
      if (nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
        const islandNodes = new Set();
        depthFirstSearch(node, islands.length, islandNodes);
        if (islandNodes.size > 0 && !islandNodes.values().next().value.grounded) {
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
                const c = currNode.debugColour();
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
