using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class TerrainColumn : MonoBehaviour {
  public static readonly int SIZE = 1;
  public static readonly float HALF_SIZE = SIZE * 0.5f;
  public static readonly float MIN_LANDING_OVERHANG_UNITS = SIZE*2;
  public static readonly int MIN_LANDING_OVERHANG_NODES = (int)(TerrainGrid.NODES_PER_UNIT*MIN_LANDING_OVERHANG_UNITS);
  public static readonly int MIN_ADJACENT_LANDING_NODES_ON_AXIS = (TerrainGrid.NODES_PER_UNIT*SIZE)-2;
  public static readonly int NUM_ADJACENT_LANDING_NODES = (TerrainGrid.NODES_PER_UNIT*SIZE)-1;
  public static readonly int MAX_LANDING_HEIGHT_DEVIATION_NODES = 1;
  public static readonly float BOUNDS_EPSILON = 1e-4f;

  public Vector3Int index { get; private set; } = new Vector3Int(0,0,0); // Index within the TerrainGrid
  public List<TerrainColumnLanding> landings { get; private set; } = new List<TerrainColumnLanding>();

  // GameObject and Mesh data
  private MeshFilter meshFilter;
  private MeshCollider meshCollider;
  private MeshRenderer meshRenderer;

  public static TerrainColumn GetUniqueTerrainColumn(TerrainGrid terrainGrid, Vector3Int terrainColIdx) {
    var name = GetName(terrainColIdx);

    var terrainColGO = terrainGrid.columnsParent.transform.Find(name)?.gameObject;
    if (!terrainColGO) { 
      terrainColGO = PrefabUtility.InstantiatePrefab((UnityEngine.Object)terrainGrid.terrainAssetContainer.terrainColumnPrefab) as GameObject;
    }

    terrainColGO.transform.SetParent(terrainGrid.columnsParent.transform);
    terrainColGO.name = name;
    terrainColGO.transform.position = TerrainColumn.SIZE * (Vector3)terrainColIdx;

    var terrainCol = terrainColGO.GetComponent<TerrainColumn>();
    if (terrainCol) {
      terrainCol.index = terrainColIdx;
      terrainCol.meshFilter   = terrainColGO.GetComponent<MeshFilter>();
      terrainCol.meshCollider = terrainColGO.GetComponent<MeshCollider>();
      terrainCol.meshRenderer = terrainColGO.GetComponent<MeshRenderer>();
    }

    return terrainCol;
  }


  public static string GetName(Vector3Int idx) {
    return string.Format("Terrain Column ({0},{1})", idx.x, idx.z);
  }

  public Bounds Bounds() { return meshFilter.sharedMesh.bounds;  }
  public int NumNodesX() { return TerrainColumn.SIZE * TerrainGrid.NODES_PER_UNIT; }
  public int NumNodesY(TerrainGrid terrain) { return terrain.ySize  * TerrainGrid.NODES_PER_UNIT; }
  public int NumNodesZ() { return TerrainColumn.SIZE * TerrainGrid.NODES_PER_UNIT; }

  public void GetMeshMinMax(TerrainGrid terrain, out Vector3 minPt, out Vector3 maxPt, bool includeLevelBounds=true) {
    // When building the mesh for a TerrainColumn: 
    // If we're at the near or far extents of the grid then we include one layer of the outside coordinates.
    // We do this to avoid culling the triangles that make up the outer walls of the terrain.
    var outerLayerAmt = TerrainGrid.UnitsPerNode();
    minPt = new Vector3(includeLevelBounds ? Mathf.Min(index.x-outerLayerAmt,0) : 0, float.MinValue, includeLevelBounds ? Mathf.Min(index.z-outerLayerAmt,0) : 0);
    maxPt = new Vector3(TerrainColumn.SIZE, float.MaxValue, TerrainColumn.SIZE);
    if (includeLevelBounds) {
      var extentNodeIdx = TerrainGrid.TerrainColumnNodeIndex(this, new Vector3Int(NumNodesX(), NumNodesY(terrain), NumNodesZ()));
      maxPt.x += extentNodeIdx.x >= terrain.NumNodesX() ? outerLayerAmt : 0;
      maxPt.z += extentNodeIdx.z >= terrain.NumNodesZ() ? outerLayerAmt : 0;
    }

    // Subtract/Add an epsilon to avoid removing vertices at the edges
    minPt.x -= BOUNDS_EPSILON; minPt.z -= BOUNDS_EPSILON;
    maxPt.x += BOUNDS_EPSILON; maxPt.z += BOUNDS_EPSILON;
  }

  public void RegenerateMesh(TerrainGrid terrain) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var materials = new List<Tuple<Material[],float[]>>();

    var _numNodesX = NumNodesX();
    var _numNodesY = NumNodesY(terrain);
    var _numNodesZ = NumNodesZ();

    var corners = CubeCorner.buildEmptyCorners();
    var localIdx = new Vector3Int();
    for (int x = -1; x <= _numNodesX; x++) {
      localIdx.x = x;
      for (int y = -1; y < _numNodesY; y++) {
        localIdx.y = y;
        for (int z = -1; z <= _numNodesZ; z++) {
          localIdx.z = z;
          
          var terrainIdx = TerrainGrid.TerrainColumnNodeIndex(this, localIdx); // "global" index within the whole terrain
          for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
            // Get the node at the current index in the grid (also gets empty "ghost" nodes at the edges)
            var cornerNode = terrain.GetNode(terrainIdx + MarchingCubes.corners[i]);
            corners[i].setFromNode(cornerNode, -transform.position);
          }
          MarchingCubes.Polygonize(corners, materials, triangles, vertices);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();

    // Split the mesh triangles up into their respective material groups (i.e., submeshes)
    var terrainAssets = terrain.terrainAssetContainer;
    MeshHelper.Submeshify(ref mesh, ref meshRenderer, ref materials, triangles, 
      terrainAssets.defaultTerrainMaterial, terrainAssets.triplanar3BlendMaterial);

    Vector3 minPt, maxPt;
    GetMeshMinMax(terrain, out minPt, out maxPt);

    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance, minPt, maxPt);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    RegenerateLandings(terrain);
  }

  private void RegenerateLandings(TerrainGrid terrain) {
    landings.Clear();
    var idxRange = terrain.GetIndexRangeForTerrainColumn(this);
    idxRange.yEndIdx -= (MIN_LANDING_OVERHANG_NODES-1);

    // Build an array of all empty nodes (with enough overhang) in each node column within this TerrainColumn
    var tempIdx = new Vector3Int();
    var availNodeArr = new List<int>[idxRange.xEndIdx-idxRange.xStartIdx+1,idxRange.zEndIdx-idxRange.zStartIdx+1];
    for (int x = idxRange.xStartIdx; x <= idxRange.xEndIdx; x++) {
      for (int z = idxRange.zStartIdx; z <= idxRange.zEndIdx; z++) {
        var availNodeList = availNodeArr[x-idxRange.xStartIdx,z-idxRange.zStartIdx] = new List<int>();
        for (int y = idxRange.yStartIdx; y <= idxRange.yEndIdx; y++) {
          tempIdx.Set(x,y,z);
          var node = terrain.GetNode(tempIdx);
          var nodeIsTerrain = node.isTerrain();
          tempIdx.Set(x,y+1,z);
          var aboveNode = terrain.GetNode(tempIdx);
          var aboveNodeIsTerrain = aboveNode.isTerrain();
          if ((y == 0 && !nodeIsTerrain && !aboveNodeIsTerrain) || (nodeIsTerrain && !aboveNodeIsTerrain)) {
            // Check whether there's an overhang for a landing
            bool isOverhang = true;
            for (int i = 2; i < MIN_LANDING_OVERHANG_NODES; i++) {
              tempIdx.Set(x,y+i,z);
              var overhangNode = terrain.GetNode(tempIdx);
              if (overhangNode.isTerrain()) { isOverhang = false; break; }
            }
            if (isOverhang) {
              availNodeList.Add(y);
              y += MIN_LANDING_OVERHANG_NODES;
            }
          }
        }
      }
    }

    var numXNodes = NumNodesX();
    var numZNodes = NumNodesZ();

    // Go through the available nodes and check to see if there are enough nodes clustered
    // together in adjacent colunns with approximately the same y level to make up landings
    for (int x = 0; x <= availNodeArr.GetLength(0)-NUM_ADJACENT_LANDING_NODES; x++) {
      for (int z = 0; z <= availNodeArr.GetLength(1)-NUM_ADJACENT_LANDING_NODES; z++) {
        var availNodeList = availNodeArr[x,z];
        for (int i = 0; i < availNodeList.Count; i++) {
          int y = availNodeList[i];

          int landingNodeCount = 1;

          int numConsecutiveX = 0;
          int numConsecutiveZ = 0;

          Vector3Int landingMin = new Vector3Int(x, y, z);
          Vector3Int landingMax = new Vector3Int(0, y, 0);

          for (int ax = x; ax < numXNodes; ax++) {
            numConsecutiveZ = 0;
            for (int az = z; az < numZNodes; az++) {

              if (ax == x && az == z) {
                numConsecutiveX = 1;
                numConsecutiveZ = 1;
                continue;
              }

              var adjAvailNodeList = availNodeArr[ax,az];
              bool foundNode = false;
              for (int j = 0; j < adjAvailNodeList.Count; j++) {
                var adjY = adjAvailNodeList[j];
                if (Math.Abs(y-adjY) < MAX_LANDING_HEIGHT_DEVIATION_NODES) {
                  landingNodeCount++;
                  
                  landingMin.y = Math.Min(landingMin.y, adjY);
                  landingMax = Vector3Int.Max(landingMax, new Vector3Int(ax, adjY, az));
                  
                  adjAvailNodeList.RemoveAt(j);
                  foundNode = true;
                  break;
                }
                else if (adjY > y) { break; }
              }
              if (foundNode) {
                numConsecutiveZ++;
              }
            }
            if (numConsecutiveZ >= MIN_ADJACENT_LANDING_NODES_ON_AXIS) {
              numConsecutiveX++;
            }
          }

          availNodeList.RemoveAt(i);
          i--;

          // Did we find a landing (i.e., a square of a reasonable size of level nodes)?
          if (numConsecutiveX >= MIN_ADJACENT_LANDING_NODES_ON_AXIS) {
            // Map the x and z into the node index space (from TerrainColumn local space)
            landingMin.x += idxRange.xStartIdx;
            landingMin.z += idxRange.zStartIdx;
            landingMax.x += idxRange.xStartIdx;
            landingMax.z += idxRange.zStartIdx;
            // Create the landing...
            var landing = TerrainColumnLanding.GetUniqueTerrainColumnLanding(this, terrain.terrainAssetContainer.terrainColumnLandingPrefab, landingMin, landingMax);
            if (landing) {
              landing.RegenerateMesh(terrain, this);
              landings.Add(landing);
            }
          }
        }
      }
    }
  }

}
