import * as THREE from 'three';

import Debug from '../debug';
import MathUtils from '../MathUtils';

import TerrainColumn from './TerrainColumn';
import RigidBodyLattice from './RigidBodyLattice';
import Debris from './Debris';
import GameTypes from './GameTypes';
import GameMaterials from './GameMaterials';

export default class Battlefield {
  static get MAX_SIZE() { return TerrainColumn.SIZE * 200; }
  static get MAX_HEIGHT() { return TerrainColumn.SIZE * 25; }

  constructor(scene, physics) {
    this._scene = scene;
    this.physics = physics;

    this.debris = [];

    this.sunLight = new THREE.DirectionalLight(0xffffff, 0.2);
    this.sunLight.castShadow = false;
    this.sunLight.position.set(0, 1, -1);
    this.sunLight.position.normalize();
    this._scene.add(this.sunLight);

    const skyColor = 0xB1E1FF;  // light blue
    const groundColor = 0xB97A20;  // brownish orange
    this.skyLight = new THREE.HemisphereLight(skyColor, groundColor, 0.5);
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
    this.bedrockMesh = new THREE.Mesh(bedrockGeom, bedrockMat.threeMaterial);
    this.bedrockMesh.receiveShadow = true;
    this.physics.addBedrock({ 
      gameType: GameTypes.BEDROCK,
      gameObject: null, 
      mesh: this.bedrockMesh, 
      material: bedrockMat.cannonMaterial
    });
    this.terrainGroup.add(this.bedrockMesh);

    // TODO: Debug routine?
    //const grid = new THREE.GridHelper(Battlefield.MAX_SIZE, Battlefield.MAX_SIZE, 0x00FF00);
    //grid.renderOrder = Debug.GRID_RENDER_ORDER;
    //grid.material.depthFunc = THREE.AlwaysDepth;
    //this.terrainGroup.add(grid);

    this._scene.add(this.terrainGroup);
  }

  setTerrain(terrain) {
    this._clearTerrain();
    this._terrain = terrain;
    const terrainSize = terrain.length * TerrainColumn.SIZE;
    this.terrainGroup.position.set(-terrainSize / 2, 0, -terrainSize / 2);
    this._buildRigidbodyLattice();
    this.terrainPhysicsCleanup();
  }

  _buildRigidbodyLattice() {
    this.rigidBodyLattice.clear();
    this.rigidBodyLattice.buildFromTerrain(this._terrain);
  }

  clear() {
    this._clearTerrain();
  }
  _clearTerrain() {
    if (!this._terrain) { return; }
    for (let x = 0; x < this._terrain.length; x++) {
      for (let z = 0; z < this._terrain[x].length; z++) {
        this._terrain[x][z].clear();
      }
    }
    this._terrain = [];
  }

  // NOTE: We assume that the subtractGeometry is in the same coord space as the terrain
  blowupTerrain(subtractGeometry) {
    subtractGeometry.computeBoundingBox();
    const {boundingBox} = subtractGeometry;
    const {clamp} = THREE.MathUtils;

    // Get all the terrain columns that might be modified
    const minX = clamp(Math.floor(boundingBox.min.x), 0, this._terrain.length-1);
    const maxX = clamp(Math.floor(boundingBox.max.x), 0, this._terrain.length-1);

    const minZ = Math.max(0, Math.floor(boundingBox.min.z));
    const floorMaxZ = Math.floor(boundingBox.max.z);
    
    const terrainCols = [];
    for (let x = minX; x <= maxX; x++) {
      const terrainZ = this._terrain[x];
      const maxZ = Math.min(terrainZ.length - 1, floorMaxZ);
      if (maxZ >= 0 && minZ < terrainZ.length) {
        for (let z = minZ; z <= maxZ; z++) {
          terrainCols.push(terrainZ[z]);
        }
      }
    }

    for (const terrainCol of terrainCols) {
      terrainCol.blowupTerrain(subtractGeometry);
    }
  }

  convertDebrisToTerrain(debrisPhysObj) {
    const { gameType, gameObject: debrisObj, mesh } = debrisPhysObj;
    if (gameType !== GameTypes.DEBRIS) {
      console.error("Attempting to convert invalid game type back into terrain, ignoring.");
      return;
    }

    //const DIST_SNAP = TerrainColumn.SIZE / 3;
    const ANGLE_SNAP_EPSILON = 20 * Math.PI / 180;
    const PI_OVER_2 = Math.PI / 2;

    mesh.updateMatrixWorld();

    const { geometry, rotation, position } = mesh;
    // We need to clean up the rotation so that the box lies cleanly in the terrain grid
    for (const coord of ['x', 'y', 'z']) {
      // Snap the rotation to the nearest 90 degree angle
      const truncDiv = Math.trunc(rotation[coord] / PI_OVER_2);
      const snapped = truncDiv * PI_OVER_2;
      if (Math.abs(rotation[coord] - snapped) <= ANGLE_SNAP_EPSILON) {
        rotation[coord] = snapped;
      }
    }

    // Bake the snapped rotation of the mesh into the geometry
    const rotationMatrix = new THREE.Matrix4();
    rotationMatrix.makeRotationFromEuler(rotation);
    geometry.applyMatrix4(rotationMatrix);
    rotation.set(0,0,0);
    mesh.updateMatrix();

    const { matrix } = mesh;
    geometry.computeBoundingBox();
    const aabb = geometry.boundingBox.clone();
    aabb.applyMatrix4(matrix);

    //const temp = new THREE.Box3Helper(aabb);
    //this.terrainGroup.add(temp);

    const closestMin = aabb.min.clone().divideScalar(TerrainColumn.SIZE).round();
    const closestMax = aabb.max.clone().divideScalar(TerrainColumn.SIZE).round();

    // Check to see if the mesh is inside the playable area anymore
    if ((closestMax.x <= 0 || closestMin.x >= this._terrain.length)) {
      let zOutsideOfTerrain = true;
      for (let x = closestMin.x; x < closestMax.x; x++) {
        if (this._terrain[x] && closestMin.z < this._terrain[x].length) {
          zOutsideOfTerrain = false;
          break;
        }
      }
      if (closestMax.z <= 0 || zOutsideOfTerrain) {
        console.log("Debris went outside of the battlefield bounds.");
        console.warn("You need to implement debris dissolution, for the time being it just turns red!");
        mesh.material = new THREE.MeshBasicMaterial({color:0xff0000});
        return;
      }
    }

    // Snap the x and z coordinates to the nearest terrain corner
    const distMinX = Math.abs(aabb.min.x - closestMin.x);
    const distMinZ = Math.abs(aabb.min.z - closestMin.z);
    const distMaxX = Math.abs(aabb.max.x - closestMax.x);
    const distMaxZ = Math.abs(aabb.max.z - closestMax.z);

    const translation = new THREE.Vector3();
    if (distMinX < distMaxX) { //&& distMinX <= DIST_SNAP) {
      translation.x = closestMin.x - aabb.min.x;
    }
    else { //if (distMaxX <= DIST_SNAP) {
      translation.x = closestMax.x - aabb.max.x;
    }
    if (distMinZ < distMaxZ) { //&& distMinZ <= DIST_SNAP) {
      translation.z = closestMin.z - aabb.min.z;
    }
    else { //if (distMaxZ <= DIST_SNAP) {
      translation.z = closestMax.z - aabb.max.z;
    }

    // If the bottom y coordinate is close to the origin trunc it
    if (Math.abs(aabb.min.y) <= TerrainColumn.EPSILON) {
      translation.y = closestMin.y - aabb.min.y;
    }

    // Bake the new translation into the geometry and remove the remaining transform from the mesh
    geometry.translate(
      MathUtils.roundToDecimal(mesh.position.x + translation.x, 2), 
      MathUtils.roundToDecimal(mesh.position.y + translation.y, 2), 
      MathUtils.roundToDecimal(mesh.position.z + translation.z, 2)
    );
    geometry.computeBoundingBox();

    mesh.translateX(-mesh.position.x);
    mesh.translateY(-mesh.position.y);
    mesh.translateZ(-mesh.position.z);
    mesh.updateMatrix();

    // We need to reattach the debris to the terrain in all the 
    // correct terrain columns and landing ranges that it now occupies
    for (let x = closestMin.x; x < closestMax.x; x++) {
      for (let z = closestMin.z; z < closestMax.z && x < this._terrain.length; z++) {
        const currTerrainCol = this._terrain[x][z];
        if (currTerrainCol) {
          //console.log(`Terrain column: ${currTerrainCol}`);
          currTerrainCol.attachDebris(debrisObj);
        }
      }
    }
    
    // TODO

    

    // Clean-up
    this.debris.splice(this.debris.indexOf(debrisObj), 1);
  }

  // Do a check for floating "islands" (i.e., terrain blobs that aren't connected to the ground), 
  // remove them from the terrain and turn them into physics objects
  terrainPhysicsCleanup() {
    const { rigidBodyLattice, physics, debris} = this;
    const islands = rigidBodyLattice.traverseIslands();

    // Find all the landing ranges associated with each island
    const islandLandingRanges = [];
    for (const islandNodeSet of islands) {
      let landingRangeSet = new Set();
      for (const node of islandNodeSet) {
        landingRangeSet = new Set([...landingRangeSet, ...node.getLandingRanges()]);
      }
      islandLandingRanges.push(landingRangeSet);
    }

    // Merge together all the landing ranges in each island and turn them into detached
    // geometry that become dynamic physics objects
    for (const landingRangeSet of islandLandingRanges) {
      for (const landingRange of landingRangeSet) {
        // Make sure there are no longer any nodes associated with the detached range
        rigidBodyLattice.removeNodesInsideLandingRange(landingRange);
      }

      // We'll need to convert the landingRanges into debris
      const debrisObj = new Debris(this.terrainGroup, landingRangeSet);
      debris.push(debrisObj);
      debrisObj.addPhysics(physics);

      // Clean-up the landing ranges, they are now debris and will no longer exist
      for (const landingRange of landingRangeSet) {
        const {terrainColumn} = landingRange;
        landingRange.terrainColumn.detachLandingRange(landingRange);
        landingRange.clear(terrainColumn);
      }
    }

    rigidBodyLattice.debugDrawNodes(true);
  }

}