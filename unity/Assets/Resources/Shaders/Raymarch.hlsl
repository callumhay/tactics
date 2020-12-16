#ifndef RAYMARCH_HLSL_INCLUDED
#define RAYMARCH_HLSL_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "IntersectBox.hlsl"
#include "NodeDefs.hlsl"
#include "SimplexNoise.hlsl"

#define MIN_ITERATIONS 3
#define MAX_ITERATIONS 32

void DoSample(Texture3D volumeTex, SamplerState volumeSampler, float3 uvw, float weight,
  float nodeVolume, float transmittance, inout float4 colour) {
  float4 node = SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvw);

  float nodeVolPercentage = transmittance*smoothstep(0, nodeVolume, nodeLiquidVolume(node));
  //float nodeVolPercentage = saturate(smoothstep(0, nodeVolume, 10*nodeLiquidVolume(node)));
  //float4 sampleColour = nodeLiquidVolume(node) > nodeVolume ? float4(1,0,0,nodeVolPercentage) : (nodeSettled(node) ==  SETTLED_NODE) ? float4(0,1,0,nodeVolPercentage) : float4(1, 1, 1, nodeVolPercentage);//((nodeType(node) == SOLID_NODE_TYPE) ? float4(1,0,0,1) : float4(1, 1, 1, nodeVolPercentage));
  float4 sampleColour = weight * float4(1,1,1,nodeVolPercentage);
  colour.rgb = (1-sampleColour.a)*colour.rgb + sampleColour.a * sampleColour.rgb;
  colour.a = (1-sampleColour.a)*colour.a + sampleColour.a;
  
}

float3 calcUVW(float3 borderFront, float resNoBorderLen, float3 currPos, float3 boxMin, float boxMinMaxLen) {
  return (borderFront + ((currPos-boxMin)/boxMinMaxLen)*resNoBorderLen) / (float)resolution;
}

float3 calcIsoNormal(Texture3D volumeTex, SamplerState volumeSampler, float3 uvwCenter, float resolution) {
  float cellSize = 1.0 / resolution;

  float volL = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter - float3(cellSize, 0.0, 0.0)));
  float volR = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter + float3(cellSize, 0.0, 0.0)));
  float volB = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter - float3(0.0, cellSize, 0.0)));
  float volT = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter + float3(0.0, cellSize, 0.0)));
  float volD = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter - float3(0.0, 0.0, cellSize)));
  float volU = nodeLiquidVolume(SAMPLE_TEXTURE3D(volumeTex, volumeSampler, uvwCenter + float3(0.0, 0.0, cellSize)));

  return float3(volL-volR, volB-volT, volD-volU);
}

void Raymarch_float(
  Texture3D volumeTex, SamplerState volumeSampler, 
  Texture2D jitterTex, SamplerState jitterSampler,
  float eyeDepthAlongRay, float cameraFarPlane, float2 pos, float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, 
  int3 borderFront, int3 borderBack, float resolution, float transmittance, float nodeVolume,
  float time, out float4 colour, out float depthOffset, out float3 nNormal) {
  
  depthOffset = 0;
  float tNear = 0.0; float tFar = 0.0;
  IntersectBox_float(rayOrigin, rayDir, boxMin, boxMax, tNear, tFar);

  tNear = max(0, tNear); // If the camera is inside the volume then just start/end at the camera
  tFar = min(tFar, eyeDepthAlongRay); // We only march as far as the end of the volume or the front of the closest object in the depth buffer
  clip(tFar - tNear); // Check for an intersection hit (negative values mean there was no hit so we clip them)

  // Calculate box min to max length and unit vector
  float3 minToMaxVec = boxMax-boxMin;
  float boxMinMaxLen = length(minToMaxVec);

  // Figure out the number of voxels along the ray within the box, this will be the basis
  // for the number of steps we march along the ray
  float3 borderVec = borderFront + borderBack;
  float3 resVec = float3(resolution, resolution, resolution);
  float3 resNoBorderVec = resVec-borderVec;
  float resNoBorderLen = length(resNoBorderVec);
  float3 resNoBorderVecDivRes = resNoBorderVec / (float)resolution;

  // Perform a jitter along the ray, the jitter must not exceed half the voxel unit distance along the ray
  //float jitterOffset = SAMPLE_TEXTURE2D(jitterTex, jitterSampler, pos.xy).r; // Random values in [0,1)

  // Calculate the intersection points of the eye ray to the box
  float3 pNear = rayOrigin + rayDir*tNear;
  float3 pFar  = rayOrigin + rayDir*tFar;
  float3 nearToFarVec = pFar-pNear;
  float nearFarLen = length(nearToFarVec);

  float bestSamples = 2.0 * (nearFarLen / boxMinMaxLen) * resNoBorderLen;
  float fSamples = clamp(bestSamples, MIN_ITERATIONS, MAX_ITERATIONS);
  int nSamples = floor(fSamples);
  float3 stepVec = nearToFarVec / (nSamples-1.0);
  float3 currPos = pNear;// - stepVec*jitterOffset;

  colour = float4(0,0,0,0);
  float3 uvw = float3(0,0,0);
  int i = 0;
  [unroll(MAX_ITERATIONS)]
  for (; i < nSamples; i++) {
    uvw = calcUVW(borderFront, resNoBorderLen, currPos, boxMin, boxMinMaxLen);
    DoSample(volumeTex, volumeSampler, uvw, 1, nodeVolume, transmittance, colour);
    currPos += stepVec;
    if (colour.a > 0.99) { break; }
  }
  
  clip(colour.a - 1e-4); // Discard the fragment if there's no alpha

  float noiseVal = snoise(float4(currPos-stepVec, 0.2*time));
  nNormal = calcIsoNormal(volumeTex, volumeSampler, uvw, resolution) + 0.001*float3(noiseVal,noiseVal,noiseVal);
  
  depthOffset = length(currPos-pNear)-tFar;

/*
  float remainingSample = frac(fSamples); 
  if (i == nSamples && remainingSample > 0) {
    //uvw = calcUVW(borderFront, resNoBorderLen, currPos, boxMin, boxMinMaxLen);
    //DoSample(volumeTex, volumeSampler, uvw, remainingSample, nodeVolume, transmittance, colour);
    //float3 partNormal = calcIsoNormal(volumeTex, volumeSampler, uvw, resolution);
    //nNormal += remainingSample*partNormal;
    depthOffset = tNear-tFar;
  }
  else {
    depthOffset = length(currPos-pNear)-tFar;
  }
  */

  float normalLen = length(nNormal);
  nNormal = normalLen < 1e-12 ? normalize(-stepVec) : nNormal/normalLen;

  colour = saturate(colour);
}

#endif // RAYMARCH_HLSL_INCLUDED