import * as THREE from 'three';
import {assert} from 'chai'

class LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, terrainColumn, material) {
    assert(xIdx >= 0, "xIdx of a LatticeNode must be at least zero.");
    assert(yIdx >= 0, "yIdx of a LatticeNode must be at least zero.");
    assert(zIdx >= 0, "zIdx of a LatticeNode must be at least zero.");

    this.id = id;
    this.xIdx = xIdx;
    this.zIdx = zIdx;
    this.yIdx = yIdx;
    this.pos = pos;
    this.terrainColumn = terrainColumn || null;
    this.material = material;
  }
  get density() {
    return this.material.density;
  }
  debugColour() {
    return this.terrainColumn ? this.terrainColumn.debugColour() : new THREE.Color(0,0,0);
  }
}

/**
 * A lattice node that either exists or does not, if it exists it counts as a 1
 * towards marching cubes, if it does not exist it counts as a 0.
 */
export class TerrainLatticeNode extends LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, terrainColumn, material) {
    super(id, xIdx, zIdx, yIdx, pos, terrainColumn, material);
    this.grounded = false;
  }
}

export class WaterLatticeNode extends LatticeNode {
  constructor(id, xIdx, zIdx, yIdx, pos, terrainColumn, material, volume) {
    super(id, xIdx, zIdx, yIdx, pos, terrainColumn, material);
    this.volume = volume;
  }
}

