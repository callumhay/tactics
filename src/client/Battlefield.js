import * as THREE from 'three';

import Debug from '../debug';
import MathUtils from '../MathUtils';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice';
import Debris from './Debris';
import GameTypes from './GameTypes';
import GameMaterials from './GameMaterials';
import MarchingCubes from './MarchingCubes';
import { assert } from 'chai';
import GamePhysics from './GamePhysics';
import { TetrahedronGeometry } from 'three';

class Battlefield {
  static get MAX_SIZE() { return TerrainColumn.SIZE * 200; }
  static get HALF_MAX_SIZE() { return Battlefield.MAX_SIZE / 2; }
  static get MAX_HEIGHT() { return TerrainColumn.SIZE * 25; }
  static get HALF_MAX_HEIGHT() { return Battlefield.MAX_HEIGHT/2; }

  constructor(scene, physics) {
    this._scene = scene;
    this.physics = physics;

    this._terrain = [];
    this.debris = [];

    this.sunLight = new THREE.DirectionalLight(0xffffff, 0.2);
    this.sunLight.castShadow = false;
    this.sunLight.position.set(0, 1, -1);
    this.sunLight.position.normalize();
    this._scene.add(this.sunLight);

    const skyColor = 0xB1E1FF;  // light blue
    const groundColor = 0xB97A20;  // brownish orange
    this.skyLight = new THREE.HemisphereLight(skyColor, groundColor, 0.3);
    this._scene.add(this.skyLight);

    this.fillLight = new THREE.PointLight(0xffffff, 0.5);
    this.fillLight.castShadow = true;
    this.fillLight.shadow.mapSize.width = 1024;
    this.fillLight.shadow.mapSize.height = 1024;
    this.fillLight.position.set(0,10,10);
    this._scene.add(this.fillLight);

    this.extentsBoundingBox = new THREE.Box3(
      new THREE.Vector3(0, 0, 0), new THREE.Vector3(Battlefield.MAX_SIZE, Battlefield.MAX_SIZE, Battlefield.MAX_HEIGHT)
    );

    this.terrainGroup = new THREE.Group();
    this.terrainNodeLattice = new RigidBodyLattice(this.terrainGroup);
    this.waterNodeLattice = new RigidBodyLattice(this.terrainGroup);

    const bedrockMat = GameMaterials.materials[GameMaterials.MATERIAL_TYPE_BEDROCK];
    bedrockMat.three.map.repeat.set(Battlefield.HALF_MAX_SIZE, Battlefield.HALF_MAX_SIZE);
    const bedrockGeom = new THREE.BoxBufferGeometry(Battlefield.MAX_SIZE, Battlefield.MAX_SIZE, TerrainColumn.SIZE);
    bedrockGeom.translate(0, 0, -TerrainColumn.HALF_SIZE);
    this.bedrockMesh = new THREE.Mesh(bedrockGeom, bedrockMat.three);
    this.bedrockMesh.receiveShadow = true;
    this.physics.addBedrock({ 
      gameType: GameTypes.BEDROCK,
      gameObject: null, 
      mesh: this.bedrockMesh, 
      material: bedrockMat.cannon
    });
    this.terrainGroup.add(this.bedrockMesh);

    // TODO: Debug routine?
    const grid = new THREE.GridHelper(Battlefield.MAX_SIZE, Battlefield.MAX_SIZE, 0x00FF00);
    grid.translateY(0.1);
    grid.renderOrder = Debug.GRID_RENDER_ORDER;
    //grid.material.depthFunc = THREE.AlwaysDepth;
    grid.material.opacity = 0.4;
    grid.material.transparent = true;
    this.terrainGroup.add(grid);

    this._scene.add(this.terrainGroup);
  }

  setTerrain(terrain) {
    this._clearTerrainColumns();
    this._terrain = terrain;

    // The map must be square... make sure of this
    let maxZLength = 0;
    for (let x = 0; x < this._terrain.length; x++) {
      maxZLength = Math.max(maxZLength, this._terrain[x].length);
    }
    const terrainSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(-terrainSize / 2, 0, -terrainSize / 2);
    for (let x = 0; x < this._terrain.length; x++) {
      while (this._terrain[x].length !== maxZLength) {
        this._terrain[x].push(new TerrainColumn(this, x,this._terrain[x].length, null));
      }
    }

    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    this.terrainPhysicsCleanup();

    // Regenerate everything for the first time.
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].regenerate();
      }
    }

    this.initWaterPhysics();

    //this.terrainNodeLattice.debugDrawNodes(true);
  }
  _clearTerrainColumns() {
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].clear();
      }
    }
    this._terrain = [];
  }

  clear() {
    this._clearTerrainColumns();
    this.terrainNodeLattice.clear();
    this.waterNodeLattice.clear();
  }

  update(dt) {
    this.waterPhysicsUpdate(dt);
    if (this.count) {
      if (this.count % 60 === 0) this._debugDrawWaterData();
    }
    else {
      this.count = 0;
    }
    this.count++;
  }

  // NOTE: We assume that the explosionShape is in the same coordinate space as the terrain
  blowupTerrain(explosionShape) {
    // Blow up the nodes
    // Test all the nodes in this column against explosionShape and remove all nodes inside it
    const removedNodes = this.terrainNodeLattice.removeNodesInsideShape(explosionShape);
    
    // Build a set of TerrainColumns and associated cube IDs for all the removed nodes 
    // so that we know what to update
    const terrainCols = new Set();
    const tcCubeIdMap = {};
    for (const node of removedNodes) {
      const { terrainColumn, xIdx, yIdx, zIdx } = node;
      assert(terrainColumn);
      terrainCols.add(terrainColumn);

      const tcId = terrainColumn.id;
      if (!tcCubeIdMap[tcId]) { tcCubeIdMap[tcId] = { terrainColumn, cubeIds: new Set() }; }
      tcCubeIdMap[tcId].cubeIds.add(MarchingCubes.createCubeCellRegisterKey(xIdx, yIdx, zIdx));
    }

    // Update the terrain physics for any islands that may have been formed
    {
      const otherTcCubeIdMap = this.terrainPhysicsCleanup(terrainCols);
      // Merge the two TerrainColumn -> cube ID mappings
      for (const entry of Object.entries(otherTcCubeIdMap)) {
        const [id, obj] = entry;
        const {terrainColumn, cubeIds} = obj;
        if (!tcCubeIdMap[id]) { tcCubeIdMap[id] = { terrainColumn, cubeIds: new Set() }; }
        tcCubeIdMap[id].cubeIds = new Set([...tcCubeIdMap[id].cubeIds, ...cubeIds]);
      }
    }

    this.regenerateTerrainColumns(tcCubeIdMap);
    //this.terrainNodeLattice.debugDrawNodes(true); // TODO: Remove this
  }

  regenerateTerrainColumns(terrainColCubeIdMap) {
    const affectedTCs = {};
    const mergeAffectedTCs = (atcObj) => {
      const { terrainColumn, cubeIds } = atcObj;
      const { id } = terrainColumn;
      if (!affectedTCs[id]) {
        affectedTCs[id] = atcObj;
      }
      affectedTCs[id].cubeIds = new Set([...affectedTCs[id].cubeIds, ...cubeIds]);
    };

    const queue = [];
    for (const atcObj of Object.values(terrainColCubeIdMap)) {
      mergeAffectedTCs(atcObj);
      const {terrainColumn} = atcObj;
      queue.push(terrainColumn);
    }

    const alreadyVisited = new Set();
    while (queue.length > 0) {
      const terrainColumn = queue.shift();
      if (alreadyVisited.has(terrainColumn)) { continue; }
      const {id} = terrainColumn;
      const {cubeIds} = affectedTCs[id];
      assert(cubeIds);
      const otherAffectedTCsMap = terrainColumn.regenerate(cubeIds);
      for (const atcObj of Object.values(otherAffectedTCsMap)) {
        mergeAffectedTCs(atcObj);
        queue.push(atcObj.terrainColumn);
      }
      alreadyVisited.add(terrainColumn);
    }
  }

  getTerrainColumn(xIdx, zIdx) {
    if (this._terrain[xIdx]) {
      return this._terrain[xIdx][zIdx] || null;
    }
    return null;
  }

  convertDebrisToTerrain(debrisPhysObj) {
    const { gameType, gameObject: debrisObj, mesh } = debrisPhysObj;
    if (gameType !== GameTypes.DEBRIS) {
      console.error("Attempting to convert invalid game type back into terrain, ignoring.");
      return;
    }

    mesh.updateMatrixWorld();
    const { geometry, rotation } = mesh;

    // Bake the rotation of the mesh into the geometry and remove it from the mesh
    const rotationMatrix = new THREE.Matrix4();
    rotationMatrix.makeRotationFromEuler(rotation);
    geometry.applyMatrix4(rotationMatrix);
    rotation.set(0,0,0);
    mesh.updateMatrix();

    // Bake the translation into the geometry and remove the remaining transform from the mesh
    const translation = new THREE.Vector3(
      MathUtils.roundToDecimal(mesh.position.x, 2), 
      MathUtils.roundToDecimal(mesh.position.y, 2), 
      MathUtils.roundToDecimal(mesh.position.z, 2)
    );
    geometry.translate(translation.x, translation.y, translation.z);

    mesh.translateX(-translation.x);
    mesh.translateY(-translation.y);
    mesh.translateZ(-translation.z);
    mesh.position.set(
      MathUtils.roundToDecimal(mesh.position.x, 2), 
      MathUtils.roundToDecimal(mesh.position.y, 2),
      MathUtils.roundToDecimal(mesh.position.z, 2),
    );
    mesh.updateMatrixWorld();
    

    // Check to see if the mesh is inside the playable area anymore
    //this.terrainGroup.add(new THREE.Box3Helper(new THREE.Box3(closestMin.clone(), closestMax.clone()))); // Debugging

    // We need to reattach the debris to the terrain in all the 
    // correct terrain columns that it now occupies
    geometry.computeBoundingBox();
    const {min, max} = geometry.boundingBox;
    const floorMinX = Math.floor(min.x);
    const ceilMaxX = Math.ceil(max.x);
    const floorMinZ = Math.floor(min.z);
    const ceilMaxZ = Math.ceil(max.z);

    let numAddedNodes = 0;
    const tcCubeIdMap = {};
    if (ceilMaxX >= 0 && floorMinX < this._terrain.length && ceilMaxZ >= 0 && floorMinZ < this._terrain[0].length) {

      const startX = floorMinX;
      const endX = ceilMaxX;
      const startZ = floorMinZ;
      const endZ = ceilMaxZ;

      for (let x = startX; x <= endX; x++) {
        if (this._terrain[x]) {
          for (let z = startZ; z <= endZ; z++) {
            if (this._terrain[x][z]) {
              const terrainColumn = this._terrain[x][z];
              const { id } = terrainColumn;
              const addedNodes = this.terrainNodeLattice.addTerrainColumnDebris(terrainColumn, debrisObj);
              numAddedNodes += addedNodes.length;
              if (addedNodes.length > 0 && !tcCubeIdMap[id]) { 
                tcCubeIdMap[id] = { terrainColumn, cubeIds: addedNodes.map(n => {
                  const {xIdx, yIdx, zIdx} = n;
                  return MarchingCubes.createCubeCellRegisterKey(xIdx,yIdx,zIdx);
                })} 
              }
            }
          }
        }
      }
    }

    if (numAddedNodes === 0) {
      console.log("Debris went outside of the battlefield bounds.");
      console.warn("You need to implement debris dissolution, for the time being it just turns red!");
      mesh.material = new THREE.MeshLambertMaterial({ emissive: 0xff0000 });
      return;
    }

    // Re-traverse the rigid body node lattice and regenerate the affected columns
    this.terrainNodeLattice.traverseGroundedNodes();
    this.regenerateTerrainColumns(tcCubeIdMap);

    // Clean-up
    debrisObj.clearGeometry();
    this.debris.splice(this.debris.indexOf(debrisObj), 1);
  }

  // Do a check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  // The given affectedTerrainCols are the ones to examine and update, if a list isn't given then
  // ALL terrainColumns are examined.
  terrainPhysicsCleanup(affectedTerrainCols=[]) {
    const {terrainNodeLattice, terrainGroup, physics, debris} = this;

    terrainNodeLattice.traverseGroundedNodes(affectedTerrainCols);
    const islands = terrainNodeLattice.traverseIslands();

    // Return a map of terrainColumn -> cubeIds for terrain regeneration
    const tcCubeIdMap = {};
    for (const islandNodeSet of islands) {
      const debrisObj = new Debris(terrainGroup, terrainNodeLattice, islandNodeSet);
      if (debrisObj.mesh !== null) {
        const physObj = debrisObj.addPhysics(physics);
        if (physObj) {
          debris.push(debrisObj);
        }
      }

      for (const node of islandNodeSet) {
        const {terrainColumn, xIdx, yIdx, zIdx} = node;
        assert(terrainColumn);
        const tcId = terrainColumn.id;
        const cubeId = MarchingCubes.createCubeCellRegisterKey(xIdx, yIdx, zIdx);
        if (!tcCubeIdMap[tcId]) { tcCubeIdMap[tcId] = {terrainColumn, cubeIds: new Set()}; }
        tcCubeIdMap[tcId].cubeIds.add(cubeId);
      }
    }

    // The nodes are longer part of the terrain, remove them
    for (const nodeSet of islands) {
      terrainNodeLattice.removeNodes(nodeSet);
    }

    return tcCubeIdMap;
  }

  _debugDrawWaterData() {
    const {waterNodes, halfUnitsBetweenNodes} = this.waterNodeLattice;
    if (waterNodes) {
      const nodeGeometry = this._debugWaterPoints ? this._debugWaterPoints.geometry : new THREE.BufferGeometry();
      const vertices = [];
      const colours = [];
      const tempVec3 = new THREE.Vector3();
      for (let x = 0; x < waterNodes.length; x++) {
        for (let z = 0; z < waterNodes[x].length; z++) {
          for (let y = 0; y < waterNodes[x][z].length; y++) {
            const currNode = waterNodes[x][z][y];
            if (currNode) {
              const {xIdx,zIdx,yIdx,mass} = currNode;
              assert(mass >= 0);
              
              const nodePos = this.waterNodeLattice._nodeIndexToPosition(xIdx,yIdx,zIdx);
              if (mass > 0.1) {
                vertices.push(nodePos.x); vertices.push(nodePos.y); vertices.push(nodePos.z);
                colours.push(mass); colours.push(mass); colours.push(mass);
              }
              /*
              for (let i = 0; i < currNode.flowEdges.length; i++) {
                const flowEdge = currNode.flowEdges[i];
                if (flowEdge){// && flowEdge.flow !== 0) {
                  const flowIncs = flowIncrements[i];
                  const flowColour = 1;//THREE.MathUtils.clamp(Math.abs(flowEdge.flow*10), 0, 1);
                  tempVec3.copy(nodePos);
                  tempVec3.set(tempVec3.x + flowIncs[0] * halfUnitsBetweenNodes, tempVec3.y + flowIncs[2] * halfUnitsBetweenNodes, tempVec3.z + flowIncs[1] * halfUnitsBetweenNodes);
                  vertices.push(tempVec3.x); vertices.push(tempVec3.y); vertices.push(tempVec3.z);
                  colours.push(flowColour); colours.push(0); colours.push(flowColour);
                }
              }
              */
              
              

            }
          }
        }
      }
      if (this._debugWaterPoints) {
        this._debugWaterPoints.geometry.dispose();
        this.terrainGroup.remove(this._debugWaterPoints);
      }
      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(vertices), 3));
      nodeGeometry.setAttribute('color', new THREE.BufferAttribute(new Float32Array(colours), 3));
      this._debugWaterPoints = new THREE.Points(nodeGeometry, new THREE.PointsMaterial({ color: 0xFFFFFF, vertexColors: true, size: 0.02, depthFunc: THREE.AlwaysDepth }));
      this._debugWaterPoints.renderOrder = Debug.NODE_DEBUG_RENDER_ORDER;
      this.terrainGroup.add(this._debugWaterPoints);
    }
  }

  initWaterPhysics() {
    const {waterNodeLattice} = this;
    const { nodes, numNodesPerUnit, unitsBetweenNodes, diagonalUnitsBetweenNodes} = waterNodeLattice;
    if (!waterNodeLattice.waterNodes) {
      let maxYIdx = 0;
      for (let x = 0; x < nodes.length; x++) {
        for (let z = 0; z < nodes[x].length; z++) {
          maxYIdx = Math.max(maxYIdx, nodes[x][z].length);
        }
      }

      // Initialize the water data nodes
      const nodesPerSideOfBattlefield = this._terrain.length * numNodesPerUnit;
      const waterNodes = waterNodeLattice.waterNodes = [];
      const waterEdges = waterNodeLattice.waterEdges = [];
      while (waterNodes.length < nodesPerSideOfBattlefield) { waterNodes.push([]); }
      for (let x = 0; x < waterNodes.length; x++) {
        while (waterNodes[x].length < nodesPerSideOfBattlefield) { waterNodes[x].push([]);}
        for (let z = 0; z < waterNodes[x].length; z++) {
          for (let y = 0; y < maxYIdx; y++) {
            waterNodes[x][z].push(new WaterData(x, z, y, 
              nodes[x] && nodes[x][z] && nodes[x][z][y] ? 1 : 0));
          }
        }
      }
      // Initialize the edges between water data nodes
      for (let x = 0; x < waterNodes.length; x++) {
        for (let z = 0; z < waterNodes[x].length; z++) {
          for (let y = 0; y < waterNodes[x][z].length; y++) {
            const waterNode = waterNodes[x][z][y];
            assert(waterNode);
            const {flowEdges} = waterNode;
            /*
            if (x !== 0) {
              const xMinus1WN = waterNodes[x - 1][z][y];
              flowEdges[0] = new WaterFlowEdge(unitsBetweenNodes, xMinus1WN, waterNode);
              xMinus1WN.flowEdges[4] = flowEdges[0];
              waterEdges.push(flowEdges[0]);
              
              if (z !== 0) {
                const xzMinus1WN = waterNodes[x - 1][z - 1][y];
                flowEdges[1] = new WaterFlowEdge(diagonalUnitsBetweenNodes, xzMinus1WN, waterNode);
                xzMinus1WN.flowEdges[5] = flowEdges[1];
                waterEdges.push(flowEdges[1]);
              }
            }

            if (z !== 0) {
              const zMinus1WN = waterNodes[x][z-1][y];
              flowEdges[2] = new WaterFlowEdge(unitsBetweenNodes, zMinus1WN, waterNode);
              zMinus1WN.flowEdges[6] = flowEdges[2];
              waterEdges.push(flowEdges[2]);

              if (x !== waterNodes.length - 1) {
                const xPlus1ZMinus1WN = waterNodes[x + 1][z - 1][y];
                flowEdges[3] = new WaterFlowEdge(diagonalUnitsBetweenNodes, waterNode, xPlus1ZMinus1WN);
                xPlus1ZMinus1WN.flowEdges[7] = flowEdges[3];
                waterEdges.push(flowEdges[3]);
              }
            }

            if (y !== 0) {
              const yMinus1WN = waterNodes[x][z][y-1];
              flowEdges[8] = new WaterFlowEdge(unitsBetweenNodes, yMinus1WN, waterNode);
              yMinus1WN.flowEdges[9] = flowEdges[8];
              waterEdges.push(flowEdges[8]);
            }
            */

          
            if (x !== 0) {
              flowEdges[0] = new WaterFlowEdge(unitsBetweenNodes);
              if (z !== 0) {
                flowEdges[1] = new WaterFlowEdge(diagonalUnitsBetweenNodes);
              }
            }
            if (x !== waterNodes.length-1) {
              flowEdges[4] = new WaterFlowEdge(unitsBetweenNodes);
              if (z !== waterNodes[x].length-1) {
                flowEdges[5] = new WaterFlowEdge(diagonalUnitsBetweenNodes);
              }
            }

            if (z !== 0) {
              flowEdges[2] = new WaterFlowEdge(unitsBetweenNodes);
              if (x !== waterNodes.length - 1) {
                flowEdges[3] = new WaterFlowEdge(diagonalUnitsBetweenNodes);
              }
            }
            if (z !== waterNodes[x].length - 1) {
              flowEdges[6] = new WaterFlowEdge(unitsBetweenNodes);
              if (x !== 0) {
                flowEdges[7] = new WaterFlowEdge(diagonalUnitsBetweenNodes);
              }
            }

            if (y !== 0) {
              flowEdges[8] = new WaterFlowEdge(unitsBetweenNodes);
            }
            if (y !== waterNodes[x][z].length-1) {
              flowEdges[9] = new WaterFlowEdge(unitsBetweenNodes);
            }
           
          }

        }
      }
    }
  }

  waterPhysicsUpdate(dt) {
    const { waterNodeLattice, terrainNodeLattice } = this;
    const { nodes: terrainNodes } = terrainNodeLattice;
    const { waterNodes, waterEdges, unitsBetweenNodes } = waterNodeLattice;
    if (!waterNodes) { return; }
    const waterNodeArea = unitsBetweenNodes*unitsBetweenNodes;
    const dtStable = Math.min(dt, 0.1);//unitsBetweenNodes/GamePhysics.GRAVITY);

    // Using the Moore neighbourhood (all adjacent nodes including corners), the area of flow between neighbours
    const sideCrossSectionArea = 0.41421356237 * waterNodeArea; // tan(pi/8)*waterNodeArea

    // Calculate all of the flows...
    for (let x = 0; x < waterNodes.length; x++) {
      for (let z = 0; z < waterNodes[x].length; z++) {
        for (let y = 0; y < waterNodes[x][z].length; y++) {
          const waterNode = waterNodes[x][z][y];
          const {flowEdges} = waterNode;

          let resultMass = waterNode.mass;

          const gravityOutEdge = flowEdges[8];
          const gravityInEdge  = flowEdges[9];
          if (gravityOutEdge) {
            const feedNode = waterNodes[x][z][y - 1];
            const dMass = resultMass - feedNode.mass;
            let flowQtyGravity = (waterNodeArea / unitsBetweenNodes) * GamePhysics.GRAVITY * dMass * dtStable;
            if (gravityInEdge) {
              flowQtyGravity += Math.abs(gravityInEdge.flow)*dtStable;
            }
            let flowQtyFeed = flowQtyGravity;

            /*
            // We only care about keeping the mass steady in this waterNode, let the others
            // take care of themselves via flows
            if (waterNode.mass > 1) {
              // Try to dump the water downward
              flowQtyFeed += flow
            }
            */

            /*
            let massBefore = resultMass;
            resultMass -= (gravityOutEdge.flow+flowQtyGravity) * dtStable;
            if (resultMass < 0) {
              waterNode.mass = 0;
              gravityOutEdge.flow = 0;
              feedNode.flowEdges[9].flow = 0;
              
              if (feedNode.mass > 1) {
                waterNode.mass += feedNode.mass-1;
                feedNode.mass = 1;
              }
            }
            else {
              if (resultMass > 1) {
                waterNode.mass = 1;

              }
              massBefore = feedNode.mass;
              resultMass = massBefore - (feedNode.flowEdges[9].flow - flowQtyGravity) * dtStable;
              if (resultMass > 1) {
                waterNode.mass += resultMass-1;
                if (waterNode.mass > 1) {
                  waterNode.mass = 1;
                }

                feedNode.mass = 1;
                feedNode.flowEdges[9].flow = 0;
                gravityOutEdge.flow = 0;

              }
              else {
                gravityOutEdge.flow += flowQtyGravity;
                feedNode.flowEdges[9].flow -= flowQtyGravity;
              }
            }
            */
          }






          /*
          // Side flows
          let totalSideFlow = 0;
          for (let i = 0; i < flowEdges.length-2; i++) {
            const flowEdge = flowEdges[i];
            if (flowEdge) {
              const inc = flowIncrements[i];
              const neighbourNode = waterNodes[x + inc[0]][z + inc[1]][y];
              const dMass = resultMass - neighbourNode.mass;
              flowEdge.flow += (sideCrossSectionArea / flowEdge.length) * GamePhysics.GRAVITY * dMass * dtStable;
              totalSideFlow += flowEdge.flow;
            }
          }

          const massBefore = resultMass;
          const flowValue = totalSideFlow * dtStable;
          resultMass -= flowValue;
          let flowTotalAdjustMultiplier = 1;
          if (resultMass < 0) {
            flowTotalAdjustMultiplier = massBefore / flowValue;
            resultMass = 0;
          }
          if (flowTotalAdjustMultiplier !== 1) {
            for (let i = 0; i < flowEdges.length - 2; i++) {
              const flowEdge = flowEdges[i];
              if (flowEdge) {
                flowEdge.flow = flowTotalAdjustMultiplier * (flowEdge.flow / totalSideFlow);
              }
            }
          }
          */
        }
      }
    }

    for (let x = 0; x < waterNodes.length; x++) {
      for (let z = 0; z < waterNodes[x].length; z++) {
        for (let y = 0; y < waterNodes[x][z].length; y++) {
          const waterNode = waterNodes[x][z][y];
          for (const flowEdge of waterNode.flowEdges) {
            if (flowEdge) {
              waterNode.mass -= flowEdge.flow * dtStable;
            }
          }
          waterNode.mass = Math.max(0, waterNode.mass);
        }
      }
    }


    
  }

}

const flowIncrements = [
  [-1, 0, 0], [-1, -1, 0], [0, -1, 0], [1, -1, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [-1, 1, 0], [0, 0, -1], [0,0,1]];
class WaterData {
  constructor(xIdx,zIdx,yIdx,mass=1) {
    this.xIdx = xIdx, this.yIdx = yIdx, this.zIdx = zIdx;
    this.mass = mass;
    // Stores the XZ side edges (indices 0-7) and finally going down (8) and flowing from up (9)
    this.flowEdges = new Array(10).fill(null);
  }
}

class WaterFlowEdge {
  constructor(length) {
    this.length = length; 
    this.flow = 0;
  }
};

/*
const flowIncrements = [ [-1,0],[-1,-1],[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1] ];
class WaterData {
  constructor() { 
    this.waterPools = [];
    //this.waterBlobs = [];
  }
  addPool(surfaceY, depth) {
    const newPool = new WaterPool(surfaceY, depth);
    let insertAtEnd = true;

    // Avoid overlapping pools by merging them as needed
    for (let j = 0; j < this.waterPools.length; j++) {
      const pool = this.waterPools[j];
      if (pool.overlapDepth(newPool) >= 0) {
        // Merge the two pools into a new pool
        const bottomY = Math.min(surfaceY-depth, pool.surfaceY-pool.depth);
        newPool.surfaceY = Math.max(surfaceY, pool.surfaceY);
        newPool.depth = newPool.surfaceY - bottomY;
        if (pool.surfaceY !== newPool.surfaceY || pool.depth !== newPool.depth) {
          // Add back the merged pool recursively
          this.waterPools.splice(j,1);
          this.addPool(newPool.surfaceY, newPool.depth);
        }
        insertAtEnd = false;
        break;
      }
      else if (pool.surfaceY > newPool.surfaceY) {
        // Insert the new pool into the current water pool list index
        this.waterPools.splice(j, 0, newPool);
        insertAtEnd = false;
        break;
      }
    }
    if (insertAtEnd) { this.waterPools.push(newPool); }
  }

  addPools(surfaceYs, depths) {
    assert(surfaceYs.length === depths.length);
    for (let i = 0; i < surfaceYs.length; i++) {
      this.addPool(surfaceYs[i], depths[i]);
    }
  }

};
class WaterPool {
  static get flowIncrements() { return flowIncrements; }
  constructor(surfaceY, depth) { 
    assert(depth >= 0 && surfaceY >= 0);
    this.depth = depth; 
    this.surfaceY = surfaceY;
    // There are always 8 flow edges and they are stored counter-clockwise starting from [x,z] = [-1,0]
    //this.flowEdges = new Array(8).fill(null); 
  }
  overlapDepth(pool) {
    return Math.min(this.surfaceY, pool.surfaceY) - Math.max(this.surfaceY-this.depth, pool.surfaceY-pool.depth);
  }
};

*/
export default Battlefield;