#ifndef NODE_DEFS_HLSL_INCLUDED
#define NODE_DEFS_HLSL_INCLUDED

#define SOLID_NODE_TYPE 1
#define EMPTY_NODE_TYPE 0

#define NODE_VOL_IDX 0
#define NODE_TYPE_IDX 1
#define NODE_SETTLED_IDX 2

#define SETTLED_NODE 1
#define UNSETTLED_NODE 0

#define ISOVAL_CUTOFF 0.5

struct NodeFlowInfo {
  float flowL; float flowR;
  float flowB; float flowT;
  float flowD; float flowU;
};

struct LiquidNodeUpdate {
  float terrainIsoVal;
  float liquidVolume;
  float3 velocity;
  float isDiff;
};

float4 buildNode(int type, float liquidVol, int settled) {
  float4 node = float4(0,0,0,0);
  node[NODE_TYPE_IDX]    = type;
  node[NODE_VOL_IDX]     = liquidVol;
  node[NODE_SETTLED_IDX] = settled;
  return node;
}
int nodeType(float4 node) { return (int)node[NODE_TYPE_IDX]; }
float nodeLiquidVolume(float4 node) { return node[NODE_VOL_IDX]; }
int nodeSettled(float4 node) { return (int)node[NODE_SETTLED_IDX]; }

#endif // NODE_DEFS_HLSL_INCLUDED