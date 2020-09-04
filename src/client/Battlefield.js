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
    this.rigidBodyLattice = new RigidBodyLattice(this.terrainGroup);

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
    // Order matters here!!!
    this.clear();
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

    //this.rigidBodyLattice.debugDrawNodes(true);
  }

  clear() {
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].clear();
      }
    }
    this._terrain = [];
    MarchingCubes.clearCubeCellRegister();
  }

  // NOTE: We assume that the explosionShape is in the same coordinate space as the terrain
  blowupTerrain(explosionShape) {
    // Blow up the nodes
    // Test all the nodes in this column against explosionShape and remove all nodes inside it
    const removedNodes = this.rigidBodyLattice.removeNodesInsideShape(explosionShape);
    
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
    //this.rigidBodyLattice.debugDrawNodes(true); // TODO: Remove this
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
              const addedNodes = this.rigidBodyLattice.addTerrainColumnDebris(terrainColumn, debrisObj);
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
    this.rigidBodyLattice.traverseGroundedNodes();
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
    const {rigidBodyLattice, terrainGroup, physics, debris} = this;

    rigidBodyLattice.traverseGroundedNodes(affectedTerrainCols);
    const islands = rigidBodyLattice.traverseIslands();

    // Return a map of terrainColumn -> cubeIds for terrain regeneration
    const tcCubeIdMap = {};
    for (const islandNodeSet of islands) {
      const debrisObj = new Debris(terrainGroup, rigidBodyLattice, islandNodeSet);
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
      rigidBodyLattice.removeNodes(nodeSet);
    }

    return tcCubeIdMap;
  }
}

export default Battlefield;