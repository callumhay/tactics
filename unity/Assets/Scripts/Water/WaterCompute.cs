using UnityEngine;

public class WaterCompute : MonoBehaviour {

  public static readonly int NUM_THREADS_PER_BLOCK = 8;

  public float liquidDensity        = 1000.0f;   // kg/m^3
  public float atmosphericPressure  = 101325.0f; // N/m^2 (or Pascals)
  public float maxGravityVelocity   = 20.0f;     // m/s
  public float maxPressureVelocity  = 18.0f;      // m/s
  [Range(0,1)]
  public float vorticityConfinement = 0.012f;
  [Range(1,128)]
  public int numPressureIters = 10;
  
  private int numThreadGroups;
  private ComputeShader liquidComputeShader;
  private int advectKernelId;
  private int applyExtForcesKernelId;
  private int curlKernelId;
  private int vcKernelId;
  private int divergenceKernelId;
  private int pressureKernelId;
  private int projectKernelId;
  private int flowsKernelId;
  private int sumFlowsKernelId;
  private int adjustVolFromFlowsKernelId;

  public RenderTexture nodeDataRT;
  private RenderTexture velRT;
  private RenderTexture temp3DFloat4RT1;
  private RenderTexture temp3DFloat4RT2;
  private RenderTexture temp3DFloatRT;
  private RenderTexture tempPressurePing;
  private RenderTexture tempPressurePong;
  private ComputeBuffer flowsBuffer;

  private VolumeRaymarcher volComponent;

  private void Start() {
    liquidComputeShader = Resources.Load<ComputeShader>("Shaders/LiquidCS");
    advectKernelId = liquidComputeShader.FindKernel("CSAdvect");
    applyExtForcesKernelId = liquidComputeShader.FindKernel("CSApplyExternalForces");
    curlKernelId = liquidComputeShader.FindKernel("CSCurl");
    vcKernelId = liquidComputeShader.FindKernel("CSVorticityConfinementKernel");
    divergenceKernelId = liquidComputeShader.FindKernel("CSDivergenceKernel");
    pressureKernelId = liquidComputeShader.FindKernel("CSPressureKernel");
    projectKernelId = liquidComputeShader.FindKernel("CSProjectKernel");
    flowsKernelId = liquidComputeShader.FindKernel("CSCalculateFlowsKernel");
    sumFlowsKernelId = liquidComputeShader.FindKernel("CSSumFlowsKernel");
    adjustVolFromFlowsKernelId = liquidComputeShader.FindKernel("CSAdjustNodeFlowsKernel");

    liquidComputeShader.SetFloat("liquidDensity", liquidDensity);
    liquidComputeShader.SetFloat("atmoPressure", atmosphericPressure);
    liquidComputeShader.SetFloat("maxGravityVel", maxGravityVelocity);
    liquidComputeShader.SetFloat("maxPressureVel", maxPressureVelocity);
    liquidComputeShader.SetFloat("unitsPerNode", TerrainGrid.unitsPerNode());

    volComponent = GetComponent<VolumeRaymarcher>();
    if (!volComponent) {
      Debug.LogError("Could not find VolumeRaymarcher component in GameObject.");
      return;
    }

    var borderBack = volComponent.getBorderBack();
    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();
    var internalResSize = new Vector3Int(fullResSize, fullResSize, fullResSize) - (borderBack+borderFront);
    Debug.Log("Internal resolution size: " + internalResSize);
    Debug.Log("Border Front: " +  new Vector3(borderFront.x, borderFront.y, borderFront.z));
    Debug.Log("Border Back: " + new Vector3(borderBack.x, borderBack.y,borderBack.z));

    liquidComputeShader.SetInts("borderBack", new int[]{borderBack.x, borderBack.y,borderBack.z});
    liquidComputeShader.SetInts("borderFront", new int[]{borderFront.x, borderFront.y, borderFront.z});
    liquidComputeShader.SetInts("internalSize", new int[]{internalResSize.x, internalResSize.y, internalResSize.z});
    liquidComputeShader.SetInt("fullSize", fullResSize);

    numThreadGroups = fullResSize / NUM_THREADS_PER_BLOCK;
    Debug.Log("Number of thread groups: " + numThreadGroups); 

    initBuffersAndRTs(fullResSize);
    initDebugNodes();
  }

  private void OnDestroy() {
    nodeDataRT?.Release();
    velRT?.Release();
    temp3DFloat4RT1?.Release();
    temp3DFloat4RT2?.Release();
    temp3DFloatRT?.Release();
    tempPressurePing?.Release();
    tempPressurePong?.Release();
    flowsBuffer?.Release();
  }
  
  private void initBuffersAndRTs(int fullResSize) {
    nodeDataRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    velRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT1 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT2 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloatRT = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);
    tempPressurePing = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);
    tempPressurePong = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);

    int flattenedBufSize = fullResSize*fullResSize*fullResSize;
    flowsBuffer = new ComputeBuffer(flattenedBufSize, 6*sizeof(float));
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
    /*
    liquidComputeShader.SetFloat("dt", 10.0f);//Time.fixedDeltaTime);
    advectVelocity();
    applyExternalForces();
    applyVorticityConfinement();
    computeDivergence();
    computePressure();
    projectVelocity();
    calculateAndSumFlows();
    adjustNodesFromFlows();
    */

    volComponent.updateVolumeData(nodeDataRT);
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

    liquidComputeShader.SetFloat("gravityMagnitude", 9.81f);//Mathf.Abs(Physics.gravity.y));
    liquidComputeShader.Dispatch(applyExtForcesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT1); // Put the result into the velocity buffer
  }

  private void applyVorticityConfinement() {
    liquidComputeShader.SetTexture(curlKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(curlKernelId, "curl", temp3DFloat4RT1);
    liquidComputeShader.SetTexture(curlKernelId, "curlLength", temp3DFloatRT);
    liquidComputeShader.Dispatch(curlKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    // NOTE: curl == temp3DFloat4RT1, |curl| == temp3DFloatRT

    float dtVC = applyCFL(Time.fixedDeltaTime*this.vorticityConfinement);
    liquidComputeShader.SetFloat("dtVC", dtVC);
    liquidComputeShader.SetTexture(vcKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(vcKernelId, "curl", temp3DFloat4RT1);
    liquidComputeShader.SetTexture(vcKernelId, "curlLength", temp3DFloatRT);
    liquidComputeShader.SetTexture(vcKernelId, "vcVel", temp3DFloat4RT2);
    swapRTs(ref velRT, ref temp3DFloat4RT2); // Put the result (vcVel) into the velocity buffer
  }

  private void computeDivergence() {
    liquidComputeShader.SetTexture(divergenceKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(divergenceKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(divergenceKernelId, "divergence", temp3DFloatRT);
    liquidComputeShader.SetTexture(divergenceKernelId, "pressure", tempPressurePing);
    liquidComputeShader.Dispatch(divergenceKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    // NOTE: divergence == temp3DFloatRT
  }

  private void computePressure() {
    // NOTE: divergence == temp3DFloatRT    
    // NOTE: We cleared tempPressurePing (i.e., the input pressure) to all zeros in the divergence pass∏
    for (int i = 0; i < numPressureIters; i++) {
      liquidComputeShader.SetTexture(pressureKernelId, "nodeData", nodeDataRT);
      liquidComputeShader.SetTexture(pressureKernelId, "divergence", temp3DFloatRT);
      liquidComputeShader.SetTexture(pressureKernelId, "inputPressure", tempPressurePing);
      liquidComputeShader.SetTexture(pressureKernelId, "outputPressure", tempPressurePong);
      liquidComputeShader.Dispatch(pressureKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
      swapRTs(ref tempPressurePing, ref tempPressurePong);
    }

    // NOTE: final pressure values == tempPressurePing
  }

  private void projectVelocity() {
    liquidComputeShader.SetTexture(projectKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(projectKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(projectKernelId, "pressure", tempPressurePing);
    liquidComputeShader.SetTexture(projectKernelId, "projectedVel", temp3DFloat4RT1);
    liquidComputeShader.Dispatch(projectKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT1); // Put the result into the velocity buffer
  }

  struct NodeFlowInfo {
    float flowL; float flowR;
    float flowB; float flowT;
    float flowD; float flowU;
  };
  private void calculateAndSumFlows() {
    // Calculate all the flows going in/out of each cell to neighbouring cells along each axis
    liquidComputeShader.SetTexture(flowsKernelId, "vel", velRT);
    liquidComputeShader.SetTexture(flowsKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetBuffer(flowsKernelId, "flows", flowsBuffer);
    liquidComputeShader.Dispatch(flowsKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    // Sum together all the new liquid volumes based on the flows
    liquidComputeShader.SetBuffer(sumFlowsKernelId, "flows", flowsBuffer);
    liquidComputeShader.SetTexture(sumFlowsKernelId, "summedFlows", temp3DFloatRT);
    liquidComputeShader.Dispatch(sumFlowsKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    // NOTE: summed flows == temp3DFloatRT
  }

  private void adjustNodesFromFlows() {
    // NOTE: summed flows == temp3DFloatRT
    liquidComputeShader.SetTexture(adjustVolFromFlowsKernelId, "summedFlows", temp3DFloatRT);
    liquidComputeShader.SetTexture(adjustVolFromFlowsKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.Dispatch(adjustVolFromFlowsKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
  }

  private float applyCFL(float dt) {
    return dt;// Mathf.Min(dt, 0.3f*TerrainGrid.unitsPerNode()/(Mathf.Max(maxGravityVelocity, maxPressureVelocity)));
  }

  private void swapRTs(ref RenderTexture rt1, ref RenderTexture rt2) {
    var temp = rt1;
    rt1 = rt2;
    rt2 = temp;
  }
  
  private void clearRT(RenderTexture rt, Color c) {
    var tempRT = RenderTexture.active;
    RenderTexture.active = rt;
    GL.Clear(true, true, c);
    RenderTexture.active = tempRT;
  }

  private void initDebugNodes() {
    int debugKernelId = liquidComputeShader.FindKernel("CSFillDebugNodeData");
    liquidComputeShader.SetTexture(debugKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.Dispatch(debugKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
  }

}
