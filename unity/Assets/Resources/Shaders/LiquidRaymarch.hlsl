#ifndef RAYMARCH_HLSL_INCLUDED
#define RAYMARCH_HLSL_INCLUDED

#include "IntersectBox.hlsl"
#include "NodeDefs.hlsl"
#include "SimplexNoise.hlsl"

void DoSample(Texture3D volumeTex, SamplerState volumeSampler, float3 uvw, float weight,
  float nodeVolume, float thicknessMultiplier, inout float4 colour) {
  float4 node = SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvw, 0);

  float nodeVolPercentage = clamp(thicknessMultiplier*nodeLiquidVolume(node) / nodeVolume, 0, 1);
  nodeVolPercentage = nodeVolPercentage <= nodeVolume*0.05 ? 0 : nodeVolPercentage;
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

  float volL = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter - float3(cellSize, 0.0, 0.0),0));
  float volR = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter + float3(cellSize, 0.0, 0.0),0));
  float volB = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter - float3(0.0, cellSize, 0.0),0));
  float volT = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter + float3(0.0, cellSize, 0.0),0));
  float volD = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter - float3(0.0, 0.0, cellSize),0));
  float volU = nodeLiquidVolume(SAMPLE_TEXTURE3D_LOD(volumeTex, volumeSampler, uvwCenter + float3(0.0, 0.0, cellSize),0));

  return float3(volL-volR, volB-volT, volD-volU);
}

void LiquidRaymarch_float(
  Texture3D volumeTex, SamplerState volumeSampler, 
  Texture2D jitterTex, SamplerState jitterSampler,
  float eyeDepthAlongRay, float cameraFarPlane, float2 screenPos, float2 screenDim, 
  float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, 
  int3 borderFront, int3 borderBack, float resolution, float thicknessMultiplier, float nodeVolume,
  float time, out float4 colour, out float depthOffset, out float3 nNormal) {
  
  depthOffset = 0;
  float tNear = 0.0; float tFar = 0.0;
  IntersectBox_float(rayOrigin, rayDir, boxMin, boxMax, tNear, tFar);

  tNear = max(0, tNear);  // If the camera is inside the volume then just start/end at the camera
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

  // Calculate the intersection points of the eye ray to the box
  float3 pNear = rayOrigin + rayDir*tNear;
  float3 pFar  = rayOrigin + rayDir*tFar;
  float3 nearToFarVec = pFar-pNear;
  float nearFarLen = length(nearToFarVec);

  int nSamples = min(256, max(2, 2*ceil((nearFarLen / boxMinMaxLen) * resNoBorderLen)));
  float3 stepVec = nearToFarVec / (float)(nSamples);
  float stepLen = nearFarLen / (float)(nSamples);

  // Perform a jitter along the ray
  float jitterVal = SAMPLE_TEXTURE2D_LOD(jitterTex, jitterSampler, screenPos*screenDim*3.0/1000.0, 0).r;
  float distTravelled = stepLen*jitterVal;
  float depthTravelled = tNear;

  // Raymarch to find the accumulation of liquid (up to saturation or until we hit the end of the ray)
  colour = float4(0,0,0,0);
  float3 uvw = float3(0,0,0);
  float3 currPos = float3(0,0,0);
  while (distTravelled < nearFarLen) {
    currPos = pNear + distTravelled * rayDir;
    uvw = calcUVW(borderFront, resNoBorderLen, currPos, boxMin, boxMinMaxLen);
    DoSample(volumeTex, volumeSampler, uvw, 1, nodeVolume, thicknessMultiplier, colour);
    if (colour.a > 0.99) { break; }
    distTravelled += stepLen;
    depthTravelled += stepLen;
  }
  clip(colour.a - 1e-4); // Discard the fragment if there's no alpha

  depthOffset = depthTravelled-tFar;
  
  float noiseVal = sumSNoise(float4(currPos, 0.1*time), 2, 0.5, 1.5, 0.75);
  nNormal = normalize(calcIsoNormal(volumeTex, volumeSampler, uvw, resolution) + 0.001*float3(noiseVal,noiseVal,noiseVal));
  colour = saturate(colour);
}

#endif // RAYMARCH_HLSL_INCLUDED