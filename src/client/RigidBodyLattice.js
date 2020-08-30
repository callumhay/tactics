import * as THREE from 'three';
import {assert} from 'chai'

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';
import Battlefield from './Battlefield';

const tempVec3 = new THREE.Vector3();

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, terrainColumn, material) {
    assert(xIdx >= 0, "xIdx of a LatticeNode must be at least zero.");
    assert(yIdx >= 0, "yIdx of a LatticeNode must be at least zero.");
    assert(zIdx >= 0, "zIdx of a LatticeNode must be at least zero.");

    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.terrainColumn = terrainColumn || null;
    this.material = material;
    this.grounded = false;
  }

  get density() {
    return this.material.density;
  }

  debugColour() {
    return this.terrainColumn ? this.terrainColumn.debugColour() : new THREE.Color(0,0,0);
  }
}

const TRAVERSAL_UNVISITED_STATE     = 1;
const TRAVERSAL_FINISHED_STATE      = 2;

export default class RigidBodyLattice {
  static get DEFAULT_NUM_NODES_PER_UNIT() { return 5; }

  constructor(terrainGroup) {
    this.terrainGroup = terrainGroup;
    this.clear();
  }

  get numNodesPerUnit() {
    return RigidBodyLattice.DEFAULT_NUM_NODES_PER_UNIT;
  }
  get unitsBetweenNodes() {
    return TerrainColumn.SIZE / this.numNodesPerUnit;
  }
  get halfUnitsBetweenNodes() {
    return this.unitsBetweenNodes / 2;
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
    const nodeXIdxStart = this._unitsToNodeIndex(xIndex * TerrainColumn.SIZE);
    const nodeZIdxStart = this._unitsToNodeIndex(zIndex * TerrainColumn.SIZE);
    return {
      nodeXIdxStart,
      nodeXIdxEnd: nodeXIdxStart + numNodesPerUnitMinusOne,
      nodeZIdxStart,
      nodeZIdxEnd: nodeZIdxStart + numNodesPerUnitMinusOne,
    };
  }
  _unitsToNodeIndex(unitVal) {
    return Math.floor(unitVal * this.numNodesPerUnit);
  }
  _nodeIndexToPosition(xIdx, yIdx, zIdx) {
    const nodePos = new THREE.Vector3(xIdx,yIdx,zIdx);
    nodePos.multiplyScalar(this.unitsBetweenNodes);
    nodePos.addScalar(this.halfUnitsBetweenNodes);
    return nodePos;
  }
  _getNode(xIdx, zIdx, yIdx) {
    if (this.nodes[xIdx] && this.nodes[xIdx][zIdx]) {
      return this.nodes[xIdx][zIdx][yIdx] || null;
    }
    return null;
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
            assert(false, "Nodes shouldn't have more than one TerrainColumn associated with them.");
            node.terrainColumn = terrainColumn;
          }
          else {
            nodesZ[y] = new LatticeNode(this.nextNodeId++, x, z, y, this._nodeIndexToPosition(x,y,z), terrainColumn, material);
          }
        }
      }
    }
  }

  addTerrainColumnDebris(terrainColumn, debris) {
    const {mesh, material} = debris;
    const {geometry} = mesh;
    const {boundingBox} = geometry;
    assert(boundingBox, "The bounding box for the geometry should already be generated.");
    //terrainColumn.battlefield.terrainGroup.add(Debug.buildDebugBoundingBoxMesh(boundingBox));
    
    // Create a node-index bounding box for the terrain column
    const {nodeXIdxStart, nodeXIdxEnd, nodeZIdxStart, nodeZIdxEnd} = this._getXZIndexRangeForTerrainColumn(terrainColumn);
    const nodeYIdxStart = 0, nodeYIdxEnd = this._unitsToNodeIndex(Battlefield.MAX_HEIGHT);
    const tcBoundingBox = new THREE.Box3(
      new THREE.Vector3(nodeXIdxStart, nodeYIdxStart, nodeZIdxStart), new THREE.Vector3(nodeXIdxEnd, nodeYIdxEnd, nodeZIdxEnd)
    );
    // Convert the geometry bounding box into a node-index based box
    const geomBoundingBox = boundingBox.clone();
    {
      const {min,max} = geomBoundingBox;
      min.set(this._unitsToNodeIndex(min.x), this._unitsToNodeIndex(min.y), this._unitsToNodeIndex(min.z));
      max.set(this._unitsToNodeIndex(max.x), this._unitsToNodeIndex(max.y), this._unitsToNodeIndex(max.z));
    }

    const raycaster = new THREE.Raycaster();
    raycaster.ray.direction.set(0,1,0);
    let rayPosTransform = null;
    if (mesh.parent) { rayPosTransform = mesh.parent.matrixWorld;}
    else { rayPosTransform = new THREE.Matrix4(); }

    // In order for the raycaster to collide with interior faces of the mesh, we need to 
    // temporarily set the material to be double-sided
    const temp = mesh.material.side;
    mesh.material.side = THREE.DoubleSide;

    // Go through all the nodes in the bounding box where the geometry and the terrainColumn intersect
    // Check whether each node is inside the geometry, if it is then add it
    tcBoundingBox.intersect(geomBoundingBox);
    const {min, max} = tcBoundingBox;

    while (this.nodes.length <= max.x) { this.nodes.push([]); }
    for (let x = min.x; x <= max.x; x++) {
      const nodesX = this.nodes[x];
      while (nodesX.length <= max.z) { nodesX.push([]); }
      for (let z = min.z; z <= max.z; z++) {
        const nodesXZ = this.nodes[x][z];
        while (nodesXZ.length <= max.y) { nodesXZ.push(null); }
        for (let y = min.y; y <= max.y; y++) {
          const nodePos = this._nodeIndexToPosition(x,y,z);
          let intersections = [];
          raycaster.ray.origin.set(nodePos.x, nodePos.y, nodePos.z).applyMatrix4(rayPosTransform);

          //const temp = new THREE.ArrowHelper(raycaster.ray.direction, raycaster.ray.origin, 0.1);
          //temp.line.material.depthFunc=THREE.AlwaysDepth;
          //temp.cone.material.depthFunc = THREE.AlwaysDepth;
          //terrainColumn.battlefield._scene.add(temp);
    
          mesh.raycast(raycaster, intersections);
          if (intersections.length > 0) {
            nodesXZ[y] = new LatticeNode(this.nextNodeId++, x, z, y, nodePos, terrainColumn, material);
          }
        }
      }
    }

    mesh.material.side = temp;
  }

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

  static _makeNodeCubeCellFromNodeArray(xIdx, yIdx, zIdx, nodeArray, positionFunc) {
    const {xyzPt, x1yzPt, x1yz1Pt, xyz1Pt, xy1zPt, x1y1zPt, x1y1z1Pt, xy1z1Pt} = positionFunc(xIdx,yIdx,zIdx);
    const xPlus1 = xIdx+1, yPlus1 = yIdx+1, zPlus1 = zIdx+1;
    const xOutside = (xIdx < 0 || xIdx >= nodeArray.length);
    const xPlus1Outside = (xPlus1 >= nodeArray.length);
    const zOutside = (zIdx < 0);
    const yOutside = (yIdx < 0);

    const n4 = (xOutside || zOutside || zIdx >= nodeArray[xIdx].length || yPlus1 >= nodeArray[xIdx][zIdx].length) ? 
      {node: null, pos: xy1zPt} : {node: nodeArray[xIdx][zIdx][yPlus1], pos: xy1zPt};
    const n5 = (xPlus1Outside || zOutside || zIdx >= nodeArray[xPlus1].length || yPlus1 >= nodeArray[xPlus1][zIdx].length) ? 
      {node: null, pos: x1y1zPt} : {node: nodeArray[xPlus1][zIdx][yPlus1], pos: x1y1zPt};
    const n6 = (xPlus1Outside || zPlus1 >= nodeArray[xPlus1].length || yPlus1 >= nodeArray[xPlus1][zPlus1].length) ? 
      {node: null, pos: x1y1z1Pt} : {node: nodeArray[xPlus1][zPlus1][yPlus1], pos: x1y1z1Pt};
    const n7 = (xOutside || zPlus1 >= nodeArray[xIdx].length || yPlus1 >= nodeArray[xIdx][zPlus1].length) ? 
      {node: null, pos: xy1z1Pt} : {node: nodeArray[xIdx][zPlus1][yPlus1], pos: xy1z1Pt};

    const n0 = yOutside ? {...n4, pos: xyzPt} : (xOutside || zOutside || zIdx >= nodeArray[xIdx].length || yOutside || yIdx >= nodeArray[xIdx][zIdx].length) ?
      { node: null, pos: xyzPt } : { node: nodeArray[xIdx][zIdx][yIdx], pos: xyzPt };
    const n1 = yOutside ? {...n5, pos: x1yzPt} : (xPlus1Outside || zOutside || zIdx >= nodeArray[xPlus1].length || yOutside || yIdx >= nodeArray[xPlus1][zIdx].length) ?
      { node: null, pos: x1yzPt } : { node: nodeArray[xPlus1][zIdx][yIdx], pos: x1yzPt };
    const n2 = yOutside ? {...n6, pos: x1yz1Pt} : (xPlus1Outside || zPlus1 >= nodeArray[xPlus1].length || yOutside || yIdx >= nodeArray[xPlus1][zPlus1].length) ?
      { node: null, pos: x1yz1Pt } : { node: nodeArray[xPlus1][zPlus1][yIdx], pos: x1yz1Pt };
    const n3 = yOutside ? {...n7, pos: xyz1Pt} : (xOutside || zPlus1 >= nodeArray[xIdx].length || yOutside || yIdx >= nodeArray[xIdx][zPlus1].length) ?
      { node: null, pos: xyz1Pt } : { node: nodeArray[xIdx][zPlus1][yIdx], pos: xyz1Pt };

    return {id:`${xIdx},${yIdx},${zIdx}`, corners: [n0,n1,n2,n3,n4,n5,n6,n7]};
  }

  _makeNodeCubeCell(xIdx, yIdx, zIdx) {
    return RigidBodyLattice._makeNodeCubeCellFromNodeArray(xIdx, yIdx, zIdx, this.nodes, this._getNodeCubePositions.bind(this));
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
      for (let z = nodeZIdxStart-1; z <= nodeZIdxEnd; z++) {
        for (let y = -1; y <= maxYIdx; y++) {
          cubeCells.push(this._makeNodeCubeCell(x,y,z));
        }
      }
    }
    return cubeCells;
  }

  getNodeIslandCubeCells(nodes) {
    const cubeCells = [];

    // Find the index bounding box that includes all the given nodes
    const bbMin = new THREE.Vector3(Infinity, Infinity, Infinity);
    const bbMax = new THREE.Vector3(-Infinity, -Infinity, -Infinity);
    for (const node of nodes) {
      const {xIdx, yIdx, zIdx} = node;
      tempVec3.set(xIdx,yIdx,zIdx);
      bbMin.min(tempVec3);
      bbMax.max(tempVec3);
    }
    
    // Make the box wider on each side by 1 (so that we capture the cubes making up the outer surface)
    tempVec3.set(1,1,1);
    bbMin.sub(tempVec3);
    bbMax.add(tempVec3);

    // Create a new, isolated array of nodes with its own local index space at the origin
    // and a mapping of -bbMin to get from this.nodes space to it
    const nodeBoundingBox = new THREE.Box3(bbMin, bbMax);
    nodeBoundingBox.getSize(tempVec3);

    const isolatedNodes = new Array(tempVec3.x).fill(null);
    for (let x = 0; x < isolatedNodes.length; x++) {
      const zNodes = new Array(tempVec3.z).fill(null);
      isolatedNodes[x] = zNodes;
      for (let z = 0; z < zNodes.length; z++) {
        const yNodes = new Array(tempVec3.y).fill(null);
        isolatedNodes[x][z] = yNodes;
      }
    }
    // Map each of the nodes into the isolatedNodes array
    for (const node of nodes) {
      const {xIdx, zIdx, yIdx} = node;
      isolatedNodes[xIdx-bbMin.x][zIdx-bbMin.z][yIdx-bbMin.y] = node;
    }

    const isolatedNodePositionFunc = ((xIdx, yIdx, zIdx) => {
      return this._getNodeCubePositions(xIdx+bbMin.x, yIdx+bbMin.y, zIdx+bbMin.z);
    }).bind(this);

    // We can now use the isolated nodes array to generate all our cube cells
    for (let x = 0; x < isolatedNodes.length; x++) {
      const zNodes = isolatedNodes[x];
      for (let z = 0; z < zNodes.length; z++) {
        const yNodes = zNodes[z];
        for (let y = 0; y < yNodes.length; y++) {
          cubeCells.push(RigidBodyLattice._makeNodeCubeCellFromNodeArray(x, y, z, isolatedNodes, isolatedNodePositionFunc));
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
        if (nodeTraversalInfo && nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
          node.grounded = true;
          nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
          const neighbours = this.getNeighboursForNode(node).filter(n => n !== null);
          
          // If there are almost no neighbours, then the node is stranded and it should be removed.
          if (neighbours.length <= 2) {
            const {xIdx, zIdx, yIdx} = node;
            delete traversalInfo[node.id];
            this._removeNode(xIdx, zIdx, yIdx);
          }
          else {
            queue.push(...neighbours);
          }
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
      const neighbours = this.getNeighboursForNode(node).filter(n => n !== null);
      
      // If there are too few neighbours, then the node is stranded and it should be removed.
      if (neighbours.length <= 2) {
        const {xIdx, zIdx, yIdx} = node;
        delete traversalInfo[node.id];
        this._removeNode(xIdx, zIdx, yIdx);
      }
      else {
        const nodeTraversalInfo = traversalInfo[node.id];
        nodeTraversalInfo.islandNum = islandNum;
        nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
        islandNodes.add(node);
      }

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
      if (nodeTraversalInfo && nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
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
