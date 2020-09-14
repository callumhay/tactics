
import * as THREE from 'three';

class SpatialHashMap {
  constructor(xSize,ySize,zSize) {
    this.xSize = xSize;
    this.ySize = ySize;
    this.zSize = zSize;
    
    this.grid = new Array(xSize*ySize*zSize).fill(null).map(() => []);
  }

  clear() {
    this.grid.forEach(cell => cell.splice(0));
  }

  index(x,y,z) {
    return x*this.xSize*this.xSize + z*this.zSize + y;
  }

  add(pt, data) {
    const {x,y,z} = this._toIndexPt(pt);
    this.grid[this.index(x,y,z)].push(data);
  }

  query(pt, radius=0) {
    if (radius) {
      return this._queryWithRadius(pt, radius);
    }
    const {x,y,z} = this._toIndexPt(pt);
    return this.grid[this.index(x,y,z)];
  }

  _queryWithRadius(pt, radius) {
    const min = this._toIndexPt({x:pt.x-radius, y:pt.y-radius, z:pt.z-radius});
    const max = this._toIndexPt({x:pt.x+radius, y:pt.y+radius, z:pt.z+radius});
    const result = [];
    for (let x = min.x; x <= max.x; x++) {
      for (let z = min.z; z <= max.z; z++) {
        for (let y = min.y; y <= max.y; y++) {
          result.push(...this.grid[this.index(x,y,z)]);
        }
      }
    }
    return result;
  }

  _toIndexPt(pt) {
    const {clamp} = THREE.MathUtils;
    return {
      x: clamp(Math.round(pt.x), 0, this.xSize-1),
      y: clamp(Math.round(pt.y), 0, this.ySize-1),
      z: clamp(Math.round(pt.z), 0, this.zSize-1)
    }
  }

}

export default SpatialHashMap;