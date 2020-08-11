import * as THREE from 'three';

import TerrainColumn from './TerrainColumn';

class LatticeNode {
  constructor(pos, terrainCol) {
    this.pos = pos;
    this.terrainCol = terrainCol;
  }
}

class LatticeEdge {
  constructor(n0, n1) {
    this.nodes = [n0, n1];
    this.line = new THREE.Line3(n0.pos, n1.pos);
  }

  connects(nodeA, nodeB) {
    return (this.nodes[0] === nodeA && this.nodes[1] === nodeB || this.nodes[0] === nodeB && this.nodes[1] === nodeA);
  }
}

const DEFAULT_NODES_PER_TERRAIN_SQUARE_UNIT = 3;

export default class RigidBodyLattice {
  constructor() {
    this.clear();
  }

  clear() {
    this.nodes = [];
    this.edges = [];
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

    const unitsBetweenNodes = TerrainColumn.SIZE / (numNodesPerUnit-1);

    // Nodes are built to reflect the same coordinate system as the terrain
    const numNodesX = terrain.length * numNodesPerUnit + 1 - terrain.length;
    
    this.nodes = new Array(numNodesX).fill(null);

    
    for (let x = 0; x < numNodesX; x++) {
      const nodeXPos = x*unitsBetweenNodes;

      // The node might be associated with more than one part of the terrain
      const currTerrainXIndices = [];
      const floorXIdx = Math.floor(x/numNodesPerUnit);
      currTerrainXIndices.push(floorXIdx);
      if (x % numNodesPerUnit === numNodesPerUnit-1 && x < numNodesX-1) {
        currTerrainXIndices.push(floorXIdx+1);
      }

      const terrainZ = terrain[floorXIdx];
      const numNodesZ = terrainZ.length * numNodesPerUnit + 1 - terrainZ.length;
      const nodesZ = this.nodes[x] = new Array(numNodesZ).fill(null);
      
      for (let z = 0; z < numNodesZ; z++) {
        const nodeZPos = z*unitsBetweenNodes;

        const currTerrainZIndices = [];
        const floorZIdx = Math.floor(z/numNodesPerUnit);
        currTerrainZIndices.push(floorZIdx);
        if (z % numNodesPerUnit === numNodesPerUnit-1 && z < numNodesZ-1) {
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

/*
        // Deal with the landings of each terrain column, create nodes at each precision interval on the 
        // interior of the terrain that makes up the square

        const {landingRanges} = column;
        const maxHeight = landingRanges[landingRanges.length-1][1];
        const numNodesY = maxHeight * numNodesPerUnit;
        const nodesY = nodesZ[z] = new Array(numNodesY).fill(null);


        for (let r = 0; r < landingRanges.length; r++) {
          const [start, end] = landingRanges[r];

          const nodeYIdx = Math.max(0, Math.floor(start * numNodesPerUnit)-1);
          let nodeYPos = start*unitsBetweenNodes;

          const height = end-start;
          const numNodesYInRange = height*numNodesPerUnit;

          for (let i = 0; i < numNodesYInRange; i++) {
            nodesY[nodeYIdx+i] = new LatticeNode(new THREE.Vector3(nodeXPos, nodeYPos, nodeZPos), column);
            nodeYPos += unitsBetweenNodes;
          }
        }
        */
      
      }
    }

    // Initialize the edges
    // TODO


  }

  debugDrawNodes(terrainGroup, show=true) {
    if (show && this.debugNodeGroup || !show && !this.debugNodeGroup) { return; }
    if (!show) {
      terrainGroup.remove(this.debugNodeGroup);
      this.debugNodeGroup = null;
    }
    else {
      const nodeGeometry = new THREE.BufferGeometry();
      const vertices = new Float32Array(this.numNodes()*3);
      let vertexIdx = 0;
      for (let x = 0; x < this.nodes.length; x++) {
        for (let z = 0; z < this.nodes[x].length; z++) {
          for (let y = 0; y < this.nodes[x][z].length; y++) {
            const currNode = this.nodes[x][z][y];
            if (currNode) {
              const nodePos = currNode.pos;
              vertices[vertexIdx++] = nodePos.x;
              vertices[vertexIdx++] = nodePos.y;
              vertices[vertexIdx++] = nodePos.z;
            }
          }
        }
      }

      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(vertices, 3));
      this.debugNodeGroup = new THREE.Points(nodeGeometry, new THREE.PointsMaterial({color:0xFF0000, size:0.1, depthFunc:THREE.AlwaysDepth}));
      terrainGroup.add(this.debugNodeGroup);
    }
  }




}
