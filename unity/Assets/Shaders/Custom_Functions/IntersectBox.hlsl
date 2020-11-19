#ifndef INTERSECTBOX_HLSL_INCLUDED
#define INTERSECTBOX_HLSL_INCLUDED

void IntersectBox_float(
  float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, 
  out float tNear, out float tFar
) {
  // compute intersection of ray with all six bbox planes
  float3 invR = 1.0 / rayDir;
  float3 tbot = invR * (boxMin.xyz - rayOrigin);
  float3 ttop = invR * (boxMax.xyz - rayOrigin);
  // re-order intersections to find smallest and largest on each axis
  float3 tmin = min(ttop, tbot);
  float3 tmax = max(ttop, tbot);
  // find the largest tmin and the smallest tmax
  float2 t0 = max (tmin.xx, tmin.yz);
  tNear = max (t0.x, t0.y);
  t0 = min (tmax.xx, tmax.yz);
  tFar = min (t0.x, t0.y);
}

void IsInsideBoxClipOutside_float(float3 testPoint, float3 aabbMin, float3 aabbMax, out bool inside) {
  float epsilon = 1e-6f;
  if (testPoint.x <= aabbMax.x+epsilon && 
      testPoint.y <= aabbMax.y+epsilon && 
      testPoint.z <= aabbMax.z+epsilon &&
      testPoint.x >= aabbMin.x-epsilon &&
      testPoint.y >= aabbMin.y-epsilon &&
      testPoint.z >= aabbMin.z-epsilon) {
    inside = 1;
  } else { 
    inside = 0; clip(-1);
  }
}

#endif // INTERSECTBOX_HLSL_INCLUDED