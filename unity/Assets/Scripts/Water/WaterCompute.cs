using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649
/// <summary>
/// Structure used to read/write update the liquid compute shaders and convey information
/// back and forth between the CPU and GPU.
/// </summary>
struct LiquidNodeUpdate {
  public float terrainIsoVal;
  public float liquidVolume;
  public Vector3 velocity;
  public float isDiff; // Used to determine whether a value was changed or not on the GPU (>0 means it changed)

  public static int flattenedNodeIdx(in Vector3Int nodeIdx, in Vector3Int borderFront, int size) {
    return LevelData.node3DIndexToFlatIndex(borderFront.x+nodeIdx.x, borderFront.y+nodeIdx.y, borderFront.z+nodeIdx.z, size, size);
  }
  public static Vector3Int unflattenedNodeIdx(int flatIdx, in Vector3Int borderFront, int size) {
    var result = LevelData.flatIndexToNode3DIndex(flatIdx, size, size);
    result.x -= borderFront.x; result.y -= borderFront.y; result.z -= borderFront.z;
    return result;
  }
};

/// <summary>
/// Structure used to track flows coming out of each node in the liquid simulation's flow compute shaders.
/// </summary>
struct NodeFlowInfo {
  public float flowL; public float flowR;
  public float flowB; public float flowT;
  public float flowD; public float flowU;
};

[ExecuteAlways]
public class WaterCompute : MonoBehaviour {

  public static readonly int NUM_THREADS_PER_BLOCK = 8;

  public bool enableSimulation = false;
  [Range(1,10000)]
  public float liquidDensity = 1000.0f;   // kg/m^3
  [Range(0,101325)]
  public float atmosphericPressure = 100.0f;
  [Range(0,1000)]
  public float maxGravityVelocity = 20.0f;    // m/s
  [Range(0,1000)]
  public float maxPressureVelocity = 11.0f;     // m/s
  [Range(0,100)]
  public float friction = 6.0f;
  [Range(0,10000)]
  public float gravityMagnitude = 9.81f;
  [Range(0,1)]
  public float vorticityConfinement = 0.012f;
  [Range(1,100)]
  public float flowMultiplier = 1.0f;
  [Range(1,128)]
  public int numPressureIters = 40;
  
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
  private int updateNodesKernelId;
  private int readNodesKernelId;

  private RenderTexture nodeDataRT;
  private RenderTexture velRT;
  private RenderTexture obsticleVelRT;
  private RenderTexture temp3DFloat4RT1;
  private RenderTexture temp3DFloat4RT2;
  private RenderTexture temp3DFloatRT;
  private RenderTexture tempPressurePing;
  private RenderTexture tempPressurePong;
  private ComputeBuffer flowsBuffer;

  ComputeBuffer updateNodeComputeBuf; // CPU -> GPU buffer - allows us to tell the water simulation about the terrain
  ComputeBuffer readNodeComputeBuf;   // GPU -> CPU buffer - allows us to tell the terrain about the water simulation
  float[] readNodeCPUArr; // Temp buffer for reading nodes from GPU to CPU

  private VolumeRaymarcher volComponent;

  private Vector3Int currBorderBack;
  private Vector3Int currBorderFront;
  private int currFullResSize;


  public void initAll(VolumeRaymarcher _volComponent) {
    volComponent = _volComponent;
    if (liquidComputeShader == null) {
      liquidComputeShader = Resources.Load<ComputeShader>("Shaders/LiquidCS");
    }

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
    updateNodesKernelId = liquidComputeShader.FindKernel("CSUpdateNodeData");
    readNodesKernelId = liquidComputeShader.FindKernel("CSReadNodeData");

    initUniforms();

    var borderBack = volComponent.getBorderBack();
    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();

    // Don't reinitialize all the buffers unless we changed the dimensions
    if (borderBack != currBorderBack || borderFront != currBorderFront || currFullResSize != fullResSize) {
      currBorderBack = borderBack;
      currBorderFront = borderFront;
      currFullResSize = fullResSize;

      var internalResSize = new Vector3Int(fullResSize, fullResSize, fullResSize) - (borderBack+borderFront);
      //Debug.Log("Internal resolution size: " + internalResSize);
      //Debug.Log("Border Front: " +  new Vector3(borderFront.x, borderFront.y, borderFront.z));
      //Debug.Log("Border Back: " + new Vector3(borderBack.x, borderBack.y,borderBack.z));

      liquidComputeShader.SetInts("borderBack", new int[]{borderBack.x, borderBack.y,borderBack.z});
      liquidComputeShader.SetInts("borderFront", new int[]{borderFront.x, borderFront.y, borderFront.z});
      liquidComputeShader.SetInts("internalSize", new int[]{internalResSize.x, internalResSize.y, internalResSize.z});
      liquidComputeShader.SetInt("fullSize", fullResSize);

      numThreadGroups = fullResSize / NUM_THREADS_PER_BLOCK;
      //Debug.Log("Number of thread groups: " + numThreadGroups); 

      initBuffersAndRTs(fullResSize);
      //initDebugNodes();
      clearNodes();
    }
  }

  private void initBuffersAndRTs(int fullResSize) {
    clearBuffersAndRTs();

    nodeDataRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    velRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    obsticleVelRT = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT1 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloat4RT2 = init3DRenderTexture(fullResSize, RenderTextureFormat.ARGBFloat);
    temp3DFloatRT = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);
    tempPressurePing = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);
    tempPressurePong = init3DRenderTexture(fullResSize, RenderTextureFormat.RFloat);

    int flattenedBufSize = fullResSize*fullResSize*fullResSize;

    flowsBuffer = new ComputeBuffer(flattenedBufSize, 6*sizeof(float)); // Uses the NodeFlowInfo struct
    GC.SuppressFinalize(flowsBuffer);
    updateNodeComputeBuf = new ComputeBuffer(flattenedBufSize, 6*sizeof(float), ComputeBufferType.Default, ComputeBufferMode.SubUpdates); // Uses the LiquidNodeUpdate struct
    GC.SuppressFinalize(updateNodeComputeBuf);
    readNodeComputeBuf = new ComputeBuffer(flattenedBufSize, sizeof(float), ComputeBufferType.Default, ComputeBufferMode.Dynamic);
    GC.SuppressFinalize(readNodeComputeBuf);

    readNodeCPUArr = new float[flattenedBufSize];
  }
  private void initUniforms() {
    if (liquidComputeShader == null) { return; }
    //Debug.Log("Reinitializing liquid compute uniforms...");
    liquidComputeShader.SetFloat("liquidDensity", liquidDensity);
    liquidComputeShader.SetFloat("atmoPressure", atmosphericPressure);
    liquidComputeShader.SetFloat("maxGravityVel", maxGravityVelocity);
    liquidComputeShader.SetFloat("maxPressureVel", maxPressureVelocity);
    liquidComputeShader.SetFloat("unitsPerNode", TerrainGrid.unitsPerNode());
    liquidComputeShader.SetFloat("gravityMagnitude", gravityMagnitude);//Mathf.Abs(Physics.gravity.y));
    liquidComputeShader.SetFloat("friction", friction);
    liquidComputeShader.SetFloat("flowMultiplier", flowMultiplier);
  }

  private void clearBuffersAndRTs() {
    nodeDataRT?.Release(); nodeDataRT = null;
    velRT?.Release(); velRT = null;
    obsticleVelRT?.Release(); obsticleVelRT = null;
    temp3DFloat4RT1?.Release(); temp3DFloat4RT1 = null;
    temp3DFloat4RT2?.Release(); temp3DFloat4RT2 = null;
    temp3DFloatRT?.Release(); temp3DFloatRT = null;
    tempPressurePing?.Release(); tempPressurePing = null;
    tempPressurePong?.Release(); tempPressurePong = null;
    flowsBuffer?.Release(); flowsBuffer?.Dispose(); flowsBuffer = null;
    updateNodeComputeBuf?.Release(); updateNodeComputeBuf?.Dispose(); updateNodeComputeBuf = null;
    readNodeComputeBuf?.Release(); readNodeComputeBuf?.Dispose(); readNodeComputeBuf = null;
  }

  private void Start() {
  }
  private void OnValidate() {
    initUniforms();
  }
  private void OnDestroy() {
    clearBuffersAndRTs();
  }
  
  private RenderTexture init3DRenderTexture(int resSize, RenderTextureFormat format) {
    var result = new RenderTexture(resSize, resSize, 0, format);
    result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
    result.volumeDepth = resSize;
    result.enableRandomWrite = true;
    result.Create();
    clearRT(result, new Color(0,0,0,0));
    return result;
  }

  private void FixedUpdate() {
    if (!enableSimulation || liquidComputeShader == null || volComponent == null) { return; }
    
    liquidComputeShader.SetFloat("dt", applyCFL(Time.fixedDeltaTime));
    advectVelocity();
    applyExternalForces();
    applyVorticityConfinement();
    computeDivergence();
    computePressure();
    projectVelocity();
    calculateAndSumFlows();
    adjustNodesFromFlows();

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
    liquidComputeShader.SetTexture(divergenceKernelId, "obsticleVel", obsticleVelRT);
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
    liquidComputeShader.SetTexture(projectKernelId, "obsticleVel", obsticleVelRT);
    liquidComputeShader.SetTexture(projectKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(projectKernelId, "pressure", tempPressurePing);
    liquidComputeShader.SetTexture(projectKernelId, "projectedVel", temp3DFloat4RT1);
    liquidComputeShader.Dispatch(projectKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
    swapRTs(ref velRT, ref temp3DFloat4RT1); // Put the result into the velocity buffer
  }


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
    liquidComputeShader.SetBuffer(adjustVolFromFlowsKernelId, "flows", flowsBuffer);
    liquidComputeShader.SetTexture(adjustVolFromFlowsKernelId, "summedFlows", temp3DFloatRT);
    liquidComputeShader.SetTexture(adjustVolFromFlowsKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.Dispatch(adjustVolFromFlowsKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
  }

  private float applyCFL(float dt) {
    return Mathf.Min(dt, 1f/30f);
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

  //private void initDebugNodes() {
  //  int debugKernelId = liquidComputeShader.FindKernel("CSFillDebugNodeData");
  //  liquidComputeShader.SetTexture(debugKernelId, "nodeData", nodeDataRT);
  //  liquidComputeShader.Dispatch(debugKernelId, numThreadGroups, numThreadGroups, numThreadGroups);
  //}

  public void clearNodes() {
    int clearKernelId = liquidComputeShader.FindKernel("CSClearNodeData");
    liquidComputeShader.SetTexture(clearKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.Dispatch(clearKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    // Clear the update node buffer
    var fullResSize = volComponent.getFullResSize();
    var bufferCount = fullResSize*fullResSize*fullResSize;
    LiquidNodeUpdate initUpdate;
    initUpdate.terrainIsoVal = 0;
    initUpdate.liquidVolume = 0;
    initUpdate.velocity = Vector3.zero;
    initUpdate.isDiff = 0;
    var nodeCBArr = updateNodeComputeBuf.BeginWrite<LiquidNodeUpdate>(0, bufferCount);
    for (int i = 0; i < bufferCount; i++) {
      nodeCBArr[i] = initUpdate;
    }
    updateNodeComputeBuf.EndWrite<LiquidNodeUpdate>(bufferCount);
    volComponent.updateVolumeData(nodeDataRT);
  }

  /// <summary>
  /// Writes the data from the given nodes (specifically the liquid and barrier values of each node)
  /// into the necessary GPU buffers so that it can be simulated by the Compute shaders.
  /// </summary>
  /// <param name="nodes">The current state of the nodes to be written for simulation.</param>
  public void writeUpdateNodesToLiquid(in TerrainGridNode[,,] nodes) {
    this.writeUpdateNodesAndDebrisToLiquid(nodes, new Dictionary<GameObject, TerrainDebrisDiff>());
  }

  public void writeUpdateNodesAndDebrisToLiquid(in TerrainGridNode[,,] nodes, in Dictionary<GameObject, TerrainDebrisDiff> debrisNodeDict) {
    if (updateNodeComputeBuf == null) { return; }

    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();
    var bufferCount = fullResSize*fullResSize*fullResSize;

    var nodeCBArr = updateNodeComputeBuf.BeginWrite<LiquidNodeUpdate>(0, bufferCount);
    foreach (var node in nodes) {
      var idx = LiquidNodeUpdate.flattenedNodeIdx(node.gridIndex, borderFront, fullResSize);
      LiquidNodeUpdate nodeUpdate;
      nodeUpdate.terrainIsoVal = node.isoVal;
      nodeUpdate.liquidVolume = node.liquidVol;
      nodeUpdate.velocity = Vector3.zero;
      nodeUpdate.isDiff = 1;
      nodeCBArr[idx] = nodeUpdate;
    }
    foreach (var debrisDiffs in debrisNodeDict.Values) {
      foreach (var debrisNode in debrisDiffs.currDebrisNodes) {
        var idx = LiquidNodeUpdate.flattenedNodeIdx(debrisNode.gridIndex, borderFront, fullResSize);
        LiquidNodeUpdate nodeUpdate;
        nodeUpdate.terrainIsoVal = 1;
        nodeUpdate.liquidVolume = 0;
        nodeUpdate.velocity = Vector3.zero;
        nodeUpdate.isDiff = 1;
        nodeCBArr[idx] = nodeUpdate;
      }
    }
    updateNodeComputeBuf.EndWrite<LiquidNodeUpdate>(bufferCount);

    // Update the node data...
    liquidComputeShader.SetBuffer(updateNodesKernelId, "updates", updateNodeComputeBuf);
    liquidComputeShader.SetTexture(updateNodesKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(updateNodesKernelId, "obsticleVel", obsticleVelRT);
    liquidComputeShader.Dispatch(updateNodesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    volComponent.updateVolumeData(nodeDataRT);
  }
 
  public void writeUpdateDebrisDiffToLiquid(
    in Dictionary<GameObject, TerrainDebrisDiff> debrisNodeDict,
    in IEnumerable<TerrainGridNode> liquidChangeNodes
  ) {
    if (updateNodeComputeBuf == null) { return; }

    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();
    var bufferCount = fullResSize*fullResSize*fullResSize;

    var nodeCBArr = updateNodeComputeBuf.BeginWrite<LiquidNodeUpdate>(0, bufferCount);
    foreach (var debrisDiff in debrisNodeDict.Values) {
      foreach (var debrisNode in debrisDiff.prevDebrisNodes) {
        var idx = LiquidNodeUpdate.flattenedNodeIdx(debrisNode.gridIndex, borderFront, fullResSize);
        LiquidNodeUpdate nodeUpdate;
        nodeUpdate.terrainIsoVal = debrisNode.isoVal;
        nodeUpdate.liquidVolume = debrisNode.liquidVol;
        nodeUpdate.velocity = Vector3.zero;
        nodeUpdate.isDiff = 1;
        nodeCBArr[idx] = nodeUpdate;
      }
    }
    foreach (var debrisDiff in debrisNodeDict) {
      
      var currDebrisNodes = debrisDiff.Value.currDebrisNodes;
      var debrisVel = debrisDiff.Key.GetComponent<Rigidbody>().velocity;

      foreach (var debrisNode in currDebrisNodes) {
        var idx = LiquidNodeUpdate.flattenedNodeIdx(debrisNode.gridIndex, borderFront, fullResSize);
        LiquidNodeUpdate nodeUpdate;
        nodeUpdate.terrainIsoVal = 1;
        nodeUpdate.liquidVolume  = 0;
        nodeUpdate.velocity = debrisVel;//Vector3.zero;
        nodeUpdate.isDiff = 1;
        nodeCBArr[idx] = nodeUpdate;
      }
    }
    foreach (var liquidNode in liquidChangeNodes) {
      var idx = LiquidNodeUpdate.flattenedNodeIdx(liquidNode.gridIndex, borderFront, fullResSize);
      LiquidNodeUpdate nodeUpdate;
      nodeUpdate.terrainIsoVal = 0;
      nodeUpdate.liquidVolume = liquidNode.liquidVol;
      nodeUpdate.velocity = Vector3.zero;
      nodeUpdate.isDiff = 1;
      nodeCBArr[idx] = nodeUpdate;
    }
    updateNodeComputeBuf.EndWrite<LiquidNodeUpdate>(bufferCount);

    // Update the node data...
    liquidComputeShader.SetBuffer(updateNodesKernelId, "updates", updateNodeComputeBuf);
    liquidComputeShader.SetTexture(updateNodesKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.SetTexture(updateNodesKernelId, "obsticleVel", obsticleVelRT);
    liquidComputeShader.Dispatch(updateNodesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    volComponent.updateVolumeData(nodeDataRT);
  }

  // TODO: Optimize so that we only read the interior of the 3D buffer?
  public void readUpdateNodesFromLiquid(TerrainGridNode[,,] nodes) {
    if (nodeDataRT == null || readNodeComputeBuf == null) { return; }

    // Read the nodeData texture into the readNodeComputeBuf compute buffer
    liquidComputeShader.SetBuffer(readNodesKernelId, "liquidReadValues", readNodeComputeBuf);
    liquidComputeShader.SetTexture(readNodesKernelId, "nodeData", nodeDataRT);
    liquidComputeShader.Dispatch(readNodesKernelId, numThreadGroups, numThreadGroups, numThreadGroups);

    // Convert the compute buffer into a CPU buffer
    readNodeComputeBuf.GetData(readNodeCPUArr);

    // Appropriately convert the CPU buffer into the given node liquid values
    var borderFront = volComponent.getBorderFront();
    var fullResSize = volComponent.getFullResSize();
    for (int i = 0; i < readNodeCPUArr.Length; i++) {
      var nodeIdx = LiquidNodeUpdate.unflattenedNodeIdx(i, borderFront, fullResSize);

      // Make sure we are only reading the interior (non-border) portion of the array
      if (nodeIdx.x < 0 || nodeIdx.y < 0 || nodeIdx.z < 0 || nodeIdx.x >= nodes.GetLength(0) ||
          nodeIdx.y >= nodes.GetLength(1) || nodeIdx.z >= nodes.GetLength(2)) { continue; }
          
      // We only read the liquid values
      nodes[nodeIdx.x, nodeIdx.y, nodeIdx.z].liquidVol = readNodeCPUArr[i];
    }
  }

}
