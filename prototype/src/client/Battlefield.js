import * as THREE from 'three';
import { assert } from 'chai';

import Debug from '../debug';
import MathUtils from '../MathUtils';

import TerrainColumn from './TerrainColumn';
import TerrainLattice from './TerrainLattice';
import LiquidLattice from './LiquidLattice';
import Debris from './Debris';
import GameTypes from './GameTypes';
import GameMaterials from './GameMaterials';

class Battlefield {
  static get MAX_SIZE() { return TerrainColumn.SIZE * 32; }
  static get HALF_MAX_SIZE() { return Battlefield.MAX_SIZE / 2; }
  static get MAX_HEIGHT() { return TerrainColumn.SIZE * 16; }
  static get HALF_MAX_HEIGHT() { return Battlefield.MAX_HEIGHT/2; }

  constructor(scene, model) {
    this._scene = scene;
    this._model = model;

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
    this.terrainNodeLattice = new TerrainLattice(this.terrainGroup);
    this.waterNodeLattice   = new LiquidLattice(this.terrainGroup, this._model.gpuManager);

    const bedrockMat = GameMaterials.materials[GameMaterials.MATERIAL_TYPE_BEDROCK];
    bedrockMat.three.map.repeat.set(Battlefield.HALF_MAX_SIZE, Battlefield.HALF_MAX_SIZE);
    const bedrockGeom = new THREE.BoxBufferGeometry(Battlefield.MAX_SIZE, Battlefield.MAX_SIZE, TerrainColumn.SIZE);
    bedrockGeom.translate(0, 0, -TerrainColumn.HALF_SIZE);
    this.bedrockMesh = new THREE.Mesh(bedrockGeom, bedrockMat.three);
    this.bedrockMesh.receiveShadow = true;
    const {physics} = this._model;
    physics.addBedrock({ 
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

  //xSizeUnits() { return TerrainColumn.SIZE*this._terrain.length; }
  //zSizeUnits() { return TerrainColumn.SIZE*this._terrain}

  setTerrain(terrain) {
    this._clearTerrainColumns();
    this._terrain = terrain;

    // The map must be square... make sure of this
    let maxZLength = 0;
    for (let x = 0; x < this._terrain.length; x++) {
      maxZLength = Math.max(maxZLength, this._terrain[x].length);
    }
    const terrainXSize = terrain.length * TerrainColumn.SIZE;
    const terrainZSize = maxZLength * TerrainColumn.SIZE;
    this.waterNodeLattice.initNodeSpace(terrainXSize, terrainZSize, Battlefield.MAX_HEIGHT);

    this.terrainGroup.position.set(-terrainXSize / 2, 0, -terrainXSize / 2);
    for (let x = 0; x < this._terrain.length; x++) {
      while (this._terrain[x].length !== maxZLength) {
        this._terrain[x].push(new TerrainColumn(this, x, this._terrain[x].length, null));
      }
    }

    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    this.terrainPhysicsCleanup();

    // Regenerate everything to find gaps in meshes
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].regenerate();
      }
    }

    //this.terrainNodeLattice.initNodeSpace(terrainXSize, terrainZSize, Battlefield.MAX_HEIGHT); // SLOW...
    
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
    this.waterNodeLattice.update(dt);
  }

  // NOTE: We assume that the explosionShape is in the same coordinate space as the terrain
  blowupTerrain(explosionShape) {
    // Blow up the nodes
    // Test all the nodes in this column against explosionShape and remove all nodes inside it
    const removedNodes = this.terrainNodeLattice.removeNodesInsideShape(explosionShape);
    
    // Build a set of TerrainColumns and associated cube IDs for all the removed nodes 
    // so that we know what to update
    const terrainCols = new Set();
    for (const node of removedNodes) {
      const { terrainColumn } = node;
      assert(terrainColumn);
      terrainCols.add(terrainColumn);
      
      // Add all of the neighbours of the column as well
      const {xIndex, zIndex} = terrainColumn;
      const xIdxP1 = Math.min(xIndex+1, this._terrain.length-1);
      const xIdxM1 = Math.max(xIndex-1, 0);
      const zIdxM1 = Math.max(zIndex-1, 0);

      // x-axis neighbours
      terrainCols.add(this._terrain[xIdxP1][zIndex]);
      terrainCols.add(this._terrain[xIdxM1][zIndex]);
      // z-axis neighbours
      terrainCols.add(this._terrain[xIndex][Math.min(zIndex+1, this._terrain[xIndex].length-1)]);
      terrainCols.add(this._terrain[xIndex][zIdxM1]);
      // Corner neighbours
      terrainCols.add(this._terrain[xIdxP1][zIdxM1]);
      terrainCols.add(this._terrain[xIdxM1][zIdxM1]);
      terrainCols.add(this._terrain[xIdxP1][Math.min(zIndex+1, this._terrain[xIdxP1].length-1)]);
      terrainCols.add(this._terrain[xIdxM1][Math.min(zIndex+1, this._terrain[xIdxM1].length-1)]);
    }

    // Update the terrain physics for any islands that may have been formed
    const otherTCs = this.terrainPhysicsCleanup(terrainCols);
    for (const tc of otherTCs) {
      terrainCols.add(tc);
    }

    this.regenerateTerrainColumns(terrainCols);
    //this.terrainNodeLattice.debugDrawNodes(true); // TODO: Remove this
  }

  regenerateTerrainColumns(terrainCols) {
    for (const terrainCol of terrainCols.values()) {
      terrainCol.regenerate();
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
    const affectedTCs = new Set();
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
              affectedTCs.add(terrainColumn);
              const addedNodes = this.terrainNodeLattice.addTerrainColumnDebris(terrainColumn, debrisObj);
              numAddedNodes += addedNodes.length;
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
    this.regenerateTerrainColumns(affectedTCs);

    // Update the water
    this.waterNodeLattice.setTerrainNodes(this.terrainNodeLattice);

    // Clean-up
    debrisObj.clearGeometry();
    this.debris.splice(this.debris.indexOf(debrisObj), 1);
  }

  // Do a check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  // The given affectedTerrainCols are the ones to examine and update, if a list isn't given then
  // ALL terrainColumns are examined.
  terrainPhysicsCleanup(affectedTerrainCols=[]) {
    const {terrainNodeLattice, waterNodeLattice, terrainGroup, debris} = this;
    const {physics} = this._model;

    terrainNodeLattice.traverseGroundedNodes(affectedTerrainCols);
    const islands = terrainNodeLattice.traverseIslands();

    const affectedTCs = new Set();
    for (const islandNodeSet of islands) {
      const debrisObj = new Debris(terrainGroup, terrainNodeLattice, islandNodeSet);
      if (debrisObj.mesh !== null) {
        const physObj = debrisObj.addPhysics(physics);
        if (physObj) {
          debris.push(debrisObj);
        }
      }

      for (const node of islandNodeSet) {
        const {terrainColumn} = node;
        assert(terrainColumn);
        affectedTCs.add(terrainColumn);
      }
    }

    // The nodes are longer part of the terrain, remove them
    for (const nodeSet of islands) {
      terrainNodeLattice.removeNodes(nodeSet);
    }
    waterNodeLattice.setTerrainNodes(terrainNodeLattice);

    return affectedTCs;
  }

}

export default Battlefield;