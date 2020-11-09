using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainColumn {
  public static int size = 1;

  public TerrainGrid terrain;
  public Vector3Int index { get; private set; } // Index within the TerrainGrid

  // GameObject and Mesh data
  public GameObject gameObj;
  private MeshFilter meshFilter;
  private MeshCollider meshCollider;
  private MeshRenderer meshRenderer;

  public TerrainColumn(in Vector3Int _index, in TerrainGrid _terrain) {
    index = _index;
    terrain = _terrain;
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
      //meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/DirtGrass1Mat");
    }
    gameObj.transform.position = TerrainColumn.size * (Vector3)_index;
  }

  private int numNodesX() { return TerrainColumn.size * TerrainGrid.nodesPerUnit; }
  private int numNodesY() { return terrain.ySize  * TerrainGrid.nodesPerUnit; }
  private int numNodesZ() { return TerrainColumn.size * TerrainGrid.nodesPerUnit; }

  public void clear() {
    GameObject.DestroyImmediate(gameObj);
  }

  public void regenerateMesh() {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var materials = new List<Material>();

    var _numNodesX = numNodesX();
    var _numNodesY = numNodesY();
    var _numNodesZ = numNodesZ();

    var corners = new CubeCorner[CubeCorner.numCorners];
    for (int i = 0; i < CubeCorner.numCorners; i++) { corners[i] = new CubeCorner(); }

    var localIdx = new Vector3Int();
    for (int x = -1; x <= _numNodesX; x++) {
      localIdx.x = x;
      for (int y = -1; y < _numNodesY; y++) {
        localIdx.y = y;
        for (int z = -1; z <= _numNodesZ; z++) {
          localIdx.z = z;
          
          var terrainIdx = terrain.terrainColumnNodeIndex(this, localIdx); // "global" index within the whole terrain
          for (int i = 0; i < CubeCorner.numCorners; i++) {
            // Get the node at the current index in the grid 
            // (also gets empty "ghost" nodes at the edges)
            var cornerNode = terrain.getNode(terrainIdx + MarchingCubes.corners[i]);
            corners[i].setFromNode(cornerNode, -gameObj.transform.position);
          }
          MarchingCubes.polygonize(corners, ref materials, ref triangles, ref vertices);
        }
      }
    }

    // If we're at the near or far extents of the grid then we include one layer of the outside coordinates.
    // We do this to avoid culling the triangles that make up the outer walls of the terrain.
    var outerLayerAmt = terrain.unitsPerNode();
    var minXZPt = new Vector2(Mathf.Min(index.x-outerLayerAmt,0), Mathf.Min(index.z-outerLayerAmt,0));
    var maxXZPt = Vector2.zero + (new Vector2(TerrainColumn.size,TerrainColumn.size));
    var extentNodeIdx = terrain.terrainColumnNodeIndex(this, new Vector3Int(_numNodesX, _numNodesY, _numNodesZ));
    maxXZPt.x += extentNodeIdx.x >= terrain.numNodesX() ? outerLayerAmt : 0;
    maxXZPt.y += extentNodeIdx.z >= terrain.numNodesZ() ? outerLayerAmt : 0;

    // Subtract/Add an epsilon to avoid removing vertices at the edges
    var boundEpsilon = 1e-4f;
    minXZPt.x -= boundEpsilon; minXZPt.y -= boundEpsilon;
    maxXZPt.x += boundEpsilon; maxXZPt.y += boundEpsilon;

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();

    // Split the mesh triangles up into their respective material groups (i.e., submeshes)
    (var submeshTris, var submeshMats) = MeshHelper.Submeshify(triangles, materials, Resources.Load<Material>("Materials/DirtGrass1Mat"));
    meshRenderer.sharedMaterials = submeshMats;
    mesh.subMeshCount = submeshTris.GetLength(0);
    for (int i = 0; i < submeshTris.GetLength(0); i++) {
      mesh.SetTriangles(submeshTris[i], i);
    }

    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance, minXZPt, maxXZPt);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;
  }

}
