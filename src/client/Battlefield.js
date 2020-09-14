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
  static get MAX_SIZE() { return TerrainColumn.SIZE * 64; }
  static get HALF_MAX_SIZE() { return Battlefield.MAX_SIZE / 2; }
  static get MAX_HEIGHT() { return TerrainColumn.SIZE * 24; }
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
    const terrainXSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(-terrainXSize / 2, 0, -terrainXSize / 2);
    for (let x = 0; x < this._terrain.length; x++) {
      while (this._terrain[x].length !== maxZLength) {
        this._terrain[x].push(new TerrainColumn(this, x,this._terrain[x].length, null));
      }
    }

    const terrainZSize = maxZLength * TerrainColumn.SIZE;
    this.waterNodeLattice.initNodeSpace(terrainXSize, terrainZSize, this.MAX_HEIGHT);
    this.terrainNodeLattice.initNodeSpace(terrainXSize, terrainZSize, this.MAX_HEIGHT);

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
    }
  }

  waterPhysicsUpdate(dt) {
    const { waterNodeLattice, terrainNodeLattice } = this;
    const { nodes: terrainNodes } = terrainNodeLattice;
    const { waterNodes, waterEdges, unitsBetweenNodes } = waterNodeLattice;
    if (!waterNodes) { return; }
    const waterNodeArea = unitsBetweenNodes*unitsBetweenNodes;
    const dtStable = Math.min(dt, 0.1);//unitsBetweenNodes/GamePhysics.GRAVITY);
  }

}

const tempVec3 = new THREE.Vector3();
const tempVec3a = new THREE.Vector3();

const GRAVITY_VEC3 = new THREE.Vector3(0,-GamePhysics.GRAVITY,0);
const FLUID_PARTICLE_MASS = 1;
const STIFFNESS = 10;
const STIFFNESS_NEAR = (100/35) * STIFFNESS;
const REST_DENSITY = 5;

class FluidState {
  constructor(waterNodeLattice, terrainNodeLattice) {
    this.waterNodeLattice = waterNodeLattice;
    this.terrainNodeLattice = terrainNodeLattice;

    const {unitsBetweenNodes} = this.waterNodeLattice;
    this.interactionRadius = Math.sqrt(2*unitsBetweenNodes*unitsBetweenNodes) + TerrainColumn.EPSILON;
    this.interactionRadiusSq = this.interactionRadius*this.interactionRadius;
    this.interactionRadiusInv = 1 / this.interactionRadius;

    this.positions = [];
    this.oldPositions = [];
    this.velocities = [];
    this.pressures = [];
    this.pressureNear = [];
    this.gradients = [];

    // We build enough particles for all the water present in the lattice
    const {nodes} = waterNodeLattice;
    for (let x = 0; x < nodes.length; x++) {
      for (let z = 0; z < nodes[x].length; z++) {
        for (let y = 0; y < nodes[x][z].length; y++) {
          const node = nodes[x][z][y];
          if (node) {
            this.positions.push(node.pos.clone());
            this.oldPositions.push(node.pos.clone());
            this.velocities.push(new THREE.Vector3(0,0,0));
            this.pressures.push(0);
            this.pressureNear.push(0);
            this.gradients.push(0);
          }
        }
      }
    }
    this.hashMap = new SpatialHashMap(nodes.length, nodes[0][0].length, nodes[0].length);
  }

  mass(i) { return FLUID_PARTICLE_MASS; }

  update(dt) {
    this.hashMap.clear();

    // Pass 1: Update positions from velocities after applying global forces
    for (let i = 0; i < this.positions.length; i++) {
      // Update the old position
      this.oldPositions[i].copy(this.positions[i]);

      this.applyGlobalForces(i, dt);

      // Update positions from the velocities
      const position = this.positions[i];
      tempVec3.copy(this.velocities[i]).multiplyScalar(dt);
      position.add(tempVec3);

      // Update the hashmap
      this.waterNodeLattice._posToNodeIndex(position, tempVec3);
      this.hashMap.add(tempVec3, i)
    }

    // Pass 2: update the pressure of each particle based on the surrounding neighbours and
    // then do a "double density relaxation"
    for (let i = 0; i < this.positions.length; i++) {
      const neighbours = this.getNeighboursWithGradients(i);
      this.updatePressure(i, neighbours);
      this.relax(i, neighbours, dt);
    }

    // Pass 3: Constrain the particles to the terrain / container
    this.waterNodeLattice.removeAllNodes();
    for (let i = 0; i < this.positions.length; i++) {
      this.contain(i,dt);
      this.calculateVelocity(i,dt);
      
      // TODO: Final update for the water node lattice so we can draw it
      /*
       const terrainColumn = this.getTerrainColumn(Math.floor(position.x), Math.floor(position.z));
        const node = this.waterNodeLattice.buildNode({
          xIdx:tempVec3.x, zIdx:tempVec3.z, yIdx:tempVec3.y,
          terrainColumn, material:GameMaterials.materials[GameMaterials.MATERIAL_TYPE_WATER]
        });
        */
    }
  }

  applyGlobalForces(i, dt) {
    const force = GRAVITY_VEC3;
    // f = m*a, v += a*dt
    // => v += f*dt/m
    tempVec3.copy(force).multiplyScalar(dt/this.mass(i));
    this.velocities[i].add(tempVec3);
  }

  getNeighboursWithGradients(i) {
    const radius = this.interactionRadius;
    const position = this.positions[i];
    const foundNodes = this.waterNodeLattice.getNodesInRadius(position, radius);
    const neighbourIndices = [];
    for (const node of foundNodes) {
      const {fluidStateIndices} = node;
      for (const fluidIdx of fluidStateIndices) {
        if (fluidIdx === i) { continue; }
        // Calculate the gradients for each neighbour
        const g = this.calcGradient(i,fluidIdx);
        if (!g) { continue; }
        this.gradients[fluidIdx] = g;
        neighbourIndices.push(fluidIdx);
      }
    }
    return neighbourIndices;
  }

  calcGradient(i, neighbourIdx) {
    const a = this.positions[i];
    const b = this.positions[neighbourIdx];
    tempVec3.subVectors(a,b);
    const lsq = tempVec3.lengthSq();
    if (lsq > this.interactionRadiusSq) { return 0; }
    return Math.max(0, 1-Math.sqrt(lsq)*this.interactionRadiusInv);
  }

  updatePressure(i, neighbours) {
    let density = 0;
    let nearDensity = 0;
    const m = this.mass(i);

    for (let k = 0; k < neighbours.length; k++) {
      const g = this.gradients[neighbours[k]];
      density += g*g*m;
      nearDensity += g*g*g*m;
    }

    this.pressures[i] = STIFFNESS * (density - REST_DENSITY) * m;
    this.pressureNear[i] = STIFFNESS_NEAR * nearDensity * m;
  }

  relax(i, neighbours, dt) {
    const pos = this.positions[i];
    for (let k = 0; k < neighbours.length; k++) {
      const n = neighbours[k];
      const g = this.gradients[n];
      const nPos = this.positions[n];
      const magnitude = this.pressures[i] * g + this.pressureNear[i] * g * g;

      tempVec3.subVectors(nPos, pos).normalize();
      const dI = tempVec3.multiplyScalar(magnitude * dt * dt);
      const dN = tempVec3a.copy(dI);

      const massI = this.mass(i);
      const massN = this.mass(n);
      const massTotal = massI + massN;
      const massIPercentage = massI / massTotal;
      const massNPercentage = massN / massTotal;
      dI.multiplyScalar(massIPercentage);
      dN.multiplyScalar(massNPercentage);

      pos.sub(dI);
      nPos.add(dN);
    }
  }

  contain(i, dt) {
    const position = this.positions[i];

  }

}


export default Battlefield;