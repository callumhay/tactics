import * as THREE from 'three';

import Debug from '../debug';
import MathUtils from '../MathUtils';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice';
import Debris from './Debris';
import GameTypes from './GameTypes';
import GameMaterials from './GameMaterials';
import MarchingCubes from './MarchingCubes';

export default class Battlefield {
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
    grid.renderOrder = Debug.GRID_RENDER_ORDER;
    grid.material.depthFunc = THREE.AlwaysDepth;
    grid.material.opacity = 0.25;
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

    // Regenerate the terrain
    for (let x = 0; x < this._terrain.length; x++) {
      while (this._terrain[x].length !== maxZLength) {
        this._terrain[x].push(new TerrainColumn(this, x,this._terrain[x].length, null));
      }
    }

    this.terrainPhysicsCleanup();

    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].regenerate();
      }
    }



    

    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    
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
    const {clamp} = THREE.MathUtils;
    const boundingBox = new THREE.Box3();
    explosionShape.getBoundingBox(boundingBox);

    // Get all the terrain columns that might be modified
    const minX = clamp(Math.floor(boundingBox.min.x), 0, this._terrain.length-1);
    const maxX = clamp(Math.floor(boundingBox.max.x), 0, this._terrain.length-1);

    const minZ = Math.max(0, Math.floor(boundingBox.min.z));
    const floorMaxZ = Math.floor(boundingBox.max.z);
    
    const terrainCols = [];
    for (let x = minX; x <= maxX; x++) {
      const terrainZ = this._terrain[x];
      const maxZ = Math.min(terrainZ.length - 1, floorMaxZ);
      for (let z = minZ; z <= maxZ; z++) {
        terrainCols.push(terrainZ[z]);
      }
    }

    // Blow up the nodes
    // Test all the nodes in this column against explosionShape and remove all nodes inside it
    this.rigidBodyLattice.removeNodesInsideShape(explosionShape);

    // Update the terrain physics for any islands that may have been formed
    const regenTerrainCols = this.terrainPhysicsCleanup();
    for (const terrainCol of terrainCols) {
      regenTerrainCols.add(terrainCol);
    }
    this.regenerateTerrainColumns(regenTerrainCols);
  
    //this.rigidBodyLattice.debugDrawNodes(true); // TODO: Remove this
  }

  regenerateTerrainColumns(regenTerrainColumnSet) {

    let affectedTerrainCols = new Set();
    for (const terrainCol of regenTerrainColumnSet) {
      const otherAffectedCols = terrainCol.regenerate();
      affectedTerrainCols = new Set([...affectedTerrainCols, ...otherAffectedCols]);
    }
    while (affectedTerrainCols.size > 0) {
      const terrainCol = affectedTerrainCols.values().next().value;
      if (!regenTerrainColumnSet.has(terrainCol)) {
        const otherAffectedCols = terrainCol.regenerate();
        for (const otherTc of otherAffectedCols) {
          if (!regenTerrainColumnSet.has(otherTc)) { 
            affectedTerrainCols.add(otherTc);
          }
        }
        regenTerrainColumnSet.add(terrainCol);
      }
      affectedTerrainCols.delete(terrainCol);
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

    geometry.computeBoundingBox();
    const aabb = geometry.boundingBox.clone();
    aabb.applyMatrix4(mesh.matrix);

    // Determine translation/snapping locations and distances
    const closestMin = aabb.min;//.clone().divideScalar(TerrainColumn.SIZE).floor();
    const closestMax = aabb.max;//.clone().divideScalar(TerrainColumn.SIZE).ceil();

    // Bake the translation into the geometry and remove the remaining transform from the mesh
    const translation = new THREE.Vector3(
      MathUtils.roundToDecimal(mesh.position.x, 2), 
      MathUtils.roundToDecimal(mesh.position.y, 2), 
      MathUtils.roundToDecimal(mesh.position.z, 2)
    );
    geometry.translate(translation.x, translation.y, translation.z);
    geometry.computeBoundingBox();

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
    let zOutsideOfTerrain = false;
    for (let x = closestMin.x; x < closestMax.x; x++) {
      if (this._terrain[x] && closestMin.z >= this._terrain[x].length) {
        zOutsideOfTerrain = true;
        break;
      }
    }
    if (closestMax.x <= 0 || closestMin.x >= this._terrain.length || closestMax.z <= 0 || zOutsideOfTerrain) {
      console.log("Debris went outside of the battlefield bounds.");
      console.warn("You need to implement debris dissolution, for the time being it just turns red!");
      mesh.material = new THREE.MeshLambertMaterial({emissive:0xff0000});
      return;
    }

    // We need to reattach the debris to the terrain in all the 
    // correct terrain columns that it now occupies
    const {min, max} = geometry.boundingBox;
    const {clamp} = THREE.MathUtils;
    const startX = clamp(Math.trunc(min.x), 0, this._terrain.length-1);
    const endX = clamp(Math.trunc(max.x), 0, this._terrain.length-1);

    const regenTerrainColumnSet = new Set();
    for (let x = startX; x <= endX; x++) {
      const startZ = clamp(Math.trunc(min.z), 0, this._terrain[x].length-1);
      const endZ = clamp(Math.trunc(max.z), 0, this._terrain[x].length-1);
      for (let z = startZ; z <= endZ; z++) {
        const currTerrainCol = this._terrain[x][z];
        this.rigidBodyLattice.addTerrainColumnDebris(currTerrainCol, debrisObj);
        regenTerrainColumnSet.add(currTerrainCol);
      }
    }

    this.regenerateTerrainColumns(regenTerrainColumnSet);

    // Re-traverse the rigid body node lattice
    this.rigidBodyLattice.traverseGroundedNodes();
    //this.rigidBodyLattice.debugDrawNodes(true);

    // Clean-up
    debrisObj.clearGeometry();
    this.debris.splice(this.debris.indexOf(debrisObj), 1);
  }

  // Do a check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  terrainPhysicsCleanup() {
    const { rigidBodyLattice, terrainGroup, physics, debris} = this;
    rigidBodyLattice.traverseGroundedNodes();

    const islands = rigidBodyLattice.traverseIslands();

    // Build debris for each island and find all the terrain columns associated with each island
    let terrainColumnSet = new Set();
    for (const islandNodeSet of islands) {
      const debrisObj = new Debris(terrainGroup, rigidBodyLattice, islandNodeSet);
      if (debrisObj.mesh !== null) {
        const physObj = debrisObj.addPhysics(physics);
        if (physObj) {
          debris.push(debrisObj);
        }
      }

      for (const node of islandNodeSet) {
        terrainColumnSet.add(node.terrainColumn);
      }
    }

    // The nodes are longer part of the terrain, remove them
    for (const nodeSet of islands) {
      rigidBodyLattice.removeNodes(nodeSet);
    }

    return terrainColumnSet;
  }
}