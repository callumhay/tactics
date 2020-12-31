
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

float2 ray2SphereIntersect(float3 center, float radiusInner, float radiusOuter, float3 rayOrigin, float3 rayDir) {
  float3 m = rayOrigin-center;
  float mm = dot(m,m);
  float b = dot(m, rayDir);
  float cInner = mm - radiusInner*radiusInner;

  clip(-cInner);
  float discr = b*b - cInner;
  clip(discr);
  float tInner = -b + sqrt(discr); // where (rayOrigin + tInner*rayDir) is the intersection point with the inner sphere

  // If we're here then the ray intersects with the inner sphere and must therefore intersect with the outer sphere as well
  float cOuter = mm - radiusOuter*radiusOuter;
  discr = b*b - cOuter;
  float tOuter = -b + sqrt(discr);

  float dstToInnerSphere = max(0, tInner);
  float dstBetweenSpheres = max(0, tOuter-dstToInnerSphere);

  return float2(dstToInnerSphere, dstBetweenSpheres);
}

float lightRay2SphereIntersect(float3 center, float radiusInner, float radiusOuter, float3 rayOrigin, float3 rayDir) {

  // Pretend like the clouds are a box with a thickness of radiusOuter-radiusInner
  //float3 boundsMin = center + float3(-5000,radiusInner,-5000);
  //float3 boundsMax = center + float3(5000,radiusOuter,5000);
  //return rayBoxDst(boundsMin, boundsMax, rayOrigin, 1.0/rayDir).y;
 
  // We assume the ray origin is always between the two radii, we need to figure out which of the two shells the ray intersects...
  float3 m = rayOrigin-center;
  float mm = dot(m,m);
  float b = dot(m, rayDir);
  float c = min(mm - radiusOuter*radiusOuter, mm - radiusInner*radiusInner);
  float discr = b*b - c;
  return (c > 0 || discr < 0) ? 0 : min(radiusOuter-radiusInner, max(0, -b + sqrt(discr)));
 
}


float sampleDensity(
  Texture3D shapeNoiseTex, Texture3D detailNoiseTex, SamplerState cloudNoiseSampler,
  float3 rayPos, float time, float scale, float3 boundsMin, float3 boundsMax, float3 shapeOffset,
  float4 shapeNoiseWeights, float densityOffset, float2 windDir, float baseSpeed, float detailSpeed,
  float detailNoiseScale, float3 detailOffset, float3 detailNoiseWeights, float detailNoiseWeight,
  float densityMultiplier
) {

  // Constants:
  const int mipLevel = 0;
  const float baseScale = 1/1000.0;
  const float offsetSpeed = 1/100.0;

  float3 windDir3 = -float3(windDir.x, 1, windDir.y);

  // Calculate texture sample positions
  float3 size = boundsMax - boundsMin;
  float3 uvw = (size * 0.5 + rayPos) * baseScale * scale;
  float3 shapeSamplePos = uvw + shapeOffset * offsetSpeed + float3(time, time*0.1, time*0.2) * windDir3 * baseSpeed;

/*
  // Calculate falloff at along x/z edges of the cloud container
  const float containerEdgeFadeDst = 100;
  float dstFromEdgeX = min(containerEdgeFadeDst, min(rayPos.x - boundsMin.x, boundsMax.x - rayPos.x));
  float dstFromEdgeZ = min(containerEdgeFadeDst, min(rayPos.z - boundsMin.z, boundsMax.z - rayPos.z));
  float edgeWeight = smoothstep(0, containerEdgeFadeDst, min(dstFromEdgeZ,dstFromEdgeX));

  float gMin = 0.25;
  float gMax = 0.75;
  float heightPercent = (rayPos.y - boundsMin.y) / size.y;
  float containerGradient = smoothstep(0,gMin,heightPercent) * smoothstep(1,gMax,heightPercent);
  containerGradient *= edgeWeight;
  */
  float containerGradient = 1;

  // Calculate base shape density
  float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(shapeNoiseTex, cloudNoiseSampler, shapeSamplePos,0);
  float4 normalizedShapeWeights = shapeNoiseWeights / dot(shapeNoiseWeights, 1.0);
  float shapeFBM = dot(shapeNoise, normalizedShapeWeights) * containerGradient;
  float baseShapeDensity = shapeFBM + densityOffset * 0.1;

  // Save sampling from detail tex if shape density <= 0
  float result = 0;
  if (baseShapeDensity > 0) {
    // Sample detail noise
    float3 detailSamplePos = uvw*detailNoiseScale + detailOffset * offsetSpeed + float3(time*0.4,-time,time*0.1) * windDir3 * detailSpeed;
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
  float densityOffset, float2 windDir, float baseSpeed, float detailSpeed, float detailNoiseScale, float3 detailOffset,
  float3 detailNoiseWeights, float detailNoiseWeight, float densityMultiplier, int numStepsLight,
  float darknessThreshold, float lightAbsorptionTowardSun, float lightAbsorptionThroughCloud,
  float innerRadius, float outerRadius, float3 sphereCenter,
  out float4 colour
) {


  // Cloud container intersection info:
  float2 rayToDomeInfo = ray2SphereIntersect(sphereCenter, innerRadius, outerRadius, rayPos, rayDir);
  float dstToDome = rayToDomeInfo.x;
  float dstInsideDome = rayToDomeInfo.y;

  //colour = float4(depth/500,0,0,1);
  //return;

  // Point of intersection with the cloud container
  float3 entryPoint = rayPos + rayDir * dstToDome;

  // Random starting offset (makes low-res results noisy rather than jagged/glitchy, which is nicer)
  float randomOffset = SAMPLE_TEXTURE2D_LOD(blueNoiseTex, blueNoiseSampler, screenPos*screenDim*3.0/1000.0, 0).r;
  randomOffset *= rayOffsetStrength;

  // Phase function makes clouds brighter around sun
  float cosAngle = dot(rayDir, sunLightDir);
  float phaseVal = phase(phaseParams, cosAngle);

  float dstTravelled = randomOffset;
  float dstLimit = min(depth-dstToDome, dstInsideDome);

  // March through volume:
  const float stepSize = 11;
  float transmittance = 1;
  float3 lightEnergy = 0;
  float3 currRayPos = float3(0,0,0);

  while (dstTravelled < dstLimit) {
    currRayPos = entryPoint + rayDir * dstTravelled;
    float density = sampleDensity(shapeNoiseTex, detailNoiseTex, cloudNoiseSampler, 
      currRayPos, time, scale, boundsMin, boundsMax, shapeOffset, shapeNoiseWeights,
      densityOffset, windDir, baseSpeed, detailSpeed, detailNoiseScale, detailOffset, 
      detailNoiseWeights, detailNoiseWeight, densityMultiplier);

    if (density > 0) {
      // Calculate proportion of light that reaches the given point from the lightsource
      float3 position = currRayPos;
      float lightDstInsideDome = lightRay2SphereIntersect(sphereCenter, innerRadius, outerRadius, position, sunLightDir); //rayBoxDst(boundsMin, boundsMax, position, 1.0/sunLightDir).y;
      float lightStepSize = lightDstInsideDome/numStepsLight;
      float totalDensity = 0;
      
      for (int step = 0; step < numStepsLight; step++) {
        position += sunLightDir * lightStepSize;
        totalDensity += max(0, sampleDensity(shapeNoiseTex, detailNoiseTex, cloudNoiseSampler, 
          position, time, scale, boundsMin, boundsMax, shapeOffset, shapeNoiseWeights,
          densityOffset, windDir, baseSpeed, detailSpeed, detailNoiseScale, detailOffset, 
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