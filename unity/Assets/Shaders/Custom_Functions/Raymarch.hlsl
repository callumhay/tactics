#ifndef RAYMARCH_HLSL_INCLUDED
#define RAYMARCH_HLSL_INCLUDED

#include "IntersectBox.hlsl"


void Raymarch_float(
  Texture3D volumeTex, SamplerState volumeSampler, 
  float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, 
  int3 borderFront, int3 borderBack, float resolution,
  out float4 colour
) {

  float tNear = 0; float tFar = 0;
  IntersectBox_float(rayOrigin, rayDir, boxMin, boxMax, tNear, tFar);
  clip(tFar - tNear); // Check for an intersection hit (negative values mean there was no hit so we clip them)

  tNear = max(0, tNear); // If the camera is inside the volume then just start/end at the camera
  // Calculate the intersection points of the eye ray to the box
  float3 pNear = rayOrigin + rayDir*tNear;
  float3 pFar  = rayOrigin + rayDir*tFar;
  float3 farToNearVec = pNear-pFar;

  // Calculate box min to max length and unit vector
  float3 minToMaxUnitVec = boxMax-boxMin;
  float boxMinMaxLen = length(minToMaxUnitVec) + 1e-10;
  minToMaxUnitVec /= boxMinMaxLen;

  // Figure out the number of voxels along the ray within the box, this will be the basis
  // for the number of steps we march along the ray
  float3 borderVec = borderFront + borderBack;
  float voxelSize  = (boxMax.x-boxMin.x) / (float)(resolution-borderVec.x);
  int numVoxels  = (int)floor(length(farToNearVec) / voxelSize);
  int maxSteps   = clamp(numVoxels*2, 2, 2*resolution);
  float3 stepVec = farToNearVec / (maxSteps-1.0);

  float3 resVec = float3(resolution, resolution, resolution);
  float3 resNoBorderVec = resVec-borderVec;
  

  // March back-to-front along the ray, sample and accumulate color at each step
  float3 currPos = pFar;
  //colour = float4((borderFront + dot(resNoBorderVec, (currPos-boxMin)/boxMinMaxLen)) / (float)resolution, 1);
  
  colour = float4(0,0,0,0);
  for (int i = 0; i < maxSteps; i++) {
    float3 uvw = (borderFront / (float)resolution) + (currPos-boxMin)/boxMinMaxLen;
    float4 sampleColour = SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvw);
    colour = sampleColour.a*sampleColour + (1.0-sampleColour.a)*colour;
    if (colour.a >= 1) { break; }
    currPos += stepVec;
  }

  /*
  
  
  colour = float4(0,0,0,0);
  for (int i = 0; i < maxSteps; i++) {
    float4 sample = VOLUMEFUNC(P);
    colour = s.a*s + (1.0-s.a)*colour;
    currPos += stepVec;
  }
  */

  //colour = float4(abs(pFar-pNear) / 10.0,1);
}



#endif // RAYMARCH_HLSL_INCLUDED