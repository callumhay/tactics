﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterCompute : MonoBehaviour {

  public float liquidDensity        = 1000.0f;   // kg/m^3
  public float atmosphericPressure  = 101325.0f; // N/m^2 (or Pascals)
  public float maxGravityVelocity   = 11.0f;     // m/s
  public float maxPressureVelocity  = 8.0f;      // m/s
  [Range(0,1)]
  public float vorticityConfinement = 0.012f;
  [Range(1,128)]
  public int numPressureIters = 10;
  
  private static readonly int NUM_THREADS_PER_BLOCK = 8;
  private int numThreadGroups;
  private ComputeShader liquidComputeShader;
  private int advectKernelId;
  private int applyExtForcesKernelId;
  private int curlKernelId;
  private int vcKernelId;


  private RenderTexture nodeDataRT;
  private RenderTexture velRT;
  private RenderTexture temp3DFloat4RT1;
  private RenderTexture temp3DFloat4RT2;
  private RenderTexture temp3DFloatRT;


  private void Start() {
    liquidComputeShader = Resources.Load<ComputeShader>("Shaders/LiquidCS");
    advectKernelId = liquidComputeShader.FindKernel("CSAdvect");
    applyExtForcesKernelId = liquidComputeShader.FindKernel("CSApplyExternalForces");
    curlKernelId = liquidComputeShader.FindKernel("CSCurl");
    vcKernelId = liquidComputeShader.FindKernel("CSVorticityConfinementKernel");
    
    liquidComputeShader.SetFloat("liquidDensity", liquidDensity);
    liquidComputeShader.SetFloat("atmoPressure", atmosphericPressure);
    liquidComputeShader.SetFloat("maxGravityVel", maxGravityVelocity);
    liquidComputeShader.SetFloat("maxPressureVel", maxPressureVelocity);
    liquidComputeShader.SetFloat("unitsPerNode", TerrainGrid.unitsPerNode());

    var volComponent = GetComponent<VolumeRaymarcher>();
    if (!volComponent) {
      Debug.LogError("Could not find VolumeRaymarcher component in GameObject.");
      return;
    }

    var borderBack = volComponent.getBorderBack();
    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();
    var internalResSize = new Vector3Int(fullResSize, fullResSize, fullResSize) - (borderBack+borderFront);

    liquidComputeShader.SetVector("borderBack", new Vector3(borderBack.x,borderBack.y,borderBack.z));
    liquidComputeShader.SetVector("borderFont", new Vector3(borderFront.x, borderFront.y, borderFront.z));
    liquidComputeShader.SetVector("internalSize", new Vector3(internalResSize.x, internalResSize.y, internalResSize.z));
    liquidComputeShader.SetInt("fullSize", fullResSize);

    numThreadGroups = fullResSize / NUM_THREADS_PER_BLOCK;

    initRenderTextures(fullResSize);
  }

  private void OnDestroy() {
    nodeDataRT?.Release();
    velRT?.Release();
    temp3DFloat4RT1?.Release();
    temp3DFloat4RT2?.Release();
    temp3DFloatRT?.Release();
  }
  
  private void initRenderTextures(int fullResSize) {
    nodeDataRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    velRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT1 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT2 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloatRT = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);
  }

  private RenderTexture init3DRenderTexture(int resSize, RenderTextureFormat format) {
    var result = new RenderTexture(resSize, resSize, 0, format);
    result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
    result.volumeDepth = resSize;
    result.enableRandomWrite = true;
    result.Create();
    return result;
  }

  private void FixedUpdate() {
    liquidComputeShader.SetFloat("dt", Time.deltaTime);
    advectVelocity();
    applyExternalForces();
    applyVorticityConfinement();
  }

  private void advectVelocity() {
    liquidComputeShader.SetTexture(advectKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(advectKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(advectKernelId, "advectVel", temp3DFloat4RT1); // Advection Output
    liquidComputeShader.Dispatch(advectKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT1); // Put the result into the velocity buffer
  }

  private void applyExternalForces() {
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "vel", velRT); // Output from advect becomes input velocity to apply ext. forces
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "velApplExtForces", temp3DFloat4RT1);

    liquidComputeShader.SetFloat("gravityMagnitude", Mathf.Abs(Physics.gravity.y));
    liquidComputeShader.Dispatch(applyExtForcesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT1); // Put the result into the velocity buffer
  }

  private void applyVorticityConfinement() {
    liquidComputeShader.SetTexture(curlKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(curlKernelId, "curl", temp3DFloat4RT1);
    liquidComputeShader.SetTexture(curlKernelId, "curlLength", temp3DFloatRT);
    liquidComputeShader.Dispatch(curlKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    // NOTE: curl == temp3DFloat4RT1, |curl| == temp3DFloatRT

    float dtVC = applyCFL(Time.deltaTime*this.vorticityConfinement);
    liquidComputeShader.SetFloat("dtVC", dtVC);
    liquidComputeShader.SetTexture(vcKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(vcKernelId, "curl", temp3DFloat4RT1);
    liquidComputeShader.SetTexture(vcKernelId, "curlLength", temp3DFloatRT);
    liquidComputeShader.SetTexture(vcKernelId, "vcVel", temp3DFloat4RT2);
    swapRTs(ref velRT, ref temp3DFloat4RT2); // Put the result (vcVel) into the velocity buffer


    /*
    let temp = this.tempBuffVec3;
    this.tempBuffVec3 = this.gpuManager.liquidCurl(this.velField);
    temp.delete();

    temp = this.tempBuffScalar;
    this.tempBuffScalar = this.gpuManager.liquidCurlLen(this.tempBuffVec3);
    temp.delete();

    const dtVC = this._applyCFL(dt*this.vorticityConfinement);
    temp = this.velField;
    this.velField = this.gpuManager.liquidApplyVC(dtVC, this.velField, this.tempBuffVec3, this.tempBuffScalar);
    temp.delete();
    */
  }

  private float applyCFL(float dt) {
    return Mathf.Min(dt, 0.3f*TerrainGrid.unitsPerNode()/(Mathf.Max(maxGravityVelocity, maxPressureVelocity)));
  }

  private void swapRTs(ref RenderTexture rt1, ref RenderTexture rt2) {
    var temp = rt1;
    rt1 = rt2;
    rt2 = temp;
  }
}
