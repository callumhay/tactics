using System.Collections.Generic;
using UnityEngine;

public class TerrainColumn {
  public static int size = 1;

  public TerrainGrid terrainGrid;
  public Vector3Int index { get; private set; } // Index within the TerrainGrid

  // GameObject and Mesh data
  public GameObject gameObj;
  private MeshFilter meshFilter;
  private MeshCollider meshCollider;
  private MeshRenderer meshRenderer;

  public TerrainColumn(in Vector3Int _index, in TerrainGrid _terrainGrid) {
    index = _index;
    terrainGrid = _terrainGrid;
    var name = string.Format("Terrain Column ({0},{1})", _index.x, _index.z);
    var existingGameObj = GameObject.Find(name);
    if (existingGameObj != null) {
      gameObj = existingGameObj;
      meshFilter = gameObj.GetComponent<MeshFilter>();
      meshCollider = gameObj.GetComponent<MeshCollider>();
      meshRenderer = gameObj.GetComponent<MeshRenderer>();
    }
    else {
      gameObj = new GameObject();
      gameObj.name = name;
      gameObj.tag = "Terrain";
      meshFilter   = gameObj.AddComponent<MeshFilter>();
      meshCollider = gameObj.AddComponent<MeshCollider>();
      meshRenderer = gameObj.AddComponent<MeshRenderer>();
      meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/TerrainMat");
    }
    gameObj.transform.position = TerrainColumn.size * (Vector3)_index;
  }

  private int numNodesX() { return TerrainColumn.size * terrainGrid.nodesPerUnit; }
  private int numNodesY() { return terrainGrid.ySize * terrainGrid.nodesPerUnit; }
  private int numNodesZ() { return TerrainColumn.size * terrainGrid.nodesPerUnit; }

  public void clear() {
    GameObject.DestroyImmediate(gameObj);
  }

  public void regenerateMesh() {
   var vertices = new List<Vector3>();
   var triangles = new List<int>();

    var _numNodesX = numNodesX();
    var _numNodesY = numNodesY();
    var _numNodesZ = numNodesZ();

    var corners = new CubeCorner[8];
    for (int i = 0; i < 8; i++) { corners[i] = new CubeCorner(); }

    var localIdx = new Vector3Int();
    for (int x = -1; x < _numNodesX; x++) {
      localIdx.x = x;
      for (int y = -1; y < _numNodesY; y++) {
        localIdx.y = y;
        for (int z = -1; z < _numNodesZ; z++) {
          localIdx.z = z;
          
          var terrainIdx = terrainGrid.terrainColumnNodeIndex(this, localIdx); // "global" index within the whole terrain
          for (int i = 0; i < 8; i++) {
            // Get the node at the current index in the grid (also gets empty "ghost" nodes at the edges)
            var cornerNode = terrainGrid.getNode(terrainIdx + MarchingCubes.corners[i]);
            // Localspace position for this Terrain Column
            corners[i].position = cornerNode.position - gameObj.transform.position;
            // Isovalue of the node
            corners[i].isoVal = cornerNode.isoVal;
          }
          MarchingCubes.polygonize(corners, ref triangles, ref vertices);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();
    mesh.triangles = triangles.ToArray();
    mesh.RecalculateNormals(55.0f, 1e-4f);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;
  }

}
