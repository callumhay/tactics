#ifndef RAYMARCH_HLSL_INCLUDED
#define RAYMARCH_HLSL_INCLUDED

#include "IntersectBox.hlsl"
#define MIN_ITERATIONS 8
#define MAX_ITERATIONS 256

void DoSample(Texture3D volumeTex, SamplerState volumeSampler, float3 uvw, float weight, float opacityMultiplier, inout float4 colour) {
  float4 sampleColour = weight * SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvw);
  sampleColour.a *= opacityMultiplier;
  // Back to front rendering
  colour.rgb = (1-sampleColour.a)*colour.rgb + sampleColour.a * sampleColour.rgb;
  colour.a = (1-sampleColour.a)*colour.a + sampleColour.a;
}

float3 calcUVW(int3 borderFront, float resolution, float3 currPos, float3 boxMin, float boxMinMaxLen) {
  return borderFront / resolution + (currPos-boxMin) / boxMinMaxLen;
}

void Raymarch_float(
  Texture3D volumeTex, SamplerState volumeSampler, 
  Texture2D jitterTex, SamplerState jitterSampler,
  float eyeDepth, float2 pos, float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, 
  int3 borderFront, int3 borderBack, float resolution, float opacityMultiplier,
  out float4 colour
) {

  float tNear = 0; float tFar = 0;
  IntersectBox_float(rayOrigin, rayDir, boxMin, boxMax, tNear, tFar);
  
  tNear = max(0, tNear); // If the camera is inside the volume then just start/end at the camera
  tFar = min(tFar, eyeDepth); // We only march as far as the end of the volume or the front of the closest object in the depth buffer

  clip(tFar - tNear); // Check for an intersection hit (negative values mean there was no hit so we clip them)
  
  // Calculate the intersection points of the eye ray to the box
  float3 pNear = rayOrigin + rayDir*tNear;
  float3 pFar  = rayOrigin + rayDir*tFar;
  float3 farToNearVec = pNear-pFar;
  float farToNearLen = length(farToNearVec);

  // Calculate box min to max length and unit vector
  float3 minToMaxVec = boxMax-boxMin;
  float boxMinMaxLen = length(minToMaxVec);

  // Figure out the number of voxels along the ray within the box, this will be the basis
  // for the number of steps we march along the ray
  float3 borderVec = borderFront + borderBack;
  float3 resVec = float3(resolution, resolution, resolution);
  float3 resNoBorderVec = resVec-borderVec;
  float resNoBorderLen = length(resNoBorderVec);

  // Perform a jitter along the ray, the jitter must not exceed half the voxel unit distance along the ray
  float jitterOffset = SAMPLE_TEXTURE2D(jitterTex, jitterSampler, pos.xy).r; // Random values in [0,1)
  
  float bestSamples = 2.0 * (farToNearLen / boxMinMaxLen) * resNoBorderLen;
  float fSamples = clamp(bestSamples, MIN_ITERATIONS, MAX_ITERATIONS);
  int nSamples = floor(fSamples);
  float3 stepVec = farToNearVec / (nSamples-1.0);
  float3 currPos = pFar - stepVec*jitterOffset;
  
  // March back-to-front along the ray, sample and accumulate color at each step
  colour = float4(0,0,0,0);
  [unroll(MAX_ITERATIONS)]
  for (int i = 0; i < nSamples; i++) {
    float3 uvw = calcUVW(borderFront, resolution, currPos, boxMin, boxMinMaxLen);
    DoSample(volumeTex, volumeSampler, uvw, 1, opacityMultiplier, colour);
    currPos += stepVec;
    if (colour.a > 0.99) { break; }
  }
  float remainingSample = frac(fSamples);
  if (i == nSamples && remainingSample > 0) {
    float3 uvw = calcUVW(borderFront, resolution, currPos, boxMin, boxMinMaxLen);
    DoSample(volumeTex, volumeSampler, uvw, remainingSample, opacityMultiplier, colour);
  }
  colour.a = saturate(colour.a);
}



#endif // RAYMARCH_HLSL_INCLUDED