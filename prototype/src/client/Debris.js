import * as THREE from 'three';

import GeometryUtils from '../GeometryUtils';
import MarchingCubes from './MarchingCubes';
import { assert } from 'chai';

const tempVec3 = new THREE.Vector3();

class Debris {
  constructor(terrainGroup, terrainNodeLattice, nodes) {
    this.mesh = null;
    this.physicsObj = null;
    this.density = 0;
    this.terrainGroup = terrainGroup;
    
    // Convert the nodes into geometry
    this._regenerate(terrainNodeLattice, nodes);
  }

  clearGeometry() {
    if (this.mesh) {
      this.mesh.geometry.dispose();
      this.terrainGroup.remove(this.mesh);
    }
  }

  addPhysics(physics) {
    if (this.physicsObj) {
      console.warn("A physics object has already been added for this debris!");
      return;
    }
    if (!this.mesh) {
      console.warn("Trying to make a physics object out of a null mesh.");
      return;
    }

    const physicsConfig = {
      gameObject: this,
      mesh: this.mesh,
      material: this.material.cannon,
      density: this.density,
    };
    this.physicsObj = physics.addDebris(physicsConfig);
    return this.physicsObj;
  }

  _regenerate(terrainNodeLattice, nodes) {
    this.clearGeometry();

    // Calculate the density based on node materials
    this.density = 0;
    for (const node of nodes) {
      this.density += node.density;
    }
    this.density /= nodes.size;
    
    // TODO: 
    // 1. Group the geometry by material
    // 2. Create separate rigid bodies with different materials??
    const nodeCubeCells = terrainNodeLattice.getNodeIslandCubeCells(nodes);
    const cubeIdsToTriMap = [];
    for (const nodeCubeCell of nodeCubeCells) {
      const {id, corners} = nodeCubeCell;
      const triMatObjs = MarchingCubes.polygonizeNodeCubeCell(corners);
      cubeIdsToTriMap[id] = triMatObjs;
    }

    // Create the debris geometry, center it (so that we can do physics stuff cleanly) and move the translation over to the mesh
    const {geometry, materials} = GeometryUtils.buildBufferGeometryFromCubeTriMap(cubeIdsToTriMap);
    // If there isn't enough geometry in this object to make a convex shape then
    // this isn't going to be valid, exit now and leave the geometry/mesh null
    if (geometry.getAttribute('position').count < 4) {
      geometry.dispose();
      return;
    }
    assert(materials.length > 0);
    this.material = materials[0]; // TODO: For now we just use the first material for the physics

    this.mesh = new THREE.Mesh(geometry, materials.map(m => m.debrisThree));
    GeometryUtils.centerMeshGeometryToTranslation(this.mesh);
    this.mesh.castShadow = true;
    this.mesh.receiveShadow = false;
    this.terrainGroup.add(this.mesh);
  }
}

export default Debris;