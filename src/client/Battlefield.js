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
      if (this.count % 10 === 0) this.waterNodeLattice.debugDrawNodes(true);
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



  initWaterPhysics() {
    const {waterNodeLattice} = this;
    const {nodes:waterNodes, unitsBetweenNodes, diagonalUnitsBetweenNodes} = waterNodeLattice;
    // TODO: THIS INITIALIZATION REQUIRES THE WATER TO BE IN A PHYSICALLY ACCURATE/REASONABLE STATE TO BEGIN WITH!!!
    // WE WILL NEED TO DEAL WITH SITUATIONS WHERE THIS ISN'T THE CASE BEFORE INITIALIZING THIS!!! (e.g., floating / falling water volumes)
    if (!waterNodeLattice.waterData) {
      const waterData = waterNodeLattice.waterData = [];
      while (waterData.length < waterNodes.length) { waterData.push([]); }
      for (let x = 0; x < waterData.length; x++) {
        while (waterData[x].length < waterNodes[x].length) { 
          waterData[x].push(new WaterData());
        }
        for (let z = 0; z < waterData[x].length; z++) {

          // Starting from the top of the current column, march down to the ground and find the y indices
          // of the heights where any pool of water exists
          const waterCol = waterNodes[x][z];
          const waterPoolHeightYIndices = [];
          const waterPoolDepths = [];
          {
            let inWater = false;
            let inWaterCount = 0;
            for (let y = waterCol.length-1; y >= 0; y--) {
              const waterNode = waterCol[y];
              if (waterNode) {
                if (!inWater) {
                  waterPoolHeightYIndices.push(waterNodeLattice._nodeIndexToUnits(y)+unitsBetweenNodes);
                  inWater = true;
                }
                inWaterCount += unitsBetweenNodes;
              }
              else {
                if (inWaterCount > 0) {
                  waterPoolDepths.push(inWaterCount);
                }
                inWater = false;
                inWaterCount = 0;
              }
            }
            if (inWaterCount > 0) {
              waterPoolDepths.push(inWaterCount);
            }
            assert(waterPoolHeightYIndices.length === waterPoolDepths.length);
          }
          // Build the water pool data
          const waterDataObj = waterData[x][z];
          waterDataObj.addPools(waterPoolHeightYIndices, waterPoolDepths);
          /*
          // Build the flow edge data structure for each WaterPool, sharing it with all adjacent pools
          for (let i = 0; i < waterPools.length; i++) {
            const waterPool = waterPools[i];
            const {flowEdges} = waterPool;
            if (x !== 0) {
              flowEdges[0] = new WaterFlowEdge();
              waterData[x-1][z].flowEdges[4] = flowEdges[0]; 
              if (z !== 0) {
                flowEdges[1] = new WaterFlowEdge();
                waterData[x-1][z-1].flowEdges[5] = flowEdges[1];
              }
            }
            if (z !== 0) {
              flowEdges[2] = new WaterFlowEdge();
              waterData[x][z-1].flowEdges[6] = flowEdges[2];
              if (x < waterData.length-1) {
                flowEdges[3] = new WaterFlowEdge();
                waterData[x+1][z-1].flowEdges[7] = flowEdges[3];
              }
            }
          }
          */
        }
      }
    }
  }

  waterPhysicsUpdate(dt) {
    /*
    const {waterNodeLattice, terrainNodeLattice} = this;
    const {nodes:waterNodes, unitsBetweenNodes, diagonalUnitsBetweenNodes} = waterNodeLattice;
    const {nodes:terrainNodes} = terrainNodeLattice;
    const unitsBetweenNodesSqr = unitsBetweenNodes*unitsBetweenNodes;
    const flowScaleMultiplier = 1.0;//Math.min(0.99, dt/(Battlefield.HALF_MAX_HEIGHT*Battlefield.HALF_MAX_HEIGHT*unitsBetweenNodes*GamePhysics.GRAVITY));
    //console.log(flowScaleMultiplier);

    const nodeOrNull = (nodeLookup, idx) => {
      return nodeLookup ? (nodeLookup[idx] ? nodeLookup[idx] : null) : null;
    };
    // Using the Moore neighbourhood (all adjacent nodes including corners), the area of flow between neighbours (// (tan(pi/8)*unitsBetweenNodes*depth))
    const calcFlowCrossSectionArea = (depth) => 0.41421356237 * waterNodeLattice.unitsBetweenNodes * depth; 
    // NOTE: If we were just considering the adjacent (i.e., up,down,left,right) nodes then: flowCrossSectionArea = unitsBetweenNodes*depth
    const {waterData} = waterNodeLattice;

    for (let x = 0; x < waterNodes.length; x++) {
      const waterCenterX = waterNodes[x];
      for (let z = 0; z < waterNodes[x].length; z++) {
        const waterDataObj = waterData[x][z];
        const {waterPools} = waterDataObj;

        const waterCenterCol  = waterCenterX[z];
        if (waterCenterCol.length === 0) { continue; }

        // Gather up any of the existing 8 possible neighbour node columns on the XZ plane of nodes
        const neighbourCols = WaterPool.flowIncrements.map((inc,i) => {
          const xIdx = x+inc[0], zIdx = z+inc[1];
          return {
            xIdx, zIdx, dD: (i % 2) ? diagonalUnitsBetweenNodes: unitsBetweenNodes,
            //flowEdge: flowEdges[i],
            waterCol: nodeOrNull(waterNodes[xIdx],zIdx), 
            terrainCol: nodeOrNull(terrainNodes[xIdx],zIdx)
          };
        });

       
        

        // For each pool of the column's water we will find the sum of flows in/out of the pool and
        // recalculate the height, removing/adding nodes from the water lattice as needed
        for (const waterPool of waterPools) {
          const {depth, surfaceY} = waterPool;
          
          // For each neighbouring column that exists (i.e., is inside the bounds of the battlefield)
          // we find the height difference for the current water pool and calculate the sum of flows in/out of the column
          let flowSum = 0;
          for (const neighbourCol of neighbourCols) {
            const {xIdx, zIdx, dD, waterCol, terrainCol} = neighbourCol;

            // If this neighbour doesn't exist in the battlefield, ignore it
            if (waterCol === null) { continue; } 

            let foundNeighbourPools = [];

            // Try to find the closest connectable water pool
            const {waterPools:neighbourWaterPools} = waterData[xIdx][zIdx];
            for (const neighbourWaterPool of neighbourWaterPools) {
              const {depth:neighbourDepth, surfaceY:neighbourSurfaceY} = neighbourWaterPool;
              if (surfaceY+1e-6 >= neighbourSurfaceY-neighbourDepth && surfaceY-depth <= neighbourSurfaceY+1e-6) {
                foundNeighbourPools.push(neighbourWaterPool);
              }
            }
            if (foundNeighbourPools.length > 0) {
              for (const pool of foundNeighbourPools) {
                const deltaHeight = pool.surfaceY - surfaceY;
                const sharedDepth = waterPool.overlapDepth(pool);
                flowSum += calcFlowCrossSectionArea(Math.abs(sharedDepth)) * (GamePhysics.GRAVITY/dD) * deltaHeight * dt;
              }
            }
            else {
             
              // If there are no neighbouring pools then we check how many openings there are in the terrain between
              // the surface of the water and its depth, this will be the depth-surface-area (i.e., deltaHeight)
              // Each opening in the terrain should generate a new pool of water with no depth (yet)
              let deltaHeight = 0;
              const newWaterPoolSurfaces = [];
              {
                const startY = Math.max(0, terrainNodeLattice._unitsToNodeIndex(surfaceY-depth-terrainNodeLattice.halfUnitsBetweenNodes));
                const endY   = Math.max(0, terrainNodeLattice._unitsToNodeIndex(surfaceY-terrainNodeLattice.halfUnitsBetweenNodes));

                let outsideTerrain = false;
                for (let y = startY; y <= endY; y++) {
                  if (!terrainCol[y]) {
                    if (!outsideTerrain) {
                      outsideTerrain = true;
                      newWaterPoolSurfaces.push(terrainNodeLattice._nodeIndexToUnits(y));
                    }
                    deltaHeight++; 
                  }
                  else {
                    outsideTerrain = false;
                  }
                }
                
              }
              waterDataObj.addPools(newWaterPoolSurfaces, newWaterPoolSurfaces.map(_ => 0));
              flowSum += calcFlowCrossSectionArea(Math.abs(deltaHeight)) * (GamePhysics.GRAVITY/dD) * deltaHeight * dt;
            }
          }

          
          // Apply a hydrostatic pressure equation (based on Newton's law) to calculate the new depth /surface height of the column:
          const totalOutgoingFlow = dt*flowScaleMultiplier*(flowSum/unitsBetweenNodesSqr);
          const minSurfaceY = waterPool.surfaceY-waterPool.depth;

          // The depth cannot be negative, if it is then we need to scale the flow.
          // NOTE: We may want to do this in two steps in the future and properly scale the flow values 
          // based on if a column reaches zero depth
          const prevSurfaceYIdx = waterNodeLattice._unitsToNodeIndex(surfaceY);
          waterPool.surfaceY = Math.max(minSurfaceY, waterPool.surfaceY - totalOutgoingFlow);
          const nextSurfaceYIdx = waterNodeLattice._unitsToNodeIndex(waterPool.surfaceY);
          waterPool.depth = Math.max(0, depth - totalOutgoingFlow);
          assert(prevSurfaceYIdx >= 0 && nextSurfaceYIdx >= 0);

          // Add/remove nodes based on the new water surface y-index in this xz-column
          if (nextSurfaceYIdx > prevSurfaceYIdx) {
            let y = waterCenterCol.length;
            while (waterCenterCol.length <= nextSurfaceYIdx) { waterCenterCol.push(null); }
            for (y = prevSurfaceYIdx; y < nextSurfaceYIdx; y++) {
              waterCenterCol[y] = new LatticeNode(waterNodeLattice.nextNodeId++, x,z,y++,waterCenterCol[0].terrainColumn, GameMaterials.materials[GameMaterials.MATERIAL_TYPE_WATER]);
            }
          }
          else if (nextSurfaceYIdx < prevSurfaceYIdx) {
            for (let y = nextSurfaceYIdx; y < prevSurfaceYIdx; y++) {
              waterNodeLattice._removeNode(x,z,y);
            }
          }

        }
        

      }
    }
    */
  }

}

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
//class WaterFlowEdge {
//  constructor() { this.flow = 0; }
//};

export default Battlefield;