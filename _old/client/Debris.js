import * as THREE from 'three';

import GeometryUtils from '../GeometryUtils';

class Debris {
  constructor(terrainGroup, landingRanges) {
    const geometries = [];
    this.density = 0;
    this.terrainGroup = terrainGroup;

    for (const landingRange of landingRanges) {
      const {mesh, material} = landingRange;
      const {geometry, matrixWorld} = mesh;

      geometry.applyMatrix4(matrixWorld); // Geometry needs to be placed into the terrain space
      geometries.push(geometry);

      this.density += material.density;

      // Right now we just assign the material to a landing range materal
      // Cannon doesn't support multiple materials in a body
      this.material = material;
    }
    this.density /= (landingRanges.size ? landingRanges.size : landingRanges.length);

    const csgGeometry = CSG.union(geometries);
    const geometry = CSG.BufferGeometry(csgGeometry);
    GeometryUtils.roundVertices(geometry);
    geometry.computeBoundingBox();

    const {boundingBox} = geometry;
    const bbCenter = new THREE.Vector3();
    boundingBox.getCenter(bbCenter);
    geometry.translate(-bbCenter.x, -bbCenter.y, -bbCenter.z);

    this._regenerate(geometry, bbCenter);
  }

  subtractGeometry(subtractGeom) {
    const {geometry} = this.mesh;
    const csgGeometry = CSG.subtract([geometry, subtractGeom]);
    const newGeometry = CSG.BufferGeometry(csgGeometry);
    GeometryUtils.roundVertices(newGeometry);
    geometry.dispose();
    this.mesh.geometry = newGeometry;
    newGeometry.computeBoundingBox();
  }

  clearGeometry() {
    if (this.mesh) {
      this.mesh.geometry.dispose();
      this.terrainGroup.remove(this.mesh);
    }
  }

  addPhysics(physics) {
    const physicsConfig = {
      gameObject: this,
      mesh: this.mesh,
      material: this.material.cannonMaterial,
      density: this.density,
    };
    physics.addDebris(physicsConfig);
  }

  _regenerate(geometry, translation = new THREE.Vector3(0, 0, 0)) {
    this.clearGeometry();
    this.mesh = new THREE.Mesh(geometry, this.material.threeMaterial);
    this.mesh.castShadow = true;
    this.mesh.receiveShadow = false;
    this.mesh.translateX(translation.x);
    this.mesh.translateY(translation.y);
    this.mesh.translateZ(translation.z);
    this.terrainGroup.add(this.mesh);
    this.mesh.updateMatrixWorld();
  }
}

export default Debris;