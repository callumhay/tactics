
// Henyey-Greenstein
float hg(float a, float g) {
  float g2 = g*g;
  return (1-g2) / (4*3.1415*pow(abs(1+g2-2*g*(a)), 1.5));
}
float phase(float4 phaseParams, float a) {
  float blend = 0.5;
  float hgBlend = hg(a,phaseParams.x) * (1-blend) + hg(a,-phaseParams.y) * blend;
  return phaseParams.z + hgBlend*phaseParams.w;
}

float remap(float v, float minOld, float maxOld, float minNew, float maxNew) {
  return minNew + (v-minOld) * (maxNew - minNew) / (maxOld-minOld);
}

// Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
float3 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
  // Adapted from: http://jcgt.org/published/0007/03/04/
  float3 t0 = (boundsMin - rayOrigin) * invRaydir;
  float3 t1 = (boundsMax - rayOrigin) * invRaydir;
  float3 tmin = min(t0, t1);
  float3 tmax = max(t0, t1);
  
  float dstA = max(max(tmin.x, tmin.y), tmin.z);
  float dstB = min(tmax.x, min(tmax.y, tmax.z));

  // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
  // dstA is dst to nearest intersection, dstB dst to far intersection

  // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
  // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

  // CASE 3: ray misses box (dstA > dstB)

  float dstToBox = max(0, dstA);
  float dstInsideBox = max(0, dstB - dstToBox);
  return float3(dstToBox, dstInsideBox, dstB-dstA);
}


float sampleDensity(
  Texture3D shapeNoiseTex, Texture3D detailNoiseTex, SamplerState cloudNoiseSampler,
  float3 rayPos, float time, float scale, float3 boundsMin, float3 boundsMax, float3 shapeOffset,
  float4 shapeNoiseWeights, float densityOffset, float baseSpeed, float detailSpeed,
  float detailNoiseScale, float3 detailOffset, float3 detailNoiseWeights, float detailNoiseWeight,
  float densityMultiplier
) {

  // Constants:
  const int mipLevel = 0;
  const float baseScale = 1/1000.0;
  const float offsetSpeed = 1/100.0;

  // Calculate texture sample positions
  float3 size = boundsMax - boundsMin;
  float3 uvw = (size * 0.5 + rayPos) * baseScale * scale;
  float3 shapeSamplePos = uvw + shapeOffset * offsetSpeed + float3(time, time*0.1, time*0.2) * baseSpeed;

  // Calculate falloff at along x/z edges of the cloud container
  const float containerEdgeFadeDst = 50;
  float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - boundsMin.x, boundsMax.x - rayPos.x));
  float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - boundsMin.z, boundsMax.z - rayPos.z));
  float edgeWeight = min(dstFromEdgeZ,dstFromEdgeX) / containerEdgeFadeDst;

  float gMin = 0.2;
  float gMax = 0.7;
  float heightPercent = (rayPos.y - boundsMin.y) / size.y;
  float heightGradient = saturate(remap(heightPercent, 0.0, gMin, 0, 1)) * saturate(remap(heightPercent, 1.0, gMax, 0, 1));
  heightGradient *= edgeWeight;

  // Calculate base shape density
  float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(shapeNoiseTex, cloudNoiseSampler, shapeSamplePos,0);
  float4 normalizedShapeWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1.0);
  float shapeFBM = dot(shapeNoise, normalizedShapeWeights) * heightGradient;
  float baseShapeDensity = shapeFBM + densityOffset * 0.1;

  // Save sampling from detail tex if shape density <= 0
  float result = 0;
  if (baseShapeDensity > 0) {
    // Sample detail noise
    float3 detailSamplePos = uvw*detailNoiseScale + detailOffset * offsetSpeed + float3(time*0.4,-time,time*0.1)*detailSpeed;
    float4 detailNoise = SAMPLE_TEXTURE3D_LOD(detailNoiseTex, cloudNoiseSampler, detailSamplePos, mipLevel);
    float3 normalizedDetailWeights = detailNoiseWeights / dot(detailNoiseWeights, 1);
    float detailFBM = dot(detailNoise.rgb, normalizedDetailWeights);

    // Subtract detail noise from base shape (weighted by inverse density so that edges get eroded more than centre)
    float oneMinusShape = 1 - shapeFBM;
    float detailErodeWeight = oneMinusShape * oneMinusShape * oneMinusShape;
    float cloudDensity = baseShapeDensity - (1-detailFBM) * detailErodeWeight * detailNoiseWeight;

    result = cloudDensity * densityMultiplier * 0.1;
  }

  return result;
}


void CloudRaymarch_float(
  Texture3D shapeNoiseTex, Texture3D detailNoiseTex, SamplerState cloudNoiseSampler,
  Texture2D blueNoiseTex, SamplerState blueNoiseSampler, 
  float3 rayPos, float3 rayDir, float depth, float3 boundsMin, float3 boundsMax, float2 screenPos, float2 screenDim,
  float rayOffsetStrength, float3 sunLightDir, float3 sunLightColour, float4 phaseParams,
  float3 sceneColour, float time, float scale, float3 shapeOffset, float4 shapeNoiseWeights,
  float densityOffset, float baseSpeed, float detailSpeed, float detailNoiseScale, float3 detailOffset,
  float3 detailNoiseWeights, float detailNoiseWeight, float densityMultiplier, int numStepsLight,
  float darknessThreshold, float lightAbsorptionTowardSun, float lightAbsorptionThroughCloud,
  out float4 colour
) {

  // Cloud container intersection info:
  float3 rayToContainerInfo = rayBoxDst(boundsMin, boundsMax, rayPos, 1.0/rayDir);
  clip(rayToContainerInfo.z);
  float dstToBox = rayToContainerInfo.x;
  float dstInsideBox = rayToContainerInfo.y;

  // Point of intersection with the cloud container
  float3 entryPoint = rayPos + rayDir * dstToBox;

  // Random starting offset (makes low-res results noisy rather than jagged/glitchy, which is nicer)
  float randomOffset = SAMPLE_TEXTURE2D_LOD(blueNoiseTex, blueNoiseSampler, screenPos*screenDim*3.0/1000.0, 0).r;
  randomOffset *= rayOffsetStrength;

  // Phase function makes clouds brighter around sun
  float cosAngle = dot(rayDir, sunLightDir);
  float phaseVal = phase(phaseParams, cosAngle);

  float dstTravelled = randomOffset;
  float dstLimit = min(depth-dstToBox, dstInsideBox);

  // March through volume:
  const float stepSize = 11;
  float transmittance = 1;
  float3 lightEnergy = 0;
  float3 currRayPos = float3(0,0,0);

  while (dstTravelled < dstLimit) {
    currRayPos = entryPoint + rayDir * dstTravelled;
    float density = sampleDensity(shapeNoiseTex, detailNoiseTex, cloudNoiseSampler, 
      currRayPos, time, scale, boundsMin, boundsMax, shapeOffset, shapeNoiseWeights,
      densityOffset, baseSpeed, detailSpeed, detailNoiseScale, detailOffset, 
      detailNoiseWeights, detailNoiseWeight, densityMultiplier);

    if (density > 0) {
      // Calculate proportion of light that reaches the given point from the lightsource
      float3 position = currRayPos;
      float lightDstInsideBox = rayBoxDst(boundsMin, boundsMax, position, 1.0/sunLightDir).y;
      float lightStepSize = lightDstInsideBox/numStepsLight;
      float totalDensity = 0;
      
      for (int step = 0; step < numStepsLight; step++) {
        position += sunLightDir * lightStepSize;
        totalDensity += max(0, sampleDensity(shapeNoiseTex, detailNoiseTex, cloudNoiseSampler, 
          position, time, scale, boundsMin, boundsMax, shapeOffset, shapeNoiseWeights,
          densityOffset, baseSpeed, detailSpeed, detailNoiseScale, detailOffset, 
          detailNoiseWeights, detailNoiseWeight, densityMultiplier) * lightStepSize);
      }

      float lightTransmittance = darknessThreshold + exp(-totalDensity * lightAbsorptionTowardSun) * (1.0-darknessThreshold);
      lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
      transmittance *= exp(-density * stepSize * lightAbsorptionThroughCloud);

      // Exit early if the transmittance is close to zero as further samples won't affect the result much
      if (transmittance < 0.01) { break; }
    }
    dstTravelled += stepSize;
  }

  // Add clouds to scene
  float3 cloudCol = lightEnergy * sunLightColour;
  colour = float4(sceneColour * transmittance + cloudCol, saturate(dot(lightEnergy,1)+(1-transmittance)));
}