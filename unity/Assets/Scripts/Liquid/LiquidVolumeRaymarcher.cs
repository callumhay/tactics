using System;
using UnityEngine;
using UnityEditor;

#pragma warning disable 649

[ExecuteAlways]
public class LiquidVolumeRaymarcher : MonoBehaviour {

  [SerializeField] private Texture2D jitterTexture;
  [SerializeField] private RenderTexture nodeTexture;
  [SerializeField] private TerrainGrid terrainGrid;

  private int volResolution;
  private Vector3 resBorder;
  private Vector3Int resBorderFrontInt;
  private Vector3Int resBorderBackInt;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  
  public Vector3Int getBorderFront() { return resBorderFrontInt; }
  public Vector3Int getBorderBack()  { return resBorderBackInt;  }
  public int getFullResSize() { return volResolution; }

  public void InitAll() {
    // Calculate the resolution of the 3D texture for rendering into the slices
    var numNodesVec = new Vector3(terrainGrid.NumNodesX(), terrainGrid.NumNodesY(), terrainGrid.NumNodesZ());

    // Calculate the maximum resolution (there must be at least one voxel per node on each axis 
    // plus 2 voxels for a border of 1 voxel on either side)
    var maxRes = Mathf.CeilToInt(Math.Max(numNodesVec.x, Math.Max(numNodesVec.y, numNodesVec.z)));
    volResolution = MathHelper.nextMultipleOf(maxRes+2, LiquidCompute.NUM_THREADS_PER_BLOCK);//Mathf.NextPowerOfTwo(maxRes+2);

    // Calculate the border based on the final resolution and the resolution we actually need to hold all the nodes
    resBorder = 0.5f * (new Vector3(volResolution, volResolution, volResolution) - numNodesVec);
    // The border may not be an exact integer vector, get the nearest whole non-zero integer border as well
    resBorderFrontInt = new Vector3Int((int)resBorder.x, (int)resBorder.y, (int)resBorder.z);
    Debug.Assert(resBorderFrontInt.x > 0 && resBorderFrontInt.y > 0 && resBorderFrontInt.z > 0);
    var resBorderBack = 2*resBorder - resBorderFrontInt;
    resBorderBackInt = new Vector3Int((int)resBorderBack.x, (int)resBorderBack.y, (int)resBorderBack.z);

    //Debug.Log("Resolution: " + volResolution + ", number of nodes: " + numNodesVec);
    //Debug.Log("Border (Float): " + resBorder + ", Front (Int): " + resBorderFrontInt + ", Back (Int): " + resBorderBackInt);

    var volumeUnitSize = (Vector3)terrainGrid.UnitSizeVec3();
    meshRenderer.sharedMaterial.SetVector("boundsMax", transform.localToWorldMatrix * volumeUnitSize);
    meshRenderer.sharedMaterial.SetVector("boundsMin", transform.localToWorldMatrix * new Vector3(0,0,0));
    meshRenderer.sharedMaterial.SetVector("borderFront", new Vector3(resBorderFrontInt.x, resBorderFrontInt.y, resBorderFrontInt.z));
    meshRenderer.sharedMaterial.SetVector("borderBack", new Vector3(resBorderBackInt.x, resBorderBackInt.y, resBorderBackInt.z));
    meshRenderer.sharedMaterial.SetFloat("resolution", volResolution);
    meshRenderer.sharedMaterial.SetFloat("nodeVolume", Mathf.Pow(TerrainGrid.UnitsPerNode(),3));
    meshRenderer.sharedMaterial.SetTexture("jitterTex", jitterTexture);
    UpdateNodeTexture(nodeTexture);

    // Build the bounding box used to render the volume via raymarching between its faces
    var mesh = new Mesh();
    int[] triangles = null;
    Vector3[] vertices = null;
    MeshHelper.BuildCubeData(volumeUnitSize, out triangles, out vertices);
    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    meshFilter.sharedMesh = mesh;
  }

  private void Awake() {
    meshFilter = GetComponent<MeshFilter>();
    meshRenderer = GetComponent<MeshRenderer>();

    // Make sure we don't clobber the original material in play mode
    if (Application.IsPlaying(gameObject)) {
      meshRenderer.sharedMaterial = Instantiate<Material>(meshRenderer.sharedMaterial);
    }
  }

  public void UpdateNodeTexture(RenderTexture nodeTex) {
    if (nodeTex != null) {
      meshRenderer.sharedMaterial.SetTexture("nodeTex", nodeTex);
    }
    nodeTexture = nodeTex;
  }
}
