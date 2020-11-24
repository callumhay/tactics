using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterCompute : MonoBehaviour {

  public float liquidDensity       = 1000.0f;   // kg/m^3
  public float atmosphericPressure = 101325.0f; // N/m^2 (or Pascals)
  public float maxGravityVelocity  = 11.0f;     // m/s
  public float maxPressureVelocity = 8.0f;      // m/s            

  [Range(1,128)]
  public int numPressureIters = 10;
  
  private static readonly int NUM_THREADS_PER_BLOCK = 8;
  private int numThreadGroups;
  private ComputeShader liquidComputeShader;
  private int advectKernelId;
  private int applyExtForcesKernelId;


  private RenderTexture nodeDataRT;
  private RenderTexture velRT;
  private RenderTexture temp3DFloat4RT;


  private void Start() {
    liquidComputeShader = Resources.Load<ComputeShader>("Shaders/LiquidCS");
    advectKernelId = liquidComputeShader.FindKernel("CSAdvect");
    applyExtForcesKernelId = liquidComputeShader.FindKernel("CSApplyExternalForces");
    
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
  



  private void initRenderTextures(int fullResSize) {
    nodeDataRT = init3DFloat4RenderTexture(fullResSize);
    velRT = init3DFloat4RenderTexture(fullResSize);
    temp3DFloat4RT = init3DFloat4RenderTexture(fullResSize);
  }

  private RenderTexture init3DFloat4RenderTexture(int resSize) {
    var result = new RenderTexture(resSize, resSize, 0, RenderTextureFormat.ARGBFloat);
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

  }

  private void advectVelocity() {
    liquidComputeShader.SetTexture(advectKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(advectKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(advectKernelId, "advectVel", temp3DFloat4RT); // Advection Output
    liquidComputeShader.Dispatch(advectKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT); // Put the result into the velocity buffer
  }

  private void applyExternalForces() {
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "vel", velRT); // Output from advect becomes input velocity to apply ext. forces
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(applyExtForcesKernelId, "velApplExtForces", temp3DFloat4RT);

    liquidComputeShader.SetFloat("gravityMagnitude", Mathf.Abs(Physics.gravity.y));
    liquidComputeShader.Dispatch(applyExtForcesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT); // Put the result into the velocity buffer
  }

  private void swapRTs(ref RenderTexture rt1, ref RenderTexture rt2) {
    var temp = rt1;
    rt1 = rt2;
    rt2 = temp;
  }
}
