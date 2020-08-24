
import * as THREE from 'three';

export class ExplosionSphere {
  constructor(center, radius) {
    this.shape = new THREE.Sphere(center, radius);
  }

  applyMatrix4(matrix) {
    this.shape.applyMatrix4(matrix);
  }

  containsPoint(pt) {
    return this.shape.containsPoint(pt);
  }
}