import * as THREE from 'three';

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';
import { Vector3 } from 'three';

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, columnsAndLandingRanges) {
    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.columnsAndLandingRanges = columnsAndLandingRanges;
  }
  hasLandingRange(landingRange) {
    for (let i = 0; i < this.columnsAndLandingRanges.length; i++) {
      // Make sure the node is part of the given landingRange
      const obj = this.columnsAndLandingRanges[i];
      for (let j = 0; j < obj.landingRanges.length; j++) {
        if (obj.landingRanges[j] === landingRange) {
          return true;
        }
      }
    }
    return false;
  }

  removeLandingRange(landingRange) {
    for (let i = 0; i < this.columnsAndLandingRanges.length; i++) {
      const obj = this.columnsAndLandingRanges[i];
      const { landingRanges } = obj;
      obj.landingRanges = landingRanges.filter(range => range !== landingRange);
    }
    this.columnsAndLandingRanges = this.columnsAndLandingRanges.filter(obj => obj.landingRanges.length > 0);
  }

  debugColourForLandingRanges() {
    const colour = new THREE.Color(0,0,0);
    let count = 0;
    for (let i = 0; i < this.columnsAndLandingRanges.length; i++) {
      const obj = this.columnsAndLandingRanges[i];
      for (let j = 0; j < obj.landingRanges.length; j++) {
        const landingRange = obj.landingRanges[j];
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
const TRAVERSAL_BEING_VISITED_STATE = 1;
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
    this.edges = [];
    this.numNodesPerUnit = 0;
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

          const columnsAndLandingRanges = [];
          for (let i = 0; i < columns.length; i++) {
            const column = columns[i];
            const landingRanges = column.landingRangesContainingPoint(currNodePos);
            if (landingRanges.length > 0) {
              columnsAndLandingRanges.push({column,landingRanges});
            }
          }
          if (columnsAndLandingRanges.length > 0) {
            nodesY[y] = new LatticeNode(currNodeId++, x, z, y, currNodePos, columnsAndLandingRanges);
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

  getNodesInLandingRange(landingRange) {
    // Grab all of the nodes that would be within the landing range (but might be tied to other landing ranges as well)
    const {terrainColumn, startY, endY} = landingRange;
    const {xIndex, zIndex} = terrainColumn;

    const numNodesPerUnitMinusOne = (this.numNodesPerUnit-1);

    const nodeXIdxStart = xIndex*numNodesPerUnitMinusOne;
    const nodeXIdxEnd   = nodeXIdxStart + numNodesPerUnitMinusOne;
    const nodeZIdxStart = zIndex*numNodesPerUnitMinusOne;
    const nodeZIdxEnd   = nodeZIdxStart + numNodesPerUnitMinusOne;
    const nodeYIdxStart = Math.floor(startY*numNodesPerUnitMinusOne);
    const nodeYIdxEnd   = Math.floor(endY*numNodesPerUnitMinusOne);

    const result = [];
    for (let x = nodeXIdxStart; x <= nodeXIdxEnd; x++) {
      for (let z = nodeZIdxStart; z <= nodeZIdxEnd; z++) {
        for (let y = nodeYIdxStart; y <= nodeYIdxEnd; y++) {
          const node = this.nodes[x][z][y];
          if (node) { 
            if (node.hasLandingRange(landingRange)) {
              result.push(node);
            }
          }
        }
      }
    }
    return result;
  }

  removeNodesInsideLandingRange(landingRange) {
    const nodes = this.getNodesInLandingRange(landingRange);
    nodes.forEach(node => {
      // If the node ONLY contains the given landing range then we remove it
      if (node && node.columnsAndLandingRanges.filter(obj => obj.landingRanges.length === 1 && obj.landingRanges[0] === landingRange).length === node.columnsAndLandingRanges.length) {
        const {xIdx, zIdx, yIdx} = node;
        this.nodes[xIdx][zIdx][yIdx] = null;
      }
    });
  }

  updateNodesForLandingRange(landingRange, epsilon=TerrainColumn.EPSILON) {
    const {mesh} = landingRange;
    const nodes = this.getNodesInLandingRange(landingRange);

    const raycaster = new THREE.Raycaster();
    raycaster.near = 0;
    raycaster.far = landingRange.height;
    const rayPos = new Vector3();
    const rayDir = new Vector3(0,1,0);
    
    const temp = mesh.material.side;
    mesh.material.side = THREE.DoubleSide;

    const remove = (node, landingRange) => {
      const {xIdx,zIdx,yIdx} = node;
      node.removeLandingRange(landingRange);
      if (node.columnsAndLandingRanges.length === 0) {
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
    
    const traversalInfo = this.traverseToGround();
    this.debugDrawNodes(true, traversalInfo);
  }

  getNeighboursForNodes(nodes) {
    const result = new Set();
    const nodesToIgnore = new Set(nodes);
    nodes.forEach(node => {
      const currNeighbours = this.getNeighboursForNode(node).filter(n => !nodesToIgnore.has(n));
      currNeighbours.forEach(n => result.add(n));
    });
    return result;
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

  traverseToGround() {
    const traversalInfo = {};
    const queue = [];

    // Initialize the node traversal info
    for (let x = 0; x < this.nodes.length; x++) {
      for (let z = 0; z < this.nodes[x].length; z++) {
        for (let y = 0; y < this.nodes[x][z].length; y++) {
          const node = this.nodes[x][z][y];
          if (node) {
            traversalInfo[node.id] = {
              node: node,
              visitState: TRAVERSAL_UNVISITED_STATE,
              grounded: false,
            };
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
          nodeTraversalInfo.grounded = true;
          nodeTraversalInfo.visitState = TRAVERSAL_FINISHED_STATE;
          queue.push(...this.getNeighboursForNode(node));
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
                colours.push(0); colours.push(0); colours.push(0); // Detached nodes are black
              }
              else {
                const c = currNode.debugColourForLandingRanges();
                colours.push(c.r); colours.push(c.g); colours.push(c.b);
                //colours.push(defaultColour.r); colours.push(defaultColour.g); colours.push(defaultColour.b);
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
