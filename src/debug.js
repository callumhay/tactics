const DEBUG_RENDER_ORDER_START_IDX = 10000;

class Debug {
  static get NODE_DEBUG_RENDER_ORDER() { return DEBUG_RENDER_ORDER_START_IDX + 100; }
  static get TERRAIN_AABB_RENDER_ORDER() { return DEBUG_RENDER_ORDER_START_IDX; }

  constructor() {}
}
export default Debug;