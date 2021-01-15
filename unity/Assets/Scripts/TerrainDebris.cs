using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainDebris : MonoBehaviour {
  // TODO: Remove this and use materials to define the density!
  private static readonly float DEFAULT_DENSITY = 1000.0f; // kg/m^3

  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private MeshCollider meshCollider;
  private Rigidbody rigidBody;

  // Original nodes/corners used to build the debris in local space
  private CubeCorner[,,] _originalCorners;
  public CubeCorner[,,] originalCorners { 
    set { _originalCorners = value; } 
  }

  public CubeCorner MapFromWorldspace(in Vector3 wsPos) {
    Vector3 lsPos = transform.InverseTransformPoint(wsPos); // world space to local space

    // Local space to index space and clamp the index space position into the original grid size for the debris
    int x = Mathf.Clamp(Mathf.RoundToInt(lsPos.x/TerrainGrid.UnitsPerNode() + _originalCorners.GetLength(0)/2.0f), 0, _originalCorners.GetLength(0)-1);
    int y = Mathf.Clamp(Mathf.RoundToInt(lsPos.y/TerrainGrid.UnitsPerNode() + _originalCorners.GetLength(1)/2.0f), 0, _originalCorners.GetLength(1)-1);
    int z = Mathf.Clamp(Mathf.RoundToInt(lsPos.z/TerrainGrid.UnitsPerNode() + _originalCorners.GetLength(2)/2.0f), 0, _originalCorners.GetLength(2)-1);

    return _originalCorners[x,y,z];
  }

  public static float GetDrag(in Bounds bounds) {
    return 0.01f * Mathf.Max(0.1f, (bounds.size.x * bounds.size.z));
  }

  private void Awake() {
    meshFilter = GetComponent<MeshFilter>(); 
    meshRenderer = GetComponent<MeshRenderer>(); 
    meshCollider = GetComponent<MeshCollider>();
    rigidBody = GetComponent<Rigidbody>();
  }

  // Takes a 3D array of localspace nodes and generates the mesh for this debris
  public void RegenerateMesh(TerrainGrid terrainGrid, CubeCorner[,,] lsNodes) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var materials = new List<Tuple<Material[],float[]>>();

    var corners = new CubeCorner[CubeCorner.NUM_CORNERS];
    for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) { corners[i] = new CubeCorner(); }

    for (int x = 0; x < lsNodes.GetLength(0)-1; x++) {
      for (int y = 0; y < lsNodes.GetLength(1)-1; y++) {
        for (int z = 0; z < lsNodes.GetLength(2)-1; z++) {
          ref readonly var node = ref lsNodes[x,y,z];
          for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
            ref readonly var cornerInc = ref MarchingCubes.corners[i];
            var cornerNode = lsNodes[x+cornerInc.x, y+cornerInc.y, z+cornerInc.z];
            corners[i].position = cornerNode.position;
            corners[i].isoVal = cornerNode.isoVal;
            corners[i].materials = cornerNode.materials;
          }
          MarchingCubes.Polygonize(corners, materials, triangles, vertices, false);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();

    // Split the mesh triangles up into their respective material groups (i.e., submeshes)
    var terrainAssets = terrainGrid.terrainAssetContainer;
    MeshHelper.Submeshify(ref mesh, ref meshRenderer, ref materials, triangles, 
      terrainAssets.defaultTerrainMaterial, terrainAssets.triplanar3BlendMaterial);

    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance);
    mesh.RecalculateBounds();

    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    for (int i = 0; i < meshRenderer.materials.Length; i++) {
      meshRenderer.materials[i].SetInt("IsTerrain", 0);
    }

    // TODO: Calculate the mass and drag based on the density of the material and the volume of the mesh
    rigidBody.SetDensity(DEFAULT_DENSITY);
    rigidBody.mass = Mathf.Max(1.0f, DEFAULT_DENSITY * mesh.CalculateVolume());
    rigidBody.drag = GetDrag(mesh.bounds);

    originalCorners = lsNodes;
  }

  public void OnDebrisFellOff(GameObject eventGO) {
    if (eventGO == gameObject) {
      //Debug.Log("onDebrisFellOff - destroying TerrainDebris GameObject (in TerrainDebris).");
      GameObject.Destroy(gameObject);
    }
  }
}
