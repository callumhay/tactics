import * as THREE from 'three';

const DEBUG_RENDER_ORDER_START_IDX = 10000;

class Debug {
  static get NODE_DEBUG_RENDER_ORDER() { return DEBUG_RENDER_ORDER_START_IDX + 100; }
  static get GRID_RENDER_ORDER() { return DEBUG_RENDER_ORDER_START_IDX + 1; }
  static get TERRAIN_AABB_RENDER_ORDER() { return DEBUG_RENDER_ORDER_START_IDX; }


  static buildDebugBoundingBoxMesh(boundingBox) {
    const {min,max} = boundingBox;
    const edges = new THREE.EdgesGeometry(new THREE.BoxBufferGeometry(max.x - min.x, max.y - min.y, max.z - min.z));
    const mesh = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0xFF00FF, linewidth: 0.1, depthFunc: THREE.AlwaysDepth }));

    const currCenter = new THREE.Vector3();
    boundingBox.getCenter(currCenter)
    mesh.translateX(currCenter.x);
    mesh.translateZ(currCenter.z);
    mesh.translateY(currCenter.y);

    return mesh;
  }

  constructor() {}
}
export default Debug;