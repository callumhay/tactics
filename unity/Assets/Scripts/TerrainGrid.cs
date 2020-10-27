using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class TerrainGrid : MonoBehaviour {
  [Range(1,32)]
  public int xSize = 10, ySize = 10, zSize = 10;
  [Range(2,16)]
  public int nodesPerUnit = 4;

  //[SerializeField]
  private TerrainGridNode[,,] nodes; // Does not include the outer "ghost" nodes with zeroed isovalues

  private Dictionary<Vector3Int, TerrainColumn> terrainColumns = new Dictionary<Vector3Int, TerrainColumn>();

  private int numNodesX() { return (int)(xSize*TerrainColumn.size*nodesPerUnit); }
  private int numNodesY() { return (int)(ySize*TerrainColumn.size*nodesPerUnit); }
  private int numNodesZ() { return (int)(zSize*TerrainColumn.size*nodesPerUnit); }
  public int numNodes()  { return numNodesX()*numNodesY()*numNodesZ(); }

  public float unitsPerNode() { return (1.0f / nodesPerUnit); }
  public float halfUnitsPerNode() { return (0.5f * unitsPerNode()); }
  public Vector3 unitsPerNodeVec3() { var u = unitsPerNode(); return new Vector3(u,u,u); }
  public Vector3 halfUnitsPerNodeVec3() { var v = unitsPerNodeVec3(); return 0.5f*v; }

  public int unitsToNodeIndex(float unitVal) { return (int)Mathf.Floor(unitVal * nodesPerUnit); }
  public float nodeIndexToUnits(int idx) { return idx*unitsPerNode() + halfUnitsPerNode(); }
  public Vector3 nodeIndexToUnitsVec3(int x, int y, int z) { return nodeIndexToUnitsVec3(new Vector3Int(x,y,z)); }
  public Vector3 nodeIndexToUnitsVec3(in Vector3Int nodeIdx) {
    var nodeUnitsVec = unitsPerNodeVec3(); return Vector3.Scale(nodeIdx, nodeUnitsVec) + 0.5f*nodeUnitsVec; 
  }

  public int nodesPerTerrainColumnX() { return nodesPerUnit * TerrainColumn.size; }
  public int nodesPerTerrainColumnZ() { return nodesPerTerrainColumnX(); }
  public Vector3Int nodeIndexToTerrainColumnIndex(in Vector3Int nodeIdx) {
    return new Vector3Int(nodeIdx.x/nodesPerTerrainColumnX(), 0, nodeIdx.z/nodesPerTerrainColumnZ());
  }

  // Get the worldspace bounds of the grid
  public Bounds wsBounds() { 
    var b = new Bounds();
    b.SetMinMax(transform.position, transform.position + new Vector3(xSize, ySize, zSize));
    return b;
  }

  private sealed class IndexRange {
    public int xStartIdx;
    public int xEndIdx;
    public int yStartIdx;
    public int yEndIdx;
    public int zStartIdx;
    public int zEndIdx;
  }

  private IndexRange getIndexRangeForBox(in Bounds bounds) {
    return getIndexRangeForMinMax(bounds.min, bounds.max);
  }
  private IndexRange getIndexRangeForMinMax(in Vector3 min, in Vector3 max) {
    return new IndexRange {
      xStartIdx = Mathf.Clamp(unitsToNodeIndex(min.x), 0, nodes.GetLength(0)-1),
      xEndIdx   = Mathf.Clamp(unitsToNodeIndex(max.x), 0, nodes.GetLength(0)-1),
      yStartIdx = Mathf.Clamp(unitsToNodeIndex(min.y), 0, nodes.GetLength(1)-1),
      yEndIdx   = Mathf.Clamp(unitsToNodeIndex(max.y), 0, nodes.GetLength(1)-1),
      zStartIdx = Mathf.Clamp(unitsToNodeIndex(min.z), 0, nodes.GetLength(2)-1),
      zEndIdx   = Mathf.Clamp(unitsToNodeIndex(max.z), 0, nodes.GetLength(2)-1)
    };
  }

  public List<TerrainGridNode> GetNodesInsideBox(in Bounds box) {
    var result = new List<TerrainGridNode>();
    var indices = getIndexRangeForBox(box);
    for (int x = indices.xStartIdx; x <= indices.xEndIdx; x++) {
      for (int y = indices.yStartIdx; y <= indices.yEndIdx; y++) {
        for (int z = indices.zStartIdx; z <= indices.zEndIdx; z++) {
          result.Add(this.nodes[x,y,z]);
        }
      }
    }
    return result;
  }  

  public List<TerrainGridNode> GetNodesInsideSphere(in Vector3 center, float radius) {
    var lsCenter = center - transform.position; // Get the center in localspace

    // Narrow the search down to the nodes inside the sphere's bounding box
    var dia = 2*radius;
    var nodesInBox = GetNodesInsideBox(new Bounds(lsCenter, new Vector3(dia, dia, dia)));

    // Go through the list and only take the nodes inside the sphere
    var sqrRadius = radius*radius;
    var result = new List<TerrainGridNode>();
    foreach (var node in nodesInBox) {
      if ((node.position - lsCenter).sqrMagnitude <= sqrRadius) {
        result.Add(node);
      }
    }
    return result;
  }

  public Vector3Int terrainColumnNodeIndex(in TerrainColumn terrainCol, in Vector3Int localIdx) {
    return new Vector3Int(terrainCol.index.x * nodesPerTerrainColumnX(), 0, terrainCol.index.z * nodesPerTerrainColumnZ()) + localIdx;
  }

  public TerrainGridNode getNode(in Vector3Int nodeIdx) {
    // If the index is outside of the node grid then we're dealing with a "ghost" node
    if (nodeIdx.x < 0 || nodeIdx.x > this.nodes.GetLength(0)-1 ||
        nodeIdx.y < 0 || nodeIdx.y > this.nodes.GetLength(1)-1 ||
        nodeIdx.z < 0 || nodeIdx.z > this.nodes.GetLength(2)-1) {
      var gridSpacePos = nodeIndexToUnitsVec3(nodeIdx);
      return new TerrainGridNode(gridSpacePos, new Vector3Int(-1,-1,-1), 0);
    }
    return nodes[nodeIdx.x,nodeIdx.y,nodeIdx.z];
  }

  void Start() {
    if (Application.IsPlaying(gameObject)) {

    }
    #if UNITY_EDITOR
    else {      
      generateNodes();
      buildTerrainColumns();
      regenerateMeshes();
    }
    #endif
  }

  private void generateNodes() {

    var numNodesX = this.numNodesX();
    var numNodesY = this.numNodesY();
    var numNodesZ = this.numNodesZ();

    // Don't rebuild if we already have an array that's suitable!
    if (nodes != null && nodes.GetLength(0) == numNodesX && 
        nodes.GetLength(1) == numNodesY && nodes.GetLength(2) == numNodesZ) {
      return;
    }

    // We use a temporary array to preserve any existing nodes that are still in the grid
    var tempNodes = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];
    for (int x = 0; x < numNodesX; x++) {
      var xTCIdx = Mathf.FloorToInt(x/nodesPerTerrainColumnX());
      var xPos = nodeIndexToUnits(x);
      for (int y = 0; y < numNodesY; y++) {
        var yPos = nodeIndexToUnits(y);
        for (int z = 0; z < numNodesZ; z++) {
          if (nodes != null && x < nodes.GetLength(0) && y < nodes.GetLength(1) && z < nodes.GetLength(2)) {
            tempNodes[x,y,z] = nodes[x,y,z];
          }
          else {
            var zTCIdx = Mathf.FloorToInt(z/nodesPerTerrainColumnZ());
            var zPos = nodeIndexToUnits(z);
            var unitPos = new Vector3(xPos, yPos, zPos);
            var terrainColIdx = new Vector3Int(xTCIdx, 0, zTCIdx);
            tempNodes[x,y,z] = new TerrainGridNode(unitPos, terrainColIdx, 0.0f);
          }
        }
      }
    }
    nodes = tempNodes;
  }

  private void buildTerrainColumns() {
    var keysToRemove = new List<Vector3Int>();
    foreach (var entry in terrainColumns) {
      var idx = entry.Key;
      if (idx.x >= xSize || idx.y >= ySize || idx.z >= zSize) {
        keysToRemove.Add(idx);
      }
    }
    foreach (var key in keysToRemove) {
      terrainColumns[key].clear();
      terrainColumns.Remove(key);
    }

    for (int x = 0; x < xSize; x++) {
      for (int z = 0; z < zSize; z++) {
        var currIdx = new Vector3Int(x,0,z);
        if (!terrainColumns.ContainsKey(currIdx)) {
          var terrainCol = new TerrainColumn(currIdx, this);
          terrainColumns.Add(currIdx, terrainCol);
        }
      }
    }
  }

  private void regenerateMeshes() {
    foreach (var terrainCol in terrainColumns.Values) {
      terrainCol.regenerateMesh();
    } 
  }
  private void regenerateMeshesForNodes(in ICollection<TerrainGridNode> nodes) {
    var terrainCols = new HashSet<TerrainColumn>();
    
    foreach (var node in nodes) {
      //Debug.Log(node.terrainColumnIndex);
      terrainCols.Add(terrainColumns[node.terrainColumnIndex]);
    }
    foreach (var terrainCol in terrainCols) {
      terrainCol.regenerateMesh();
    }
  }

  // Editor-Only Stuff ----------------------------------------------
  #if UNITY_EDITOR

  private static float editorAlpha = 0.5f;

  void OnValidate() {
    Invoke("delayedOnValidate", 0);
  }
  void delayedOnValidate() {
    generateNodes();
    buildTerrainColumns();
    regenerateMeshes();
  }

  void OnDrawGizmos() {
    if (nodes == null || Application.IsPlaying(gameObject)) { return; }
    Gizmos.color = new Color(1,1,1,editorAlpha);
    Gizmos.DrawWireCube(transform.position + new Vector3(((float)xSize)/2,((float)ySize)/2,((float)zSize)/2), new Vector3(xSize,ySize,zSize));
    //Gizmos.DrawWireMesh(gizmoMesh, 0, transform.position);
  }

  public void addIsoValuesToNodes(float isoVal, in List<TerrainGridNode> nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    foreach (var node in nodes) {
      var prevIsoVal = node.isoVal;
      node.isoVal = Mathf.Clamp(node.isoVal + isoVal, 0, 1);
      if (node.isoVal != prevIsoVal) { changedNodes.Add(node); }
    }
    // Only regenerate the necessary TerrainColumns for the changed nodes
    regenerateMeshesForNodes(changedNodes);
  }

  // Intersect the given ray with this grid, returns the closest relevant edit point
  // when there's a collision (on return true). Returns false on no collision.
  public bool intersectEditorRay(in Ray ray, out Vector3 editPt) {
    editPt = new Vector3(0,0,0);

    if (nodes == null) { return false; }
    var gridBounds = wsBounds();
    float startDist = 0.0f;

    // NOTE: The IntersectRay method returns a positive number if we're outside the box and
    // negative if we're inside it
    if (!gridBounds.IntersectRay(ray, out startDist)) {
      return false;
    }
    // Check to see if the origin of the ray is inside the bounds (negative distance)
    // if so then we make the starting distance the origin of the ray
    startDist = Mathf.Max(0.0f, startDist);

    // March the ray through the grid:
    // Find the closest non-zero isovalue node in the grid to the given ray,
    // if all isovalues are zero then return the furthest node from the ray.
    // Based on: J. Amanatides, A. Woo. A Fast Voxel Traversal Algorithm for Ray Tracing. Eurographics '87

    var wsStartPt = ray.GetPoint(startDist); // Worldspace first intersection pt
    var lsStartPt = wsStartPt - transform.position; // to local/grid space (NOTE: rotations and scaling do not affect the grid)

    // Use a ray pointing in the opposite direction to find the end point
    var tempRay = new Ray(wsStartPt + (0.5f*halfUnitsPerNode())*ray.direction, -ray.direction);
    float endDist = startDist;
    if (!gridBounds.IntersectRay(tempRay, out endDist)) {
      // This shouldn't happen, if it does then we just continue with the endDist equal to the startDist
      Debug.Log("No other boundary found? This shouldn't happen. Start Dist: " + startDist);
    }

    var wsEndPt = tempRay.GetPoint(endDist);
    var lsEndPt = wsEndPt - transform.position;
    //Debug.Log("Start Pt: " + wsStartPt + "; End Pt: " + wsEndPt);

    var stepVec = new Vector3Int(
      ray.direction.x >= 0 ? 1 : -1,
      ray.direction.y >= 0 ? 1 : -1,
      ray.direction.z >= 0 ? 1 : -1
    );
 
    var nodeSize = unitsPerNode();
    var currVoxel = new Vector3Int(
      (int)Mathf.Clamp(Mathf.Floor(lsStartPt.x/nodeSize), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(Mathf.Floor(lsStartPt.y/nodeSize), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(Mathf.Floor(lsStartPt.z/nodeSize), 0, nodes.GetLength(2)-1)
    );
    var lastVoxel = new Vector3Int(
      (int)Mathf.Clamp(Mathf.Floor(lsEndPt.x/nodeSize), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(Mathf.Floor(lsEndPt.y/nodeSize), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(Mathf.Floor(lsEndPt.z/nodeSize), 0, nodes.GetLength(2)-1)
    );
    // Distance along the ray to the next voxel border from the current position (tMax).
    var nextVoxelBoundary = new Vector3(
      (currVoxel.x+stepVec.x) * nodeSize,
      (currVoxel.y+stepVec.y) * nodeSize,
      (currVoxel.z+stepVec.z) * nodeSize
    );
    
    var rayDirXIsZero = Mathf.Approximately(ray.direction.x, 0);
    var rayDirYIsZero = Mathf.Approximately(ray.direction.y, 0);
    var rayDirZIsZero = Mathf.Approximately(ray.direction.z, 0);

    // Distance until next intersection with voxel-border
    // the value of t at which the ray crosses the first vertical voxel boundary
    var tMax = new Vector3(
      rayDirXIsZero ? float.MaxValue : (nextVoxelBoundary.x - lsStartPt.x) / ray.direction.x,
      rayDirYIsZero ? float.MaxValue : (nextVoxelBoundary.y - lsStartPt.y) / ray.direction.y,
      rayDirZIsZero ? float.MaxValue : (nextVoxelBoundary.z - lsStartPt.z) / ray.direction.z
    );

    // How far along the ray we must move for the horizontal component to equal the width of a voxel
    // the direction in which we traverse the grid can only be float.MaxValue if we never go in that direction
    var tDelta = new Vector3(
      rayDirXIsZero ? float.MaxValue : nodeSize/ray.direction.x*stepVec.x,
      rayDirYIsZero ? float.MaxValue : nodeSize/ray.direction.y*stepVec.y,
      rayDirZIsZero ? float.MaxValue : nodeSize/ray.direction.z*stepVec.z
    );

    var diff = new Vector3Int(0,0,0);
    bool negRay = false;
    if (currVoxel.x > 0 && currVoxel.x < this.nodes.GetLength(0) && ray.direction.x < 0) { diff.x--; negRay = true; }
    if (currVoxel.y > 0 && currVoxel.y < this.nodes.GetLength(1) && ray.direction.y < 0) { diff.y--; negRay = true; }
    if (currVoxel.z > 0 && currVoxel.z < this.nodes.GetLength(2) && ray.direction.z < 0) { diff.z--; negRay = true; }
    if (negRay) { currVoxel += diff; }

    while (currVoxel != lastVoxel && this.nodes[currVoxel.x,currVoxel.y,currVoxel.z].isoVal < 1) {
      //Debug.Log(currVoxel);
      var prevVoxel = currVoxel;
      if (tMax.x < tMax.y) {
        if (tMax.x < tMax.z) {
          currVoxel.x += stepVec.x;
          tMax.x += tDelta.x;
        }
        else {
          currVoxel.z += stepVec.z;
          tMax.z += tDelta.z;
        }
      }
      else {
        if (tMax.y < tMax.z) {
          currVoxel.y += stepVec.y;
          tMax.y += tDelta.y;
        }
        else {
          currVoxel.z += stepVec.z;
          tMax.z += tDelta.z;
        }
      }
      if (currVoxel.x < 0 || currVoxel.x >= this.nodes.GetLength(0) ||
          currVoxel.y < 0 || currVoxel.y >= this.nodes.GetLength(1) ||
          currVoxel.z < 0 || currVoxel.z >= this.nodes.GetLength(2)) {

        currVoxel = prevVoxel;
        break;
      }
    }

    var nodeHalfSize = halfUnitsPerNode();
    var nodeCenter = new Vector3(
      (nodeSize*currVoxel.x) + nodeHalfSize,
      (nodeSize*currVoxel.y) + nodeHalfSize,
      (nodeSize*currVoxel.z) + nodeHalfSize
    );
    nodeCenter += transform.position; // Back to worldspace
    editPt = nodeCenter;
    /*
    // Find where the ray intersects with the given node
    var nodeBounds = new Bounds(nodeCenter, 
      new Vector3(nodeSize + 1e-6f, nodeSize + 1e-6f, nodeSize + 1e-6f)
    );
    float dist = 0;
    if (nodeBounds.IntersectRay(ray, out dist)) {
      editPt = ray.GetPoint(dist);
    }
    else {
      //Debug.Log("Couldn't intersect ray with final node. This shouldn't happen.");
      editPt = nodeCenter;
    }
    */

    return true;
  }

  #endif // UNITY_EDITOR
}
