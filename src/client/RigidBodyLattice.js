import * as THREE from 'three';

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, columnsAndLandingRanges) {
    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.columnsAndLandingRanges = columnsAndLandingRanges;
    this.grounded = false;
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
        if (!landingRange) {
          console.log("HERE");
        }
        colour.add(landingRange.debugColour());
        count++;
      }
    }
    colour.multiplyScalar(1/Math.max(1, count));
    return colour;
  }
}

const DEFAULT_NODES_PER_TERRAIN_SQUARE_UNIT = 5;
const TRAVERSAL_UNVISITED_STATE     = 0;
const TRAVERSAL_FINISHED_STATE      = 1;

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
          maxHeight = Math.max(landingRanges[landingRanges.length-1].endY, maxHeight);
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

  _getNodeIndicesForLandingRange(landingRange) {
    const { terrainColumn, startY, endY } = landingRange;
    const { xIndex, zIndex } = terrainColumn;
    const numNodesPerUnitMinusOne = (this.numNodesPerUnit - 1);
    const nodeXIdxStart = xIndex * numNodesPerUnitMinusOne;
    const nodeZIdxStart = zIndex * numNodesPerUnitMinusOne;
    return {
      nodeXIdxStart,
      nodeXIdxEnd: nodeXIdxStart + numNodesPerUnitMinusOne,
      nodeZIdxStart,
      nodeZIdxEnd: nodeZIdxStart + numNodesPerUnitMinusOne,
      nodeYIdxStart: Math.floor(startY * numNodesPerUnitMinusOne),
      nodeYIdxEnd: Math.floor(endY * numNodesPerUnitMinusOne)
    };
  }

  getNodesInLandingRange(landingRange) {
    const { 
      nodeXIdxStart, nodeXIdxEnd, 
      nodeZIdxStart, nodeZIdxEnd, 
      nodeYIdxStart, nodeYIdxEnd
    } = this._getNodeIndicesForLandingRange(landingRange);

    // Find all of the nodes that would be within the landing range (but might be tied to other landing ranges as well)
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

  removeNodesInsideLandingRange(landingRange) {
    const {terrainColumn} = landingRange;
    const nodes = this.getNodesInLandingRange(landingRange);
    nodes.forEach(node => {
      // If the node ONLY contains the given landing range then we remove it
      const {columnsAndLandingRanges} = node;
      if (node && columnsAndLandingRanges[terrainColumn].length === 1 && 
        columnsAndLandingRanges[terrainColumn][0] === landingRange) {
        const {xIdx, zIdx, yIdx} = node;
        this.nodes[xIdx][zIdx][yIdx] = null;
      }
    });
  }

  addLandingRangeNodes(landingRange, attachSides=false) {
    const {
      nodeXIdxStart, nodeXIdxEnd,
      nodeZIdxStart, nodeZIdxEnd,
      nodeYIdxStart, nodeYIdxEnd
    } = this._getNodeIndicesForLandingRange(landingRange);
    const {terrainColumn} = landingRange;

    const allLandingRangeNodes = [];
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        const nodesY = this.nodes[x][z];
        // Make sure the nodes exist, if they don't then add them
        while (nodesY.length <= nodeYIdxEnd) { nodesY.push(null); }

        for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
          let node = nodesY[y];
          if (node) {
            const { columnsAndLandingRanges } = node;
            // If the node is not the bottom most node or it is and has no pre-attached
            // columns/landing ranges then we add the landing range to it
            if (attachSides || y === nodeYIdxStart || Object.keys(columnsAndLandingRanges).length === 0) {
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

  updateNodesForLandingRange(landingRange, epsilon=TerrainColumn.EPSILON) {
    const {mesh} = landingRange;
    const nodes = this.addLandingRangeNodes(landingRange);
    const raycaster = new THREE.Raycaster();
    raycaster.near = 0;
    raycaster.far = landingRange.height;
    const rayPos = new THREE.Vector3();
    const rayDir = new THREE.Vector3(0,1,0);
    
    const temp = mesh.material.side;
    mesh.material.side = THREE.DoubleSide;

    const remove = (node, landingRange) => {
      const {xIdx,zIdx,yIdx} = node;
      node.removeLandingRange(landingRange);
      if (Object.keys(node.columnsAndLandingRanges).length === 0) {
        this.nodes[xIdx][zIdx][yIdx] = null;
      }
    };

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      let intersections = [];
      const {pos} = node;
      rayPos.set(pos.x, pos.y, pos.z);
      raycaster.set(rayPos, rayDir);
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
          if (rayDir.dot(face.normal) < 0) {
            // Not inside the mesh - remove the current landing range from the node,
            // if there are no more landing ranges left then the node is no longer
            // attached to anything and should be removed
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
  traverseIslands() {
    /*
    const traversalInfo = {};
    const islands = [];
    
    // Grab all ungrounded nodes...
    const ungroundedNodes = [];
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node && !node.grounded) {
            ungroundedNodes.push(node);
            traversalInfo[node.id] = { islandNum: -1 };
          }
        }
      }
    }

    let currIslandNum = 0;
    while (ungroundedNodes.length > 0) {
      const node = ungroundedNodes.shift();
      if (node) {
        const nodeTraversalInfo = traversalInfo[node.id];
        if (nodeTraversalInfo.islandNum === -1) {
          const neighbours = this.getNeighboursForNode(node);
          for (const neighbour of neighbours) {

          }


        }
      }
    }
    */

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
