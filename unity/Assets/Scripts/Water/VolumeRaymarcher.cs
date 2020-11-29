using System;
using UnityEngine;

public class VolumeRaymarcher : MonoBehaviour {
  private static readonly int JITTER_TEX_SIZE = 256;
  public Texture2D jitterTexture;

  // Assigned in Awake/Start
  private int volResolution;
  private Vector3 resBorder;
  private Vector3Int resBorderFrontInt;
  private Vector3Int resBorderBackInt;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private TerrainGrid terrainGrid;

  public Vector3Int getBorderFront() { return resBorderFrontInt; }
  public Vector3Int getBorderBack()  { return resBorderBackInt;  }
  public int getFullResSize() { return volResolution; }

  private Vector3 getVolumeUnitSize() {
    Debug.Assert(terrainGrid != null);
    return new Vector3(terrainGrid.xSize, terrainGrid.ySize, terrainGrid.zSize)*TerrainColumn.size;
  }

  public void initResolutionInfo() {
    if (terrainGrid != null) { return; }
    // Find the terrain grid component, this must exist and be initialized before this function is called!
    terrainGrid = GetComponent<TerrainGrid>();
    if (!terrainGrid) {
      Debug.LogError("Could not find TerrainGrid component on GameObject!");
      return;
    }

    // Calculate the resolution of the 3D texture for rendering into the slices
    var numNodesVec = new Vector3(terrainGrid.numNodesX(), terrainGrid.numNodesY(), terrainGrid.numNodesZ());

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

    Debug.Log("Resolution: " + volResolution + ", number of nodes: " + numNodesVec);
    Debug.Log("Border (Float): " + resBorder + ", Front (Int): " + resBorderFrontInt + ", Back (Int): " + resBorderBackInt);
  }

  void Start() {
    initResolutionInfo();
    var volumeUnitSize = getVolumeUnitSize();

    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) { meshFilter = gameObject.AddComponent<MeshFilter>(); }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) { meshRenderer = gameObject.AddComponent<MeshRenderer>(); }
    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    meshRenderer.allowOcclusionWhenDynamic = false;
    meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    if (!jitterTexture) { jitterTexture = TextureHelper.buildJitterTexture2D(JITTER_TEX_SIZE, true); }

    // Set the raycasting material
    if (meshRenderer.sharedMaterial == null) {
      meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/VolumeRaymarchMat"); // "Materials/DebugDiffuseMat"
      meshRenderer.material.SetVector("boundsMax", transform.localToWorldMatrix * volumeUnitSize);
      meshRenderer.material.SetVector("boundsMin", transform.localToWorldMatrix * new Vector3(0,0,0));
      meshRenderer.material.SetVector("borderFront", new Vector3(resBorderFrontInt.x, resBorderFrontInt.y, resBorderFrontInt.z));
      meshRenderer.material.SetVector("borderBack", new Vector3(resBorderBackInt.x, resBorderBackInt.y, resBorderBackInt.z));
      meshRenderer.material.SetFloat("resolution", volResolution);
      meshRenderer.material.SetFloat("nodeVolume", Mathf.Pow(TerrainGrid.unitsPerNode(),3));
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

  //void OnDrawGizmos() {
  //  Gizmos.DrawWireCube(halfUnitSize(), volumeUnitSize);
  //}

  public void updateVolumeData(RenderTexture rt) {
    meshRenderer.material.SetTexture("nodeTex", rt);
  }
}
