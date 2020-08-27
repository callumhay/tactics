import * as THREE from 'three';

import Debug from '../debug';
import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice';
import Debris from './Debris';
import GameTypes from './GameTypes';
import GameMaterials from './GameMaterials';

export default class Battlefield {
  static get MAX_SIZE() { return TerrainColumn.SIZE * 200; }
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



    // Regenerate the terrain
    for (let x = 0; x < this._terrain.length; x++) {
      while (this._terrain[x].length !== maxZLength) {
        this._terrain[x].push(new TerrainColumn(this, x,this._terrain[x].length, null));
      }
    }
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].regenerate();
      }
    }

    const terrainSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(-terrainSize / 2, 0, -terrainSize / 2);

    this.terrainPhysicsCleanup();

    // Traverse the lattice, find anything that might not be connected to the ground, 
    // remove it from the terrain and turn it into a physical object
    
    this.rigidBodyLattice.debugDrawNodes(true);
  }

  clear() {
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].clear();
      }
    }
    this._terrain = [];
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
    const regenTerrainCols = this.terrainPhysicsCleanup(false);
    for (const terrainCol of terrainCols) {
      regenTerrainCols.add(terrainCol);
      const adjTerrainCols = this.getAdjacentTerrainColumns(terrainCol);
      for (const adjTerrainCol of adjTerrainCols) {
        regenTerrainCols.add(adjTerrainCol);
      }
    }

    // Rebuild the geometry for all the affected TerrainColumns
    for (const terrainCol of regenTerrainCols) {
      terrainCol.regenerate();
    }

    this.rigidBodyLattice.debugDrawNodes(true); // TODO: Remove this
  }

  getTerrainColumn(xIdx, zIdx) {
    if (this._terrain[xIdx]) {
      return this._terrain[xIdx][zIdx] || null;
    }
    return null;
  }
  getAdjacentTerrainColumns(terrainColumn) {
    const {xIndex, zIndex} = terrainColumn;
    const result = [];

    const increments = [
      [-1,-1], [-1,0], [-1,1], [1,-1], [1,0], [1,1], [0,-1], [0,1]
    ];

    for (const inc of increments) {
      const [xInc,zInc] = inc;
      const adjTc = this.getTerrainColumn(xIndex+xInc, zIndex+zInc);
      if (adjTc) { result.push(adjTc); }
    }

    return result;
  }

  convertDebrisToTerrain(physicsObj) {

  }

  // Do a check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  terrainPhysicsCleanup(regenerateColumns=true) {
    const { rigidBodyLattice, terrainGroup, physics, debris} = this;
    rigidBodyLattice.traverseGroundedNodes();
    let islands = rigidBodyLattice.traverseIslands();

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
        terrainColumnSet = new Set([...terrainColumnSet, ...node.attachedTerrainCols]);
      }
    }

    // The nodes are longer part of the terrain, remove them
    for (const nodeSet of islands) {
      rigidBodyLattice.removeNodes(nodeSet);
    }

    // If the node set is less than 4 points then we ignore it - you can't compute
    // a proper convex hull from it... also the geometry would be insignificant, regardless
    islands = islands.filter(nodeSet => nodeSet.count >= 4);

    // The terrain columns that we need to regenerate need to include all 
    // adjacent terrain columns to the ones effected
    const adjTerrainCols = [];
    for (const terrainColumn of terrainColumnSet) {
      adjTerrainCols.push.apply(adjTerrainCols, this.getAdjacentTerrainColumns(terrainColumn));
    }
    terrainColumnSet = new Set([...terrainColumnSet, ...adjTerrainCols]);

    if (regenerateColumns) {
      // Regenerate the geometry for the terrain columns
      for (const terrainColumn of terrainColumnSet) {
        terrainColumn.regenerate();
      }
    }

    return terrainColumnSet;
  }
}