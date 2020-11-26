using System;
using System.Collections.Generic;
using UnityEngine;

public class VolumeRaymarcher : MonoBehaviour {
  public Vector3 volumeUnitSize = new Vector3(20,20,20);

  private static int defaultJitterTexSize = 256;
  public Texture2D jitterTexture;

  // Assigned in Start()
  private int volResolution;
  private Vector3 resBorder;
  private Vector3Int resBorderFrontInt;
  private Vector3Int resBorderBackInt;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  private Vector3 halfUnitSize() { return 0.5f*volumeUnitSize; }

  public Vector3Int getBorderFront() { return resBorderFrontInt; }
  public Vector3Int getBorderBack()  { return resBorderBackInt;  }
  public int getFullResSize() { return volResolution; }

  void Awake() {
    // Calculate the resolution of the 3D texture for rendering into the slices
    var numNodesVec = TerrainGrid.nodesPerUnit * volumeUnitSize - 
      new Vector3(volumeUnitSize.x/TerrainColumn.size-1, volumeUnitSize.y/TerrainColumn.size-1, volumeUnitSize.z/TerrainColumn.size-1);
    // Calculate the maximum resolution (there must be at least one voxel per node on each axis 
    // plus 2 voxels for a border of 1 voxel on either side)
    var maxRes = Mathf.CeilToInt(Math.Max(numNodesVec.x, Math.Max(numNodesVec.y, numNodesVec.z)));
    volResolution = MathHelper.nextMultipleOf(maxRes+2, WaterCompute.NUM_THREADS_PER_BLOCK);//Mathf.NextPowerOfTwo(maxRes+2);

    // Calculate the border based on the final resolution and the resolution we actually need to hold all the nodes
    resBorder = 0.5f * (new Vector3(volResolution, volResolution, volResolution) - numNodesVec);
    // The border may not be an exact integer vector, get the nearest whole non-zero integer border as well
    resBorderFrontInt = new Vector3Int((int)resBorder.x, (int)resBorder.y, (int)resBorder.z);
    Debug.Assert(resBorderFrontInt.x > 0 && resBorderFrontInt.y > 0 && resBorderFrontInt.z > 0);
    var resBorderBack = 2*resBorder - resBorderFrontInt;
    resBorderBackInt = new Vector3Int((int)resBorderBack.x, (int)resBorderBack.y, (int)resBorderBack.z);

    Debug.Log("Resolution: " + volResolution);
    Debug.Log("Border (Float): " + resBorder + ", Front (Int): " + resBorderFrontInt + ", Back (Int): " + resBorderBackInt);
  }

  void Start() {
    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) { meshFilter = gameObject.AddComponent<MeshFilter>(); }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) { meshRenderer = gameObject.AddComponent<MeshRenderer>(); }
    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    meshRenderer.allowOcclusionWhenDynamic = false;
    meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    if (!jitterTexture) { jitterTexture = TextureHelper.buildJitterTexture2D(defaultJitterTexSize, true); }

    // Set the raycasting material
    if (meshRenderer.sharedMaterial == null) {
      meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/VolumeRaymarchMat"); // "Materials/DebugDiffuseMat"
      meshRenderer.material.SetVector("boundsMax", transform.localToWorldMatrix * volumeUnitSize);
      meshRenderer.material.SetVector("boundsMin", transform.localToWorldMatrix * new Vector3(0,0,0));
      meshRenderer.material.SetVector("borderFront", new Vector3(resBorderFrontInt.x, resBorderFrontInt.y, resBorderFrontInt.z));
      meshRenderer.material.SetVector("borderBack", new Vector3(resBorderBackInt.x, resBorderBackInt.y, resBorderBackInt.z));
      meshRenderer.material.SetFloat("resolution", volResolution);
      meshRenderer.material.SetTexture("jitterTex", jitterTexture);
    }

    // Build the bounding box used to render the volume via raymarching between its faces
    var mesh = new Mesh();
    int[] triangles = null;
    Vector3[] vertices = null;
    MeshHelper.BuildCubeData(volumeUnitSize, out triangles, out vertices);
    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    meshFilter.mesh = mesh;
  }

  //void Update() {
    //meshRenderer.material.SetTexture("nodeTex", volRenderTex);
  //}

  //void OnDestroy() {
  //  volRenderTex?.Release();
  //}

  void OnDrawGizmosSelected() {
    Gizmos.DrawWireCube(halfUnitSize(), volumeUnitSize);
  }

  public void updateVolumeData(RenderTexture rt) {
    meshRenderer.material.SetTexture("nodeTex", rt);
  }
}
