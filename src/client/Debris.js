import * as THREE from 'three';

import GeometryUtils from '../GeometryUtils';
import MarchingCubes from './MarchingCubes';

const tempVec3 = new THREE.Vector3();

class Debris {
  constructor(terrainGroup, rigidBodyLattice, nodes) {
    this.mesh = null;
    this.physicsObj = null;
    this.density = 0;
    this.terrainGroup = terrainGroup;
    
    // Convert the nodes into geometry
    this._regenerate(rigidBodyLattice, nodes);
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

  _regenerate(rigidBodyLattice, nodes) {
    this.clearGeometry();

    // Calculate the density based on node materials
    this.density = 0;
    this.material = nodes.values().next().value.material;
    for (const node of nodes) {
      this.density += node.density;
    }
    this.density /= nodes.size;
    
    // TODO: 
    // 1. Group the geometry by material
    // 2. Create separate rigid bodies with different materials??
    const nodeCubeCells = rigidBodyLattice.getNodeIslandCubeCells(nodes);
    const triangles = [];
    for (const nodeCubeCell of nodeCubeCells) {
      const {corners} = nodeCubeCell;
      MarchingCubes.polygonizeNodeCubeCell(corners, triangles);
    }

    // Create the debris geometry, center it (so that we can do physics stuff cleanly) and move the translation over to the mesh
    const geometry = GeometryUtils.buildBufferGeometryFromTris(triangles);
    // If there isn't enough geometry in this object to make a convex shape then
    // this isn't going to be valid, exit now and leave the geometry/mesh null
    if (geometry.getAttribute('position').count < 4) {
      return;
    }

    geometry.computeBoundingBox();
    const {boundingBox} = geometry;
    boundingBox.getCenter(tempVec3);
    geometry.translate(-tempVec3.x, -tempVec3.y, -tempVec3.z);

    this.mesh = new THREE.Mesh(geometry, new THREE.MeshLambertMaterial({color:0xcccccc}));
    this.mesh.translateX(tempVec3.x);
    this.mesh.translateY(tempVec3.y);
    this.mesh.translateZ(tempVec3.z);
    this.mesh.updateMatrixWorld();

    this.mesh.castShadow = true;
    this.mesh.receiveShadow = false;
    this.terrainGroup.add(this.mesh);
  }
}

export default Debris;