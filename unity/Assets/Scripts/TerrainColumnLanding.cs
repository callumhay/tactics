using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class TerrainColumnLanding : MonoBehaviour {

  public Vector3Int terrainColIdx { get; private set; }
  public Vector3Int minIdx { get; private set; }
  public Vector3Int maxIdx { get; private set; }

  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  public static TerrainColumnLanding GetUniqueTerrainColumnLanding(TerrainColumn terrainCol, Vector3Int min, Vector3Int max) {
    Debug.Assert(terrainCol != null && min.x <= max.x && min.y <= max.y && min.z <= max.z);
    var name = GetName(min, max);
    var landingGO = terrainCol.transform.Find(name)?.gameObject;
    if (landingGO) {
      DestroyImmediate(landingGO);
    }
    
    landingGO = PrefabUtility.InstantiatePrefab((UnityEngine.Object)terrainCol.landingPrefab) as GameObject;
    landingGO.transform.SetParent(terrainCol.transform);
    landingGO.transform.localPosition = Vector3.zero;
    landingGO.name = name;

    var landing = landingGO.GetComponent<TerrainColumnLanding>();
    landing.terrainColIdx = terrainCol.index;
    landing.minIdx = min;
    landing.maxIdx = max;
    landing.meshFilter   = landingGO.GetComponent<MeshFilter>();
    landing.meshRenderer = landingGO.GetComponent<MeshRenderer>();
    return landing;
  }

  public static string GetName(Vector3Int min, Vector3Int max) {
    return string.Format("Landing [{0} - {1}]", min, max);
  }

  public Vector3 centerPosition() {
    var minNodeIdx = TerrainGrid.TerrainColumnNodeIndex(terrainColIdx, Vector3Int.zero);
    var result = TerrainGrid.NodeIndexToUnitsVec3(minNodeIdx) + new Vector3(TerrainColumn.HALF_SIZE, 0, TerrainColumn.HALF_SIZE);
    result.y = TerrainGrid.NodeIndexToUnits((minIdx.y+maxIdx.y)/2);
    return result;
  }

  public void RegenerateMesh(TerrainGrid terrain, TerrainColumn terrainCol) {
    var unitsPerNode = TerrainGrid.UnitsPerNode();
    var isoInc = unitsPerNode*MarchingCubes.ISOVAL_CUTOFF;

    // Generate the landing mesh
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var mesh = new Mesh();

    if (minIdx.y == 0 && maxIdx.y == 0) {
      // Handle the special case where the ground is bedrock - just draw a plane at y==0
      var minTCIdx = TerrainGrid.TerrainColumnNodeIndex(terrainCol, Vector3Int.zero); // "global" index within the whole terrain

      bool isTCXMinEdge = terrainCol.index.x == 0;
      bool isTCXMaxEdge = terrainCol.index.x == terrain.xSize-1;
      bool isTCZMinEdge = terrainCol.index.z == 0;
      bool isTCZMaxEdge = terrainCol.index.z == terrain.zSize-1;

      var xMinInsetNodes = minIdx.x-minTCIdx.x;
      var zMinInsetNodes = minIdx.z-minTCIdx.z;
      var xMaxInsetNodes = (minTCIdx.x + TerrainGrid.NODES_PER_UNIT*TerrainColumn.SIZE-1) - maxIdx.x;
      var zMaxInsetNodes = (minTCIdx.z + TerrainGrid.NODES_PER_UNIT*TerrainColumn.SIZE-1) - maxIdx.z;

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
      var numNodesX = terrainCol.NumNodesX();
      var numNodesZ = terrainCol.NumNodesZ();
      var localIdx = new Vector3Int();
      for (int y = minIdx.y-1; y <= maxIdx.y+1; y++) {
        localIdx.y = y;
        for (int x = -1; x <= numNodesX; x++) {
          localIdx.x = x;
          for (int z = -1; z <= numNodesZ; z++) {
            localIdx.z = z;

            var terrainIdx = TerrainGrid.TerrainColumnNodeIndex(terrainCol, localIdx); // "global" index within the whole terrain
            for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
              // Get the node at the current index in the grid (also gets empty "ghost" nodes at the edges)
              var cornerNode = terrain.GetNode(terrainIdx + MarchingCubes.corners[i]);
              corners[i].setFromNode(cornerNode, -terrainCol.gameObject.transform.position);
            }
            MarchingCubes.polygonizeMeshOnly(corners, triangles, vertices);
          }
        }
      }

      mesh.SetVertices(vertices);
      mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
      
      Vector3 minPt, maxPt;
      terrainCol.GetMeshMinMax(terrain, out minPt, out maxPt, false);
      //minPt.x -= (isoInc + TerrainColumn.BOUNDS_EPSILON); maxPt.x += isoInc + TerrainColumn.BOUNDS_EPSILON;
      //minPt.z -= (isoInc + TerrainColumn.BOUNDS_EPSILON); maxPt.z += isoInc + TerrainColumn.BOUNDS_EPSILON;
      minPt.y = minIdx.y*TerrainGrid.UnitsPerNode() - TerrainColumn.BOUNDS_EPSILON;
      maxPt.y = maxIdx.y*TerrainGrid.UnitsPerNode() + isoInc + TerrainColumn.BOUNDS_EPSILON;
      
      mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance, minPt, maxPt);
      mesh.RecalculateBounds();
    }

    meshFilter.sharedMesh = mesh;

    gameObject.SetActive(false);
  }

}
