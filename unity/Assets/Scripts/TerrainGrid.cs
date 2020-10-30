using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public partial class TerrainGrid : MonoBehaviour, ISerializationCallbackReceiver {
  public static int nodesPerUnit = 5;

  [Range(1,32)]
  public int xSize = 10, ySize = 10, zSize = 10;
  [HideInInspector][SerializeField]
  private TerrainGridNode[] _serializedNodes; // Used to save nodes (NOTE: Unity doesn't save multidim arrays!)

  // Live data used in the editor and in-game
  private TerrainGridNode[,,] nodes; // Does not include the outer "ghost" nodes with zeroed isovalues
  private Dictionary<Vector3Int, TerrainColumn> terrainColumns;

  private int numNodesX() { return (int)(xSize*TerrainColumn.size*TerrainGrid.nodesPerUnit)+1-xSize; }
  private int numNodesY() { return (int)(ySize*TerrainColumn.size*TerrainGrid.nodesPerUnit)+1-ySize; }
  private int numNodesZ() { return (int)(zSize*TerrainColumn.size*TerrainGrid.nodesPerUnit)+1-zSize; }
  public int numNodes()  { return numNodesX()*numNodesY()*numNodesZ(); }

  public float unitsPerNode() { return (((float)TerrainColumn.size) / ((float)TerrainGrid.nodesPerUnit-1)); }
  public float halfUnitsPerNode() { return (0.5f * unitsPerNode()); }
  public Vector3 unitsPerNodeVec3() { var u = unitsPerNode(); return new Vector3(u,u,u); }
  public Vector3 halfUnitsPerNodeVec3() { var v = unitsPerNodeVec3(); return 0.5f*v; }

  public int unitsToNodeIndex(float unitVal) { return Mathf.FloorToInt(unitVal / unitsPerNode()); }
  public float nodeIndexToUnits(int idx) { return idx*unitsPerNode(); }
  public Vector3 nodeIndexToUnitsVec3(int x, int y, int z) { return nodeIndexToUnitsVec3(new Vector3Int(x,y,z)); }
  public Vector3 nodeIndexToUnitsVec3(in Vector3Int nodeIdx) {
    var nodeUnitsVec = unitsPerNodeVec3(); return Vector3.Scale(nodeIdx, nodeUnitsVec); 
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
  private IndexRange getIndexRangeForTerrainColumn(in TerrainColumn terrainCol) {
    var numNodesPerTCMinus1 = TerrainColumn.size*TerrainGrid.nodesPerUnit-1;
    var nodeXIdxStart = terrainCol.index.x * numNodesPerTCMinus1;
    var nodeZIdxStart = terrainCol.index.z * numNodesPerTCMinus1;
    return new IndexRange {
      xStartIdx = nodeXIdxStart,
      xEndIdx   = nodeXIdxStart + numNodesPerTCMinus1,
      yStartIdx = 0,
      yEndIdx   = numNodesY() - 1,
      zStartIdx = nodeZIdxStart,
      zEndIdx   = nodeZIdxStart + numNodesPerTCMinus1
    };
  }

  public void enqueueNodesInTerrainColumn(in TerrainColumn terrainCol, ref Queue<TerrainGridNode> tcNodes) {
    var tcIndices = getIndexRangeForTerrainColumn(terrainCol);
    for (var x = tcIndices.xStartIdx; x <= tcIndices.xEndIdx; x++) {
      for (var y = tcIndices.yStartIdx; y <= tcIndices.yEndIdx; y++) {
        for (var z = tcIndices.zStartIdx; z < tcIndices.zEndIdx; z++) {
          tcNodes.Enqueue(nodes[x,y,z]);
        }
      }
    }
  }

  public List<TerrainGridNode> getNeighboursForNode(in TerrainGridNode node) {
    var neighbours = new List<TerrainGridNode>();
    var idx = node.gridIndex;
    // There are 6 potential neighbours...
    if (idx.x > 0) { neighbours.Add(nodes[idx.x-1,idx.y,idx.z]); }
    if (idx.x < nodes.GetLength(0)-1) { neighbours.Add(nodes[idx.x+1,idx.y,idx.z]); }
    if (idx.y > 0) { neighbours.Add(nodes[idx.x,idx.y-1,idx.z]); }
    if (idx.y < nodes.GetLength(1)-1) { neighbours.Add(nodes[idx.x,idx.y+1,idx.z]); }
    if (idx.z > 0) { neighbours.Add(nodes[idx.x,idx.y,idx.z-1]); }
    if (idx.z < nodes.GetLength(2)-1) { neighbours.Add(nodes[idx.x,idx.y,idx.z+1]); }
    return neighbours;
  }

  public List<TerrainGridNode> getNodesInsideBox(in Bounds box) {
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

  public List<TerrainGridNode> getNodesInsideSphere(in Vector3 center, float radius) {
    var lsCenter = center - transform.position; // Get the center in localspace

    // Narrow the search down to the nodes inside the sphere's bounding box
    var dia = 2*radius;
    var nodesInBox = getNodesInsideBox(new Bounds(lsCenter, new Vector3(dia, dia, dia)));

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
    return new Vector3Int(
      terrainCol.index.x * (TerrainGrid.nodesPerUnit * TerrainColumn.size - 1), 0, 
      terrainCol.index.z * (TerrainGrid.nodesPerUnit * TerrainColumn.size - 1)
    ) + localIdx;
  }

  public TerrainGridNode getNode(in Vector3Int nodeIdx) {
    // If the index is outside of the node grid then we're dealing with a "ghost" node
    if (nodeIdx.x < 0 || nodeIdx.x > this.nodes.GetLength(0)-1 ||
        nodeIdx.y < 0 || nodeIdx.y > this.nodes.GetLength(1)-1 ||
        nodeIdx.z < 0 || nodeIdx.z > this.nodes.GetLength(2)-1) {
      var gridSpacePos = nodeIndexToUnitsVec3(nodeIdx);
      // NOTE: Since the node is outside the grid of TerrainColumns 
      // we don't associate any with this "ghost" node
      return new TerrainGridNode(gridSpacePos, nodeIdx, 0);
    }
    return nodes[nodeIdx.x,nodeIdx.y,nodeIdx.z];
  }

  public void OnBeforeSerialize() {
    if (nodes != null) {
      _serializedNodes = new TerrainGridNode[this.numNodes()];
      var numNodesX = this.numNodesX();
      var numNodesY = this.numNodesY();
      for (int x = 0; x < nodes.GetLength(0); x++) {
        for (int y = 0; y < nodes.GetLength(1); y++) {
          for (int z = 0; z < nodes.GetLength(2); z++) {
            _serializedNodes[z + (y*numNodesX) + (x*numNodesX*numNodesY)] = nodes[x,y,z];
          }
        }
      }
    }
  }
  public void OnAfterDeserialize() {
    if (_serializedNodes != null) {
      var numNodesX = this.numNodesX();
      var numNodesY = this.numNodesY();
      var numNodesZ = this.numNodesZ();
      nodes = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];
      for (int x = 0; x < numNodesX; x++) {
        for (int y = 0; y < numNodesY; y++) {
          for (int z = 0; z < numNodesZ; z++) {
            nodes[x,y,z] = _serializedNodes[z + (y*numNodesX) + (x*numNodesX*numNodesY)];
          }
        }
      }
    }
    _serializedNodes = null;
  }

  public void Awake() {
    /* 
    if (_instance == null) {
      if (Application.IsPlaying(gameObject)) { DontDestroyOnLoad(gameObject); }
       _instance = this;
      generateNodes();
      buildTerrainColumns();
      regenerateMeshes();
    }
    else if (_instance != this) {
      Debug.LogWarning("More than one instance of World found, removing duplicate.");
      GameObject.DestroyImmediate(this.gameObject);
      return;
    }
    */


    if (Application.IsPlaying(gameObject)) {
      buildTerrainColumns();
      terrainPhysicsCleanup();
    }
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
    var tcXIndices = new List<int>();
    var tcZIndices = new List<int>();
    var tcIndices = new List<Vector3Int>();
    var nodesPerTCMinus1 = TerrainColumn.size*TerrainGrid.nodesPerUnit - 1;
    for (int x = 0; x < numNodesX; x++) {
      var xPos = nodeIndexToUnits(x);

      // A node might be associated with more than one TerrainColumn, figure out
      // all the indices that can be tied to the node
      var currXTCIdx = Math.Max(0, Mathf.FloorToInt((x-1)/nodesPerTCMinus1));
      tcXIndices.Clear();
      tcXIndices.Add(currXTCIdx);
      if (x % nodesPerTCMinus1 == 0 && x > 0 && currXTCIdx+1 < xSize) {
        tcXIndices.Add(currXTCIdx+1);
      }

      for (int z = 0; z < numNodesZ; z++) {
        var zPos = nodeIndexToUnits(z);

        // We also gather potential TerrainColumn indices on the z-axis, this combined
        // with the ones on the x-axis (see above) will make up all the potential associated TC indices
        var currZTCIdx = Math.Max(0, Mathf.FloorToInt((z-1)/nodesPerTCMinus1));
        tcZIndices.Clear();
        tcZIndices.Add(currZTCIdx);
        if (z % nodesPerTCMinus1 == 0 && z > 0 && currZTCIdx+1 < zSize) {
          tcZIndices.Add(currZTCIdx+1);
        }

        // Based on all the TerrainColumn indices on the x and z axes we build the
        // associated indicies for the nodes
        tcIndices.Clear();
        foreach (var tcXIndex in tcXIndices) {
          foreach (var tcZIndex in tcZIndices) {
            tcIndices.Add(new Vector3Int(tcXIndex, 0, tcZIndex));
          }
        }

        for (int y = 0; y < numNodesY; y++) {
          var yPos = nodeIndexToUnits(y);
        
          if (nodes != null && x < nodes.GetLength(0) && y < nodes.GetLength(1) && z < nodes.GetLength(2)) {
            tempNodes[x,y,z] = nodes[x,y,z];
          }
          else {
            var unitPos = new Vector3(xPos, yPos, zPos);
            var gridIdx = new Vector3Int(x,y,z);
            var node = new TerrainGridNode(unitPos, gridIdx, 0.0f);
            // A grid node can have multiple associated TerrainColumns since they share nodes at the edges
            foreach (var tcIndex in tcIndices) {
              node.columnIndices.Add(tcIndex);
            } 
            tempNodes[x,y,z] = node;
          }
        }
      }
    }
    nodes = tempNodes;
  }
  private void buildTerrainColumns() {
    if (terrainColumns == null) { 
      terrainColumns = new Dictionary<Vector3Int, TerrainColumn>();
    }

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
          terrainCol.gameObj.transform.SetParent(transform);
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
      foreach (var tcIndex in node.columnIndices) { terrainCols.Add(terrainColumns[tcIndex]); }
    }
    foreach (var terrainCol in terrainCols) {
      terrainCol.regenerateMesh();
    }
  }

  public HashSet<TerrainColumn> terrainPhysicsCleanup() {
    return terrainPhysicsCleanup(new List<TerrainColumn>());
  }
  public HashSet<TerrainColumn> terrainPhysicsCleanup(in IEnumerable<TerrainColumn> affectedTCs) {
    TerrainNodeTraverser.updateGroundedNodes(this, affectedTCs);
    var islands = TerrainNodeTraverser.traverseNodeIslands(this);
    Debug.Log("Found " + islands.Count + " node islands.");

    var resultTCs = new HashSet<TerrainColumn>();
    foreach (var islandNodeSet in islands) {
      // TODO: Build debris for each island
      //Debug.Log("Debris island found, size: " + islandNodeSet.Count);
      //World.instance.addDebris(new TerrainDebris(islandNodeSet));

      foreach (var node in islandNodeSet) {
        foreach (var tcIndex in node.columnIndices) { resultTCs.Add(terrainColumns[tcIndex]); }
        // The nodes should no longer have terrain in them
        node.isoVal = 0;
      }
    }

    return resultTCs;
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
    var tempRay = new Ray(wsStartPt + 1e-4f*ray.direction, -ray.direction);
    float endDist = startDist;
    if (!gridBounds.IntersectRay(tempRay, out endDist)) {
      // This shouldn't happen, if it does then we just continue with the endDist equal to the startDist
      Debug.Log("No other boundary found? This shouldn't happen. Start Dist: " + startDist);
      endDist = startDist;
    }

    var wsEndPt = tempRay.GetPoint(endDist);
    var lsEndPt = wsEndPt - transform.position;

    var currVoxelIdx = new Vector3Int(
      (int)Mathf.Clamp(unitsToNodeIndex(lsStartPt.x), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(unitsToNodeIndex(lsStartPt.y), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(unitsToNodeIndex(lsStartPt.z), 0, nodes.GetLength(2)-1)
    );
    var lastVoxelIdx = new Vector3Int(
      (int)Mathf.Clamp(unitsToNodeIndex(lsEndPt.x), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(unitsToNodeIndex(lsEndPt.y), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(unitsToNodeIndex(lsEndPt.z), 0, nodes.GetLength(2)-1)
    );

    // Distance along the ray to the next voxel border from the current position (tMax).
    var nodeSize = unitsPerNode();
    var stepVec = new Vector3Int(ray.direction.x >= 0 ? 1 : -1,ray.direction.y >= 0 ? 1 : -1,ray.direction.z >= 0 ? 1 : -1);
    var nextVoxelBoundary = new Vector3(
      (currVoxelIdx.x+stepVec.x) * nodeSize,
      (currVoxelIdx.y+stepVec.y) * nodeSize,
      (currVoxelIdx.z+stepVec.z) * nodeSize
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
    if (currVoxelIdx.x > 0 && currVoxelIdx.x < this.nodes.GetLength(0) && ray.direction.x < 0) { diff.x--; negRay = true; }
    if (currVoxelIdx.y > 0 && currVoxelIdx.y < this.nodes.GetLength(1) && ray.direction.y < 0) { diff.y--; negRay = true; }
    if (currVoxelIdx.z > 0 && currVoxelIdx.z < this.nodes.GetLength(2) && ray.direction.z < 0) { diff.z--; negRay = true; }
    if (negRay) { currVoxelIdx += diff; }

    while (currVoxelIdx != lastVoxelIdx && this.nodes[currVoxelIdx.x,currVoxelIdx.y,currVoxelIdx.z].isoVal < 1) {
      //Debug.Log(currVoxel);
      var prevVoxel = currVoxelIdx;
      if (tMax.x < tMax.y) {
        if (tMax.x < tMax.z) {
          currVoxelIdx.x += stepVec.x;
          tMax.x += tDelta.x;
        }
        else {
          currVoxelIdx.z += stepVec.z;
          tMax.z += tDelta.z;
        }
      }
      else {
        if (tMax.y < tMax.z) {
          currVoxelIdx.y += stepVec.y;
          tMax.y += tDelta.y;
        }
        else {
          currVoxelIdx.z += stepVec.z;
          tMax.z += tDelta.z;
        }
      }
      if (currVoxelIdx.x < 0 || currVoxelIdx.x >= this.nodes.GetLength(0) ||
          currVoxelIdx.y < 0 || currVoxelIdx.y >= this.nodes.GetLength(1) ||
          currVoxelIdx.z < 0 || currVoxelIdx.z >= this.nodes.GetLength(2)) {

        currVoxelIdx = prevVoxel;
        break;
      }
    }

    var nodeCenter = new Vector3((nodeSize*currVoxelIdx.x),(nodeSize*currVoxelIdx.y),(nodeSize*currVoxelIdx.z));
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
