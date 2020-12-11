using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainColumn {

  public class Landing {
    public Vector3Int minIdx;
    public Vector3Int maxIdx;

    public GameObject gameObj;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    public Landing(in TerrainColumn terrainColumn, Vector3Int min, Vector3Int max) {
      Debug.Assert(min.x <= max.x && min.y <= max.y && min.z <= max.z);
      minIdx = min;
      maxIdx = max;

      var name = string.Format("Landing [{0} - {1}]", min, max);
      var existingGO = terrainColumn.gameObj.transform.Find(name)?.gameObject;
      if (existingGO != null) {
        gameObj = existingGO;
        meshFilter = gameObj.GetComponent<MeshFilter>();
        meshRenderer = gameObj.GetComponent<MeshRenderer>();
      }
      else {
        gameObj = new GameObject();
        gameObj.name = name;
        //gameObj.tag = "Highlightable";
        meshFilter   = gameObj.AddComponent<MeshFilter>();
        meshRenderer = gameObj.AddComponent<MeshRenderer>();
      }
      gameObj.transform.SetParent(terrainColumn.gameObj.transform);
      gameObj.transform.localPosition = Vector3.zero;

      var terrain = terrainColumn.terrain;
      var unitsPerNode = TerrainGrid.unitsPerNode();
      var isoInc = unitsPerNode*MarchingCubes.ISOVAL_CUTOFF;

      // Generate the landing mesh
      var vertices = new List<Vector3>();
      var triangles = new List<int>();
      var mesh = new Mesh();

      if (minIdx.y == 0 && maxIdx.y == 0) {
        // Handle the special case where the ground is bedrock - just draw a plane at y==0
        var minTCIdx = terrain.terrainColumnNodeIndex(terrainColumn, new Vector3Int(0,0,0)); // "global" index within the whole terrain

        bool isTCXMinEdge = terrainColumn.index.x == 0;
        bool isTCXMaxEdge = terrainColumn.index.x == terrain.xSize-1;
        bool isTCZMinEdge = terrainColumn.index.z == 0;
        bool isTCZMaxEdge = terrainColumn.index.z == terrain.zSize-1;

        var xMinInsetNodes = minIdx.x-minTCIdx.x;
        var zMinInsetNodes = minIdx.z-minTCIdx.z;
        var xMaxInsetNodes = (minTCIdx.x + TerrainGrid.nodesPerUnit*TerrainColumn.SIZE-1) - maxIdx.x;
        var zMaxInsetNodes = (minTCIdx.z + TerrainGrid.nodesPerUnit*TerrainColumn.SIZE-1) - maxIdx.z;

        bool isInsetMinX = xMinInsetNodes > 0; bool isInsetMinZ = zMinInsetNodes > 0;
        bool isInsetMaxX = xMaxInsetNodes > 0; bool isInsetMaxZ = zMaxInsetNodes > 0;

        var minPt = new Vector3(
          isTCXMinEdge ? -isoInc : isInsetMinX ? xMinInsetNodes*unitsPerNode-isoInc : 0, 0, 
          isTCZMinEdge ? -isoInc : isInsetMinZ ? zMinInsetNodes*unitsPerNode-isoInc : 0);
        var maxPt = new Vector3(
          TerrainColumn.SIZE + (isTCXMaxEdge ? isoInc : isInsetMaxX ? -xMaxInsetNodes*unitsPerNode+isoInc : 0), 0, 
          TerrainColumn.SIZE + (isTCZMaxEdge ? isoInc : isInsetMaxZ ? -zMaxInsetNodes*unitsPerNode+isoInc : 0));

        vertices.Add(minPt);
        vertices.Add(new Vector3(minPt.x, minPt.y, maxPt.z));
        vertices.Add(new Vector3(maxPt.x, minPt.y, maxPt.z));
        vertices.Add(new Vector3(maxPt.x, minPt.y, minPt.z));
        triangles.Add(0); triangles.Add(1); triangles.Add(2);
        triangles.Add(0); triangles.Add(2); triangles.Add(3);
        mesh.SetVertices(vertices);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        mesh.RecalculateNormals(); 
      }
      else {
        // We need the specialized mesh from marching cubes: This is a duplicate of the one used in the TerrainColumn, 
        // but it's isolated and allows us to highlight and apply effects to the landing specifically
        var corners = CubeCorner.buildEmptyCorners();
        var numNodesX = terrainColumn.numNodesX();
        var numNodesZ = terrainColumn.numNodesZ();
        var localIdx = new Vector3Int();
        for (int y = min.y-1; y <= max.y+1; y++) {
          localIdx.y = y;
          for (int x = -1; x <= numNodesX; x++) {
            localIdx.x = x;
            for (int z = -1; z <= numNodesZ; z++) {
              localIdx.z = z;

              var terrainIdx = terrain.terrainColumnNodeIndex(terrainColumn, localIdx); // "global" index within the whole terrain
              for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
                // Get the node at the current index in the grid (also gets empty "ghost" nodes at the edges)
                var cornerNode = terrain.getNode(terrainIdx + MarchingCubes.corners[i]);
                corners[i].setFromNode(cornerNode, -terrainColumn.gameObj.transform.position);
              }
              MarchingCubes.polygonizeMeshOnly(corners, triangles, vertices);
            }
          }
        }

        mesh.SetVertices(vertices);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        
        Vector3 minPt, maxPt;
        terrainColumn.getMeshMinMax(out minPt, out maxPt, false);
        //minPt.x -= (isoInc + TerrainColumn.BOUNDS_EPSILON); maxPt.x += isoInc + TerrainColumn.BOUNDS_EPSILON;
        //minPt.z -= (isoInc + TerrainColumn.BOUNDS_EPSILON); maxPt.z += isoInc + TerrainColumn.BOUNDS_EPSILON;
        minPt.y = minIdx.y*TerrainGrid.unitsPerNode() - TerrainColumn.BOUNDS_EPSILON;
        maxPt.y = maxIdx.y*TerrainGrid.unitsPerNode() + isoInc + TerrainColumn.BOUNDS_EPSILON;
        
        mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance, minPt, maxPt);
        mesh.RecalculateBounds();
      }

      meshFilter.sharedMesh = mesh;
      meshRenderer.material = Resources.Load<Material>("Materials/TerrainHighlightMat");
      
    }

    public void clear() {
      GameObject.DestroyImmediate(gameObj);
    }
  }

  public static readonly int SIZE = 1;
  public static readonly float MIN_LANDING_OVERHANG_UNITS = SIZE*2-2*TerrainGrid.unitsPerNode();
  public static readonly int MIN_LANDING_OVERHANG_NODES = (int)(TerrainGrid.nodesPerUnit*MIN_LANDING_OVERHANG_UNITS);
  public static readonly int NUM_ADJACENT_LANDING_NODES = (TerrainGrid.nodesPerUnit*SIZE)-1;
  public static readonly int NUM_ADJACENT_LANDING_NODES_CHECK = NUM_ADJACENT_LANDING_NODES*NUM_ADJACENT_LANDING_NODES-NUM_ADJACENT_LANDING_NODES;
  public static readonly int MAX_LANDING_HEIGHT_DEVIATION_NODES = 1;
  public static readonly float BOUNDS_EPSILON = 1e-4f;

  public TerrainGrid terrain;
  public Vector3Int index { get; private set; } // Index within the TerrainGrid
  public List<Landing> landings { get; private set; }

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
    }
    gameObj.transform.position = TerrainColumn.SIZE * (Vector3)_index;
  }

  private int numNodesX() { return TerrainColumn.SIZE * TerrainGrid.nodesPerUnit; }
  private int numNodesY() { return terrain.ySize  * TerrainGrid.nodesPerUnit; }
  private int numNodesZ() { return TerrainColumn.SIZE * TerrainGrid.nodesPerUnit; }

  private void clearLandingRanges() {
    if (landings != null) {
      foreach (var landing in landings) {
        landing.clear();
      }
    }
  }
  public void clear() {
    clearLandingRanges();
    GameObject.DestroyImmediate(gameObj);
  }

  private void getMeshMinMax(out Vector3 minPt, out Vector3 maxPt, bool includeLevelBounds=true) {
    // When building the mesh for a TerrainColumn: 
    // If we're at the near or far extents of the grid then we include one layer of the outside coordinates.
    // We do this to avoid culling the triangles that make up the outer walls of the terrain.
    var outerLayerAmt = TerrainGrid.unitsPerNode();
    minPt = new Vector3(includeLevelBounds ? Mathf.Min(index.x-outerLayerAmt,0) : 0, float.MinValue, includeLevelBounds ? Mathf.Min(index.z-outerLayerAmt,0) : 0);
    maxPt = new Vector3(TerrainColumn.SIZE, float.MaxValue, TerrainColumn.SIZE);
    if (includeLevelBounds) {
      var extentNodeIdx = terrain.terrainColumnNodeIndex(this, new Vector3Int(numNodesX(), numNodesY(), numNodesZ()));
      maxPt.x += extentNodeIdx.x >= terrain.numNodesX() ? outerLayerAmt : 0;
      maxPt.z += extentNodeIdx.z >= terrain.numNodesZ() ? outerLayerAmt : 0;
    }

    // Subtract/Add an epsilon to avoid removing vertices at the edges
    minPt.x -= BOUNDS_EPSILON; minPt.z -= BOUNDS_EPSILON;
    maxPt.x += BOUNDS_EPSILON; maxPt.z += BOUNDS_EPSILON;
  }

  public void regenerateMesh() {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var materials = new List<Tuple<Material[],float[]>>();

    var _numNodesX = numNodesX();
    var _numNodesY = numNodesY();
    var _numNodesZ = numNodesZ();

    var corners = CubeCorner.buildEmptyCorners();
    var localIdx = new Vector3Int();
    for (int x = -1; x <= _numNodesX; x++) {
      localIdx.x = x;
      for (int y = -1; y < _numNodesY; y++) {
        localIdx.y = y;
        for (int z = -1; z <= _numNodesZ; z++) {
          localIdx.z = z;
          
          var terrainIdx = terrain.terrainColumnNodeIndex(this, localIdx); // "global" index within the whole terrain
          for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
            // Get the node at the current index in the grid (also gets empty "ghost" nodes at the edges)
            var cornerNode = terrain.getNode(terrainIdx + MarchingCubes.corners[i]);
            corners[i].setFromNode(cornerNode, -gameObj.transform.position);
          }
          MarchingCubes.polygonize(corners, materials, triangles, vertices);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();

    // Split the mesh triangles up into their respective material groups (i.e., submeshes)
    MeshHelper.Submeshify(ref mesh, ref meshRenderer, ref materials, triangles, MaterialHelper.defaultMaterial);

    Vector3 minPt, maxPt;
    getMeshMinMax(out minPt, out maxPt);

    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance, minPt, maxPt);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    regenerateLandings();
  }

  private void regenerateLandings() {
    clearLandingRanges();
    landings = new List<Landing>();

    var idxRange = terrain.getIndexRangeForTerrainColumn(this);
    idxRange.yEndIdx -= (MIN_LANDING_OVERHANG_NODES-1);

    // Build an array of all empty nodes (with enough overhang) in each node column within this TerrainColumn
    var tempIdx = new Vector3Int();
    var availNodeArr = new List<int>[idxRange.xEndIdx-idxRange.xStartIdx+1,idxRange.zEndIdx-idxRange.zStartIdx+1];
    for (int x = idxRange.xStartIdx; x <= idxRange.xEndIdx; x++) {
      for (int z = idxRange.zStartIdx; z <= idxRange.zEndIdx; z++) {
        var availNodeList = availNodeArr[x-idxRange.xStartIdx,z-idxRange.zStartIdx] = new List<int>();
        for (int y = idxRange.yStartIdx; y <= idxRange.yEndIdx; y++) {
          tempIdx.Set(x,y,z);
          var node = terrain.getNode(tempIdx);
          var nodeIsTerrain = node.isTerrain();
          tempIdx.Set(x,y+1,z);
          var aboveNode = terrain.getNode(tempIdx);
          var aboveNodeIsTerrain = aboveNode.isTerrain();
          if ((y == 0 && !nodeIsTerrain && !aboveNodeIsTerrain) || (nodeIsTerrain && !aboveNodeIsTerrain)) {
            // Check whether there's an overhang for a landing
            bool isOverhang = true;
            for (int i = 2; i < MIN_LANDING_OVERHANG_NODES; i++) {
              tempIdx.Set(x,y+i,z);
              var overhangNode = terrain.getNode(tempIdx);
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

    var numXNodes = numNodesX();
    var numZNodes = numNodesZ();

    // if (index.x == 5 && index.z == 8) {
    //   for (int x = 0; x < availNodeArr.GetLength(0); x++) {
    //     for (int z = 0; z < availNodeArr.GetLength(1); z++) {
    //       Debug.Log(string.Join(",",availNodeArr[x,z]));
    //     }
    //   }
    // }

    // Go through the available nodes and check to see if there are enough nodes clustered
    // together in adjacent colunns with approximately the same y level to make up landings
    for (int x = 0; x <= availNodeArr.GetLength(0)-NUM_ADJACENT_LANDING_NODES; x++) {
      for (int z = 0; z <= availNodeArr.GetLength(1)-NUM_ADJACENT_LANDING_NODES; z++) {
        var availNodeList = availNodeArr[x,z];
        for (int i = 0; i < availNodeList.Count; i++) {
          int y = availNodeList[i];

          int landingNodeCount = 1;
          Vector3Int landingMin = new Vector3Int(x, y, z);
          Vector3Int landingMax = new Vector3Int(0, y, 0);
          for (int ax = x; ax < numXNodes; ax++) {
            for (int az = z; az < numZNodes; az++) {
              if (ax == x && az == z) { continue; }
              var adjAvailNodeList = availNodeArr[ax,az];
              for (int j = 0; j < adjAvailNodeList.Count; j++) {
                var adjY = adjAvailNodeList[j];
                if (Math.Abs(y-adjY) <= MAX_LANDING_HEIGHT_DEVIATION_NODES) {
                  landingNodeCount++;
                  
                  landingMin.y = Math.Min(landingMin.y, adjY);
                  landingMax = Vector3Int.Max(landingMax, new Vector3Int(ax, adjY, az));
                  
                  adjAvailNodeList.RemoveAt(j);
                  break;
                }
                else if (adjY > y) { break; }
              }
            }
          }

          availNodeList.RemoveAt(i);
          i--;

          // Did we find a landing (i.e., a square of a reasonable size of level nodes)?
          if (landingNodeCount >= NUM_ADJACENT_LANDING_NODES_CHECK) {
            // Map the x and z into the node index space (from terrain column local space)
            landingMin.x += idxRange.xStartIdx;
            landingMin.z += idxRange.zStartIdx;
            landingMax.x += idxRange.xStartIdx;
            landingMax.z += idxRange.zStartIdx;
            // Generate the landing and its geometry, materials, etc.
            var landing = new Landing(this, landingMin, landingMax);
            landings.Add(landing);
            //Debug.Log("Found a landing for TC " + index + " at " + landingMinY + "-" + landingMaxY);
          }
        }
      }
    }
  }

}
