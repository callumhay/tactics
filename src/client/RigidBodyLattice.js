import * as THREE from 'three';

import TerrainColumn from './TerrainColumn';
import Debug from '../debug';

class LatticeNode {
  constructor(pos, terrainColumns) {
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

export default class RigidBodyLattice {
  constructor() {
    this.clear();
  }

  clear() {
    this.nodes = [];
    this.edges = [];
    this.unitsBetweenNodes = 0;
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

    for (let x = 0; x < numNodesX; x++) {
      const nodeXPos = x*this.unitsBetweenNodes;

      // The node might be associated with more than one part of the terrain
      const currTerrainXIndices = [];
      const floorXIdx = Math.max(0,Math.floor((x-1)/(numNodesPerUnit-1)));
      const terrainZ = terrain[floorXIdx];
      currTerrainXIndices.push(floorXIdx);
      if (x % (numNodesPerUnit - 1) === 0 && floorXIdx+1 < terrain.length) {
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
            columns.push(terrain[currTerrainXIndices[i]][currTerrainZIndices[j]]);
          }
        }
        
        // TODO: Deal with irregular (non-rectangular prism) geometry

        let maxHeight = 0;
        for (let i = 0; i < columns.length; i++) {
          const {landingRanges} = columns[i];
          maxHeight = Math.max(landingRanges[landingRanges.length-1][1], maxHeight);
        }
        const numNodesY = maxHeight * numNodesPerUnit;
        const nodesY = nodesZ[z] = new Array(numNodesY).fill(null);

        for (let y = 0; y < numNodesY; y++) {
          const nodeYPos = y*this.unitsBetweenNodes;
          const currNodePos = new THREE.Vector3(nodeXPos, nodeYPos, nodeZPos);
          const columnsContainingNode = columns.filter(column => column.containsPoint(currNodePos));
          if (columnsContainingNode.length > 0) {
            nodesY[y] = new LatticeNode(currNodePos, columnsContainingNode);
          }
        }

      }
    }

    // Initialize the edges
    // TODO


  }

  debugDrawNodes(terrainGroup, show=true) {
    if (show && this.debugNodePoints || !show && !this.debugNodePoints) { return; }

    if (this.debugNodePoints) {
      terrainGroup.remove(this.debugNodePoints);
      this.debugNodePoints.geometry.dispose();
      this.debugNodePoints = null;
    }

    if (show) {
      const nodeGeometry = new THREE.BufferGeometry();
      const vertices = [];
      for (let x = 0; x < this.nodes.length; x++) {
        for (let y = 0; y < this.nodes[x].length; y++) {
          for (let z = 0; z < this.nodes[x][y].length; z++) {
            const currNode = this.nodes[x][y][z];
            if (currNode) {
              const nodePos = currNode.pos;
              vertices.push(nodePos.x);
              vertices.push(nodePos.y);
              vertices.push(nodePos.z);
            }
          }
        }
      }

      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(vertices), 3));
      this.debugNodePoints = new THREE.Points(nodeGeometry, new THREE.PointsMaterial({color:0xFF0000, size:this.unitsBetweenNodes/5, depthFunc:THREE.AlwaysDepth}));
      this.debugNodePoints.renderOrder = Debug.NODE_DEBUG_RENDER_ORDER;
      terrainGroup.add(this.debugNodePoints);
    }
  }




}
