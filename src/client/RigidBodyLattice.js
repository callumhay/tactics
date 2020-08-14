import * as THREE from 'three';

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';

class LatticeNode {
  constructor(id, pos, terrainColumns) {
    this.id = id;
    this.pos = pos;
    this.terrainColumns = terrainColumns;
  }
}

/*
class LatticeEdge {
  constructor(n0, n1) {
    this.nodes = [n0, n1];
    this.line = new THREE.Line3(n0.pos, n1.pos);
  }

  connects(nodeA, nodeB) {
    return (this.nodes[0] === nodeA && this.nodes[1] === nodeB || this.nodes[0] === nodeB && this.nodes[1] === nodeA);
  }
}
*/

const DEFAULT_NODES_PER_TERRAIN_SQUARE_UNIT = 5;
const TRAVERSAL_UNVISITED_STATE     = 0;
const TRAVERSAL_BEING_VISITED_STATE = 1;
const TRAVERSAL_FINISHED_STATE      = 2;


export default class RigidBodyLattice {
  constructor(terrainGroup) {
    this.terrainGroup = terrainGroup;
    this.clear();
  }

  clear() {
    this.nodes = [];
    this.edges = [];
    this.unitsBetweenNodes = 0;
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

    this.unitsBetweenNodes = TerrainColumn.SIZE / (numNodesPerUnit-1);
     
    // Nodes are built to reflect the same coordinate system as the terrain
    const numNodesX = terrain.length * numNodesPerUnit + 1 - terrain.length;
    this.nodes = new Array(numNodesX).fill(null);

    let currNodeId = 0;

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
          const columnsContainingNode = columns.filter(column => column.containsPoint(currNodePos));
          if (columnsContainingNode.length > 0) {
            nodesY[y] = new LatticeNode(currNodeId++, currNodePos, columnsContainingNode);
          }
        }
      }
    }

    // NOTE: No need for edges, we assume that every node is connected to all its neighbors immediate
    // orthogonal neighbours (i.e., +/- x,y,z)


    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    const traversalInfo = this.traverseToGround();
    this.debugDrawNodes(true, traversalInfo);
  }

  traverseToGround() {
    const traversalInfo = {};

    const isConnectedToGround = (node, xIdx, zIdx, yIdx) => {
      const nodeTraversalInfo = traversalInfo[node.id];

      // Check to see if the node is grounded, if so then return immediately
      if (nodeTraversalInfo.grounded) {
        nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
        return true;
      }

      nodeTraversalInfo.visitState = TRAVERSAL_BEING_VISITED_STATE;

      // There are 6 potential neighbours...
      const neighbours = [];
      if (xIdx > 0 && this.nodes[xIdx-1][zIdx]) {
        neighbours.push([this.nodes[xIdx-1][zIdx][yIdx], xIdx-1, zIdx, yIdx]);
      }
      if (xIdx < this.nodes.length-1 && this.nodes[xIdx+1][zIdx]) {
        neighbours.push([this.nodes[xIdx+1][zIdx][yIdx], xIdx+1, zIdx, yIdx]);
      }
      if (zIdx > 0 && this.nodes[xIdx][zIdx-1]){
        neighbours.push([this.nodes[xIdx][zIdx-1][yIdx], xIdx, zIdx-1, yIdx]);
      }
      if (zIdx < this.nodes[xIdx].length-1 && this.nodes[xIdx][zIdx+1]) {
        neighbours.push([this.nodes[xIdx][zIdx+1][yIdx], xIdx, zIdx+1, yIdx]);
      }
      if (yIdx > 0) {
        neighbours.push([this.nodes[xIdx][zIdx][yIdx-1], xIdx, zIdx, yIdx-1]);
      }
      if (yIdx < this.nodes[xIdx][zIdx].length-1) {
        neighbours.push([this.nodes[xIdx][zIdx][yIdx+1], xIdx, zIdx, yIdx+1])
      }

      let grounded = false;
      for (let i = 0; i < neighbours.length; i++) {
        const [neighbourNode, neighbourXIdx, neighbourYIdx, neighbourZIdx] = neighbours[i];
        if (neighbourNode) {
          const neighbourNodeTraversalInfo = traversalInfo[neighbourNode.id];
          // If the neighbour is connected to the ground then we're done, otherwise check whether it's already been traversed and
          // if not then traverse it to see if it's connected to the ground
          if (neighbourNodeTraversalInfo.grounded || (neighbourNodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE && 
            isConnectedToGround(neighbourNode, neighbourXIdx, neighbourYIdx, neighbourZIdx))) {
            grounded = true;
            break;
          }
        }
      }

      nodeTraversalInfo.grounded = grounded;
      nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
      return grounded;
    };

    // Initialize the node traversal info
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node) {
            traversalInfo[node.id] = {
              node: node,
              visitState: y === 0 ? TRAVERSAL_FINISHED_STATE : TRAVERSAL_UNVISITED_STATE,
              grounded: y === 0,
            };
          }
        }
      }
    }

    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 1; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node) {
            const nodeTraversalInfo = traversalInfo[node.id];
            if (nodeTraversalInfo.visitState === TRAVERSAL_UNVISITED_STATE) {
              nodeTraversalInfo.grounded = isConnectedToGround(node, x, z, y);
            }
          }
        }
      }
    }

    return traversalInfo;
  }


  _clearDebugDraw() {
    if (this.debugNodePoints) {
      this.terrainGroup.remove(this.debugNodePoints);
      this.debugNodePoints.geometry.dispose();
      this.debugNodePoints = null;
    }
  }
  debugDrawNodes(show=true, traversalInfo=null) {
    if (show && this.debugNodePoints || !show && !this.debugNodePoints) { return; }

    this._clearDebugDraw();

    if (show) {
      const nodeGeometry = new THREE.BufferGeometry();
      const vertices = [];
      const colours  = [];
      const defaultColour = new THREE.Color(0,1,0);
      for (let x = 0; x < this.nodes.length; x++) {
        for (let y = 0; y < this.nodes[x].length; y++) {
          for (let z = 0; z < this.nodes[x][y].length; z++) {
            const currNode = this.nodes[x][y][z];
            if (currNode) {
              const nodePos = currNode.pos;
              vertices.push(nodePos.x); vertices.push(nodePos.y); vertices.push(nodePos.z);
              if (traversalInfo && traversalInfo[currNode.id] && !traversalInfo[currNode.id].grounded) {
                colours.push(1); colours.push(0); colours.push(0);
              }
              else {
                colours.push(defaultColour.r); colours.push(defaultColour.g); colours.push(defaultColour.b);
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
