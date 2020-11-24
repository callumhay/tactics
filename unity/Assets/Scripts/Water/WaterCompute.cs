using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterCompute : MonoBehaviour {

  public float liquidDensity       = 1000.0f;   // kg/m^3
  public float atmosphericPressure = 101325.0f; // N/m^2 (or Pascals)
  public float maxGravityVelocity  = 11.0f;     // m/s
  public float maxPressureVelocity = 8.0f;      // m/s
  public float maxPressureHeight   = 5;         // m

  [Range(1,128)]
  public int numPressureIters = 10;
  
  private ComputeShader liquidComputeShader;
  private int advectKernelId;

  private void Start() {
    liquidComputeShader = Resources.Load<ComputeShader>("Shaders/LiquidCS");
    advectKernelId = liquidComputeShader.FindKernel("CSAdvect");
  }
  


}
