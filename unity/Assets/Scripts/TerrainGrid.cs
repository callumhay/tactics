using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Events;

[ExecuteAlways]
public partial class TerrainGrid : MonoBehaviour, ISerializationCallbackReceiver {
  public static int nodesPerUnit = 5;

  public LevelData levelData;

  public int xSize { get { return levelData.xSize; } }
  public int ySize { get { return levelData.ySize; } }
  public int zSize { get { return levelData.zSize; } }

  // Live data used in the editor and in-game, note that we serialize these fields so that we can undo/redo edit operations
  private TerrainGridNode[,,] nodes; // Does not include the outer "ghost" nodes with zeroed isovalues
  private Dictionary<Vector3Int, TerrainColumn> terrainColumns;

  private Bedrock bedrock;

  // Liquid computation component
  private WaterCompute waterCompute;

  public int numNodesX() { return LevelData.sizeToNumNodes(xSize); }
  public int numNodesY() { return LevelData.sizeToNumNodes(ySize); }
  public int numNodesZ() { return LevelData.sizeToNumNodes(zSize); }
  public int numNodes()  { return numNodesX()*numNodesY()*numNodesZ(); }

  public static float unitsPerNode() { return (((float)TerrainColumn.size) / ((float)TerrainGrid.nodesPerUnit-1)); }
  public static float halfUnitsPerNode() { return (0.5f * unitsPerNode()); }
  public Vector3 unitsPerNodeVec3() { var u = unitsPerNode(); return new Vector3(u,u,u); }
  public Vector3 halfUnitsPerNodeVec3() { var v = unitsPerNodeVec3(); return 0.5f*v; }

  public static float unitVolumePerNode() { return Mathf.Pow(unitsPerNode(),3f); }

  public static int unitsToNodeIndex(float unitVal) { return Mathf.FloorToInt(unitVal / unitsPerNode()); }
  public Vector3Int unitsToNodeIndexVec3(float x, float y, float z) { 
    return new Vector3Int(
      Mathf.Clamp(unitsToNodeIndex(x), 0, nodes.GetLength(0)-1), 
      Mathf.Clamp(unitsToNodeIndex(y), 0, nodes.GetLength(1)-1), 
      Mathf.Clamp(unitsToNodeIndex(z), 0, nodes.GetLength(2)-1));
    }
  public Vector3Int unitsToNodeIndexVec3(in Vector3 unitPos) { return unitsToNodeIndexVec3(unitPos.x, unitPos.y, unitPos.z);}
  public static float nodeIndexToUnits(int idx) { return idx*unitsPerNode(); }
  public Vector3 nodeIndexToUnitsVec3(int x, int y, int z) { return nodeIndexToUnitsVec3(new Vector3Int(x,y,z)); }
  public Vector3 nodeIndexToUnitsVec3(in Vector3Int nodeIdx) {
    var nodeUnitsVec = unitsPerNodeVec3(); return Vector3.Scale(nodeIdx, nodeUnitsVec); 
  }

  public void getGridSnappedPoint(ref Vector3 wsPt) {
    // Find the closest column center to the given point and snap to it
    var lsPt = wsPt - transform.position; // local space

    var unitsPerTC = (unitsPerNode()*TerrainColumn.size*(nodesPerUnit-1));
    var oneOverUnitsPerTC = 1.0f / unitsPerTC;
    var halfUnitsPerTC = unitsPerTC / 2.0f;

    // Calculate the rounded terrain column index for snapping
    var tcIdxPt = Vector3.Scale(lsPt, new Vector3(oneOverUnitsPerTC,oneOverUnitsPerTC,oneOverUnitsPerTC));
    tcIdxPt.x = Math.Min(xSize-1, Math.Max(0, Mathf.RoundToInt(tcIdxPt.x)));
    tcIdxPt.y = Math.Min(ySize-1, Math.Max(0, Mathf.RoundToInt(tcIdxPt.y)));
    tcIdxPt.z = Math.Min(zSize-1, Math.Max(0, Mathf.RoundToInt(tcIdxPt.z)));

    // Snap the point to the middle of the closest terrain column
    // and move it back into world space
    wsPt = (unitsPerTC * tcIdxPt) + new Vector3(halfUnitsPerTC,halfUnitsPerTC,halfUnitsPerTC) + transform.position;
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
    return getIndexRangeForTerrainColumn(terrainCol.index);
  }
  private IndexRange getIndexRangeForTerrainColumn(in Vector3Int terrainColIdx) {
    var numNodesPerTCMinus1 = TerrainColumn.size*TerrainGrid.nodesPerUnit-1;
    var nodeXIdxStart = terrainColIdx.x * numNodesPerTCMinus1;
    var nodeZIdxStart = terrainColIdx.z * numNodesPerTCMinus1;
    return new IndexRange {
      xStartIdx = nodeXIdxStart,
      xEndIdx   = nodeXIdxStart + numNodesPerTCMinus1,
      yStartIdx = 0,
      yEndIdx   = numNodesY() - 1,
      zStartIdx = nodeZIdxStart,
      zEndIdx   = nodeZIdxStart + numNodesPerTCMinus1
    };
  }

  [Flags] public enum TerrainNodeSelectors { All=0xFF00, Surface=0x000F, AboveSurface=0x00F0, None=0x0000 };
  public HashSet<TerrainGridNode> getNodesInTerrainColumn(
    in Vector3Int terrainColIdx, int maxYNodeIdx, in TerrainNodeSelectors nodeSelector=TerrainNodeSelectors.All 
  ) {

    var tcNodes = new HashSet<TerrainGridNode>();
    var tcIndices = getIndexRangeForTerrainColumn(terrainColIdx);
    tcIndices.yEndIdx = Math.Min(tcIndices.yEndIdx, maxYNodeIdx);

    var allNodes = nodeSelector.HasFlag(TerrainNodeSelectors.All);
    if (allNodes) {
      for (var x = tcIndices.xStartIdx; x <= tcIndices.xEndIdx; x++) {    
        for (var y = tcIndices.yStartIdx; y <= tcIndices.yEndIdx; y++) {
          for (var z = tcIndices.zStartIdx; z <= tcIndices.zEndIdx; z++) {
            tcNodes.Add(nodes[x,y,z]);
          }
        }
      }
    }
    else {
      var surfaceNodes = nodeSelector.HasFlag(TerrainNodeSelectors.Surface);
      var aboveSurfaceNodes = nodeSelector.HasFlag(TerrainNodeSelectors.AboveSurface);
      {
        for (var x = tcIndices.xStartIdx; x <= tcIndices.xEndIdx; x++) {    
          for (var y = tcIndices.yStartIdx; y <= tcIndices.yEndIdx; y++) {
            for (var z = tcIndices.zStartIdx; z <= tcIndices.zEndIdx; z++) {
              var currNode = nodes[x,y,z];
              if (currNode.isTerrain()) {
                var neighbours = getNeighboursForNode(currNode);
                if (surfaceNodes) {
                  // If any of the neighbours are empty then this is a surface node
                  foreach (var neighbour in neighbours) {
                    if (!neighbour.isTerrain()) { tcNodes.Add(currNode); break; }
                  }
                }
                if (aboveSurfaceNodes) {
                  // Grab all the neighbour nodes that are empty
                  foreach (var neighbour in neighbours) {
                    if (neighbour.isEmpty()) { tcNodes.Add(neighbour); }
                  }
                }
              }
            }
          }
        }
    
        // If there are no nodes then return the nodes at the base of the column
        if (tcNodes.Count == 0) {
          for (var x = tcIndices.xStartIdx; x <= tcIndices.xEndIdx; x++) {
            for (var z = tcIndices.zStartIdx; z <= tcIndices.zEndIdx; z++) {
              tcNodes.Add(nodes[x,tcIndices.yStartIdx,z]);
            }
          }
        }
      }
    }

    return tcNodes;
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

  public List<TerrainGridNode> getNodesInsideBox(in Bounds box, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var result = new List<TerrainGridNode>();
    var indices = getIndexRangeForBox(box);
    var maxYIdx = unitsToNodeIndex(setLevelUnits);
    indices.yStartIdx = Math.Min(maxYIdx, indices.yStartIdx);
    indices.yEndIdx = Math.Min(maxYIdx, indices.yEndIdx);

    if (groundFirst || liquidFirst) {
      int startY = indices.yStartIdx;
      for (int y = 0; y < indices.yStartIdx && startY == indices.yStartIdx; y++) {
        for (int x = indices.xStartIdx; x <= indices.xEndIdx && startY == indices.yStartIdx; x++) {
          for (int z = indices.zStartIdx; z <= indices.zEndIdx; z++) {
            var node = this.nodes[x,y,z];
            if ((groundFirst && !node.isTerrain()) || (liquidFirst && !node.isLiquid())) { startY = y; break; }
          }
        }
      }
      var yDiff = indices.yStartIdx - startY;
      indices.yStartIdx = startY;
      indices.yEndIdx -= yDiff;
    }

    for (int x = indices.xStartIdx; x <= indices.xEndIdx; x++) {
      for (int y = indices.yStartIdx; y <= indices.yEndIdx; y++) {
        for (int z = indices.zStartIdx; z <= indices.zEndIdx; z++) {
          result.Add(this.nodes[x,y,z]);
        }
      }
    }

    return result;
  }  

  public List<TerrainGridNode> getNodesInsideSphere(in Vector3 center, float radius, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = center - transform.position; // Get the center in localspace

    // Narrow the search down to the nodes inside the sphere's bounding box
    var dia = 2*radius;
    var nodesInBox = getNodesInsideBox(new Bounds(lsCenter, new Vector3(dia, dia, dia)), groundFirst, liquidFirst, setLevelUnits);

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

  public List<TerrainGridNode> getNodesInsideProjXZCircle(in Vector3 center, float radius, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = center - transform.position; // Get the center in localspace
    
    // Find the highest y node in the xz coordinates with an isovalue
    var isCenter = unitsToNodeIndexVec3(lsCenter); isCenter.y++;
    if (groundFirst || liquidFirst) {
      int startY = isCenter.y;
      var startXIdx = Mathf.FloorToInt(isCenter.x-radius);
      var endXIdx = Mathf.FloorToInt(isCenter.x+radius);
      var startZIdx = Mathf.FloorToInt(isCenter.z-radius);
      var endZIdx = Mathf.FloorToInt(isCenter.z+radius);
      for (int y = 0; y < isCenter.y && startY == isCenter.y; y++) {
        for (int x = startXIdx; x <= endXIdx && startY == isCenter.y; x++) {
          for (int z = startZIdx; z <= endZIdx  && startY == isCenter.y; z++) {
            var node = this.nodes[x,y,z];
            if ((groundFirst && !node.isTerrain()) || (liquidFirst && !node.isLiquid())) { startY = y; break; }
          }
        }
      }
      isCenter.y = startY;
    }
    else {
      while (isCenter.y > 1 && nodes[isCenter.x,isCenter.y-1,isCenter.z].isEmpty()) { isCenter.y--; }
      while (isCenter.y < nodes.GetLength(1)-1 && !nodes[isCenter.x,isCenter.y,isCenter.z].isEmpty()) { isCenter.y++; }
    }
    isCenter.y = Mathf.Min(Mathf.Max(0, unitsToNodeIndex(setLevelUnits-radius)), isCenter.y);

    // Get the nodes in a square box that encloses the circle
    var dia = 2*radius;
    var box = new Bounds(nodeIndexToUnitsVec3(isCenter), new Vector3(dia, 1.5f*halfUnitsPerNode(), dia));
    var indices = getIndexRangeForBox(box);
    var result = new List<TerrainGridNode>();
    var sqrRadius = radius*radius;
    for (int x = indices.xStartIdx; x <= indices.xEndIdx; x++) {
      for (int y = indices.yStartIdx; y <= indices.yEndIdx; y++) {
        for (int z = indices.zStartIdx; z <= indices.zEndIdx; z++) {
          var node = this.nodes[x,y,z];
          if ((node.position - lsCenter).sqrMagnitude <= sqrRadius) { result.Add(node); }
        }
      }
    }
    return result;
  }
  public List<TerrainGridNode> getNodesInsideProjXZSquare(in Bounds box, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = box.center - transform.position; // Get the center in localspace
    // Find the highest y node in the xz coordinates with an isovalue
    var isCenter = unitsToNodeIndexVec3(lsCenter); isCenter.y++;
    while (isCenter.y > 1 && nodes[isCenter.x,isCenter.y-1,isCenter.z].isEmpty()) { isCenter.y--; }
    while (isCenter.y < nodes.GetLength(1)-1 && !nodes[isCenter.x,isCenter.y,isCenter.z].isEmpty()) { isCenter.y++; }

    var projBox = new Bounds(nodeIndexToUnitsVec3(isCenter), new Vector3(box.size.x, 1.5f*halfUnitsPerNode(), box.size.z));
    return getNodesInsideBox(projBox, groundFirst, liquidFirst, setLevelUnits);
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

  private void loadLevelDataNodes() {
    // Uncommenting this can wipe the entire levelData object... not great.
    //if (levelData == null) {
    //  levelData = ScriptableObjectUtility.LoadOrCreateAssetFromPath<LevelData>(LevelData.emptyLevelAssetPath);
    //}

    if (levelData != null) {
      // When we load the nodes we are only loading their index and isovalue, the rest of
      // the information inside each node needs to be rebuilt (this is done in generateNodes).
      nodes = levelData.getNodesAs3DArray();
      generateNodes();
    }
  }
  public void OnBeforeSerialize() {
    //if (levelData != null) {
    //  levelData.setNodesFrom3DArray(nodes);
    //}
  }
  public void OnAfterDeserialize() {
    loadLevelDataNodes();
  }

  public void Awake() {
    if (Application.IsPlaying(gameObject)) {
      gameObject.SetActive(false);
      var onSleepEventListener = gameObject.AddComponent<GameEventListener>();
      onSleepEventListener.unityEvent = new UnityEvent<GameObject>();
      onSleepEventListener.unityEvent.AddListener(mergeDebrisIntoTerrain);
      onSleepEventListener.gameEvent = Resources.Load<GameEvent>(GameEvent.DEBRIS_SLEEP_EVENT);
      gameObject.SetActive(true);

      // Important! We must instantiate the level data in game mode or else
      // we'll overwrite the asset while the game plays
      levelData = Instantiate<LevelData>(levelData);
      loadLevelDataNodes();
    }
  }

  void Start() {

    if (Application.IsPlaying(gameObject)) {
      // Setup liquid computation/simulation stuff
      waterCompute = GetComponent<WaterCompute>();
      if (waterCompute == null) { Debug.LogError("Could not find WaterCompute in GameObject."); }
      else { waterCompute.initAll(); }

      // Build all the assets for the game that aren't stored in the level data
      buildBedrock();
      buildTerrainColumns();
      terrainPhysicsCleanup();
    }
    #if UNITY_EDITOR
    else {
      if (levelData != null) {
        loadLevelDataNodes();
        buildBedrock();
        buildTerrainColumns();
        // No terrainPhysicsCleanup in editor!
      }
    }
    #endif
  }

  /// <summary>
  /// Generates and fills data in for the non-serialized node data based on the serialized data (array of isovalues) alone.
  /// This method is meant to be as robust as possible so it can properly initialize the runtime node array in almost any state.
  /// </summary>
  private void generateNodes() {
    var numNodesX = this.numNodesX();
    var numNodesY = this.numNodesY();
    var numNodesZ = this.numNodesZ();

    // We use a temporary array to preserve any existing nodes that are still in the grid
    TerrainGridNode[,,] tempNodes = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];

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
          var unitPos = new Vector3(xPos, yPos, zPos);
          var gridIdx = new Vector3Int(x,y,z);

          TerrainGridNode currNode = null;
          if (nodes != null && x < nodes.GetLength(0) && y < nodes.GetLength(1) && z < nodes.GetLength(2)) {
            currNode = nodes[x,y,z];
            if (currNode == null) { currNode = new TerrainGridNode(unitPos, gridIdx); }
            else {
              currNode.position = unitPos;
              currNode.gridIndex = gridIdx;
            }
          }
          else {
            currNode = new TerrainGridNode(unitPos, gridIdx);
          }

          // A grid node can have multiple associated TerrainColumns since they share nodes at the edges
          if (currNode.columnIndices == null) { currNode.columnIndices = new List<Vector3Int>(); }
          else { currNode.columnIndices.Clear(); }
          
          foreach (var tcIndex in tcIndices) {
            currNode.columnIndices.Add(tcIndex);
          } 

          tempNodes[x,y,z] = currNode;
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

  private void buildBedrock() {
    bedrock = new Bedrock(this); 
    bedrock.gameObj.transform.SetParent(transform); 
    bedrock.gameObj.transform.SetAsFirstSibling();
  }

  private void regenerateMeshes(/*bool updateComputeBuffer = false*/) {
    foreach (var terrainCol in terrainColumns.Values) {
      terrainCol.regenerateMesh();
    }
    bedrock.regenerateMesh();
    // TODO: Update the compute buffer HERE for every node.
  }

  private void regenerateMeshes(in ICollection<TerrainColumn> terrainCols) {
    foreach (var terrainCol in terrainCols) {
      terrainCol.regenerateMesh();
    }
    waterCompute?.updateNodes(nodes); // Update the compute buffer HERE for every node in the given set of TerrainColumns.
  }
  private HashSet<TerrainColumn> regenerateMeshes(in IEnumerable<TerrainGridNode> nodes) {
    var terrainCols = findAllAffectedTCs(nodes);
    regenerateMeshes(terrainCols);
     // TODO: Update the compute buffer HERE for every node, make sure not to do this in the other regenerateMeshes method
    return terrainCols;
  }

  /// <summary>
  /// Obtain all of the unique TerrainColumns that are affected and will need to be regenerated 
  /// for the changes to the given nodes.
  /// </summary>
  /// <param name="changedNodes">Nodes that have changed.</param>
  /// <returns>Unique set of TerrainColumns that are affected by the changed nodes.</returns>
  private HashSet<TerrainColumn> findAllAffectedTCs(in IEnumerable<TerrainGridNode> changedNodes) {
    var terrainCols = new HashSet<TerrainColumn>();
    foreach (var node in changedNodes) {
      Debug.Assert(node != null);
      foreach (var tcIndex in node.columnIndices) { terrainCols.Add(terrainColumns[tcIndex]); }
      // We also need to add TerrainColumns associated with any adjacent nodes to avoid seams and other artifacts
      // caused by the node/cube dependancies that exist at the edges of each TerrainColumn mesh
      var neighbourNodes = getNeighboursForNode(node);
      foreach (var neighbourNode in neighbourNodes) {
        foreach (var tcIndex in neighbourNode.columnIndices) { terrainCols.Add(terrainColumns[tcIndex]); }
      }
    }
    return terrainCols;
  }

  private TerrainDebris buildTerrainDebris(in HashSet<TerrainGridNode> nodes) {

    // Find the index bounding box that includes all the given nodes
    var bbMin = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
    var bbMax = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    foreach (var node in nodes) {
      bbMin = Vector3Int.Min(bbMin, node.gridIndex);
      bbMax = Vector3Int.Max(bbMax, node.gridIndex);
    }
    var boxBounds = new BoundsInt();
    boxBounds.SetMinMax(bbMin, bbMax);

    // Calculate the worldspace position of the debris based on the min/max indices of the nodes
    var nodeUnits = unitsPerNode();
    var debrisCenterPos = nodeUnits * boxBounds.center;

    // Make the box wider on each side this allows us to fill the outer nodes
    // with empty isovalues so that when we use marching cubes we create a convex shape
    // (i.e., a chunk that "broke off" from the terrain).
    boxBounds.min -= 2*Vector3Int.one;
    boxBounds.max += 2*Vector3Int.one;

    // Fill the box up with empty corners
    var boxSize = boxBounds.size;
    var lsCenterPos = 0.5f * nodeUnits * (Vector3)(boxBounds.size);
    var nodeCorners = new CubeCorner[boxSize.x,boxSize.y,boxSize.z];
    for (int x = 0; x < boxSize.x; x++) {
      for (int y = 0; y < boxSize.y; y++) {
        for (int z = 0; z < boxSize.z; z++) {
          var nodeCorner = new CubeCorner();
          nodeCorner.isoVal = 0;
          // Store a localspace position
          nodeCorner.position = new Vector3(x*nodeUnits, y*nodeUnits, z*nodeUnits) - lsCenterPos;
          nodeCorners[x,y,z] = nodeCorner;
        }
      }
    }
    // Map each of the nodes isovalues into the corners array
    foreach (var node in nodes) {
      var idx = node.gridIndex;
      var corner = nodeCorners[idx.x-boxBounds.min.x, idx.y-boxBounds.min.y, idx.z-boxBounds.min.z];
      corner.isoVal = node.isoVal;
      corner.materials = new List<NodeMaterialContrib>(node.materials);
    }

    // TODO: Feed in the material for the debris here as well
    var result = new TerrainDebris(debrisCenterPos);
    result.gameObj.transform.SetParent(transform);
    result.build(nodeCorners);
    return result;
  }

  public void mergeDebrisIntoTerrain(GameObject debrisGO) {
    //Debug.Log("mergeDebrisIntoTerrain - merging debris GameObject mesh into nodes (in TerrainGrid)");

    // Get the bounding box of the debris in worldspace - we use this as a rough estimate
    // of which nodes we need to iterate through to convert the mesh back into nodes
    var debrisRenderer = debrisGO.GetComponent<Renderer>();
    var debrisNodeMapper = debrisGO.GetComponent<DebrisNodeMapper>();
    var debrisWSBounds = debrisRenderer.bounds;

    // Find the bounds in node index space
    var nodeIdxRange = getIndexRangeForMinMax(debrisWSBounds.min - transform.position, debrisWSBounds.max - transform.position);

    // Go through each potential node and cast a ray from the center of that node (in any direction)
    // If the ray intersects with the inside of the debris' mesh then add back the isovalue for that node
    var closenessEpsilon = 0.5f*halfUnitsPerNode();
    var debrisCollider = debrisGO.GetComponent<Collider>();
    var affectedNodes = new List<TerrainGridNode>();
    for (int x = nodeIdxRange.xStartIdx; x <= nodeIdxRange.xEndIdx; x++) {
      for (int y = nodeIdxRange.yStartIdx; y <= nodeIdxRange.yEndIdx; y++) {
        for (int z = nodeIdxRange.zStartIdx; z <= nodeIdxRange.zEndIdx; z++) {
          var node = nodes[x,y,z];
          var nodeWSPos = node.position + transform.position;
          if (debrisCollider.IsPointInside(nodeWSPos, closenessEpsilon)) {
            // Map the world space position into the local space node index of the debris (via DebrisNodeMapper)
            // use this to determine what the original material was
            var origCorner = debrisNodeMapper.mapFromWorldspace(nodeWSPos);
            node.isoVal = 1;
            node.materials = origCorner.materials;
            affectedNodes.Add(node);
          }
        }
      }
    }
    // Regenerate the new meshes which will incorporate the debris into the terrain and destroy the debris
    regenerateMeshes(affectedNodes);
    Destroy(debrisGO);
  }

  public HashSet<TerrainColumn> terrainPhysicsCleanup() {
    return terrainPhysicsCleanup(new List<TerrainColumn>());
  }
  public HashSet<TerrainColumn> terrainPhysicsCleanup(in IEnumerable<TerrainColumn> affectedTCs) {

    TerrainNodeTraverser.updateGroundedNodes(this, affectedTCs);
    var islands = TerrainNodeTraverser.traverseNodeIslands(this);
    var resultTCs = new HashSet<TerrainColumn>(affectedTCs);
    foreach (var islandNodeSet in islands) {
      // Build debris for each island
      var debris = buildTerrainDebris(islandNodeSet);
      // And clear the isovalues for all the nodes that make up the debris (which is now a separate mesh)
      foreach (var node in islandNodeSet) {
        foreach (var tcIndex in node.columnIndices) { resultTCs.Add(terrainColumns[tcIndex]); }
        node.isoVal = 0;
      }
    }
    regenerateMeshes(resultTCs);

    return resultTCs;
  }

  private void updateNodesInGameOrEditor(in IEnumerable<TerrainGridNode> changedNodes) {
    #if UNITY_EDITOR
    if (!Application.IsPlaying(gameObject)) {
      updateNodesInEditor(changedNodes);
      updateWaterGizmoMesh();
    }
    else 
    #endif
    {
      var affectedTCs = findAllAffectedTCs(changedNodes);
      terrainPhysicsCleanup(affectedTCs);
    }
  }

  public void addLiquidToNodes(float liquidVolPercentage, in List<TerrainGridNode> nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    var maxNodeVol = unitVolumePerNode();
    var liquidVol  = maxNodeVol*liquidVolPercentage;
    foreach (var node in nodes) {
      var prevVol = node.liquidVol;
      var prevIsoVal = node.isoVal;

      if (node.isTerrain()) { node.liquidVol = 0f; } // If the node is land then we can't add liquid to it
      else { node.liquidVol = Mathf.Clamp(node.liquidVol + liquidVol, 0, maxNodeVol); node.isoVal = 0f; }

      if (node.liquidVol != prevVol || node.isoVal != prevIsoVal) { changedNodes.Add(node); }
    }
    updateNodesInGameOrEditor(changedNodes);
  }

  public void addIsoValuesToNodes(float isoVal, in List<TerrainGridNode> nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    foreach (var node in nodes) {
      var prevIsoVal = node.isoVal;

      if (node.isLiquid()) { node.isoVal = 0f; } // If the node is liquid then we can't add land to it
      else { node.isoVal = Mathf.Clamp(node.isoVal + isoVal, 0, 1); }

      if (node.isoVal != prevIsoVal) { changedNodes.Add(node); }
    }
    updateNodesInGameOrEditor(changedNodes);
  }

  public void addIsoValuesAndMaterialToNodes(float isoVal, float matAmt, Material mat, in List<TerrainGridNode> nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    var matToPaint = mat ? mat : MaterialHelper.defaultMaterial;
    foreach (var node in nodes) {
      var prevIsoVal = node.isoVal;

      if (node.isLiquid()) { node.isoVal = 0f; } // If the node is liquid then we can't add land to it
      else { node.isoVal = Mathf.Clamp(node.isoVal + isoVal, 0, 1); }
      
      if (node.isoVal != prevIsoVal) { 
        // If the node just came into existance (was just painted from 0 to 1) then only paint the given material
        if (node.isoVal >= 0 && prevIsoVal <= 0) {
          node.materials.Clear();
          node.materials.Add(new NodeMaterialContrib(matToPaint, 1.0f));
        }
        changedNodes.Add(node);
      }
    }
    // Go through all the changed nodes and adjust materials accordingly
    var clampedMatAmt = Mathf.Clamp(matAmt, 0f, 1f);    
    foreach (var node in changedNodes) {
      // If the node has been fully "erased", then remove all its materials
      if (node.isoVal <= 0) { node.materials.Clear(); }
      else {
        bool foundMat = false;
        foreach (var matContrib in node.materials) {
          if (matContrib.material == matToPaint) {
            matContrib.contribution = Mathf.Clamp(matContrib.contribution + matAmt, 0.0f, 1.0f);
            foundMat = true;
            if (matContrib.contribution == 0f) { node.materials.Remove(matContrib); }
            break;
          }
        }
        if (!foundMat) {
          if (node.materials.Count >= TerrainGridNode.MAX_MATERIALS_PER_NODE) {
            Debug.LogWarning("A node already has the maximum number of materials, erase those materials first.");
          }
          else {
            node.materials.Add(new NodeMaterialContrib(matToPaint, clampedMatAmt));
          }
        }
      }
    }
    updateNodesInGameOrEditor(changedNodes);
  }

  // Editor-Only Stuff ----------------------------------------------
  #if UNITY_EDITOR

  private static float editorAlpha = 0.5f;
  private Mesh waterGizmoMesh;

  private void updateWaterGizmoMesh() {
    
    var vertices = new List<Vector3>();
    //var colours = new List<Color>();
    var indices = new List<int>();
    
    var nodeSize = unitsPerNode();
    var maxNodeVol = unitVolumePerNode();
    var nodeSizeVec = new Vector3(nodeSize,nodeSize,nodeSize);
    var nodeWorldTranslation = (-nodeSizeVec / 2) + transform.position;
    for (int x = 0; x < nodes.GetLength(0); x++) {
      for (int y = 0; y < nodes.GetLength(1); y++) {
        for (int z = 0; z < nodes.GetLength(2); z++) {
          var node = nodes[x,y,z];
          if (node.isLiquid()) { 
            Vector3[] verts; 
            int[] tris;
            int startVertNum = vertices.Count;
            MeshHelper.BuildCubeData(nodeSizeVec, out tris, out verts);
            for (int i = 0; i < verts.Length; i++) {
              vertices.Add(verts[i] + node.position + nodeWorldTranslation);
            }
            for (int i = 0; i < tris.Length; i++) {
              indices.Add(startVertNum + tris[i]);
            }
            //var volPct = node.liquidVol / maxNodeVol;
            //colours.Add(new Color(0, 0.25f*volPct, volPct, 1f));
          }
        }
      }
    }
    if (vertices.Count > 0) {
      if (waterGizmoMesh == null) { waterGizmoMesh = new Mesh(); }
      else { waterGizmoMesh.Clear(); }
      waterGizmoMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      waterGizmoMesh.SetVertices(vertices);
      //waterGizmoMesh.SetColors(colours);
      waterGizmoMesh.SetIndices(indices, MeshTopology.Triangles, 0);
      waterGizmoMesh.RecalculateNormals();
    }
    else {
      waterGizmoMesh = null;
    }
  }

  public void OnValidate() {
    Invoke("delayedOnValidate", 0);
  }
  void delayedOnValidate() {
    if (levelData != null) {
      loadLevelDataNodes();
      buildBedrock();
      buildTerrainColumns();
      regenerateMeshes();
      updateWaterGizmoMesh();
    }
  }

  void OnDrawGizmos() {
    if (Application.IsPlaying(gameObject)) { return; }
    Gizmos.color = new Color(1,1,1,editorAlpha);
    Gizmos.DrawWireCube(transform.position + 
      new Vector3(((float)xSize)/2,((float)ySize)/2,((float)zSize)/2), new Vector3(xSize,ySize,zSize)
    );
    if (waterGizmoMesh) {
      Gizmos.color = new Color(0,0.25f,1f,0.75f);
      Gizmos.DrawMesh(waterGizmoMesh);
    }
  }

  public float fastSampleHeight(int terrainColX, int terrainColZ) {
    if (nodes == null) { return 0f; }
    int nodeIdxX = terrainColX*TerrainColumn.size*(nodesPerUnit-1) + (nodesPerUnit/2)*TerrainColumn.size;
    int nodeIdxZ = terrainColZ*TerrainColumn.size*(nodesPerUnit-1) + (nodesPerUnit/2)*TerrainColumn.size;
    //Debug.Log("X: " + nodeIdxX + ", Z: " + nodeIdxZ + " Grid size: " + nodes.GetLength(0) + ", " + nodes.GetLength(1) + ", " + nodes.GetLength(2));
    int numYNodes = numNodesY();
    int finalYNodeIdx = 0;
    for (int y = 0; y < numYNodes-1; y++) {
      if (!nodes[nodeIdxX, y+1, nodeIdxZ].isTerrain()) { break; }
      finalYNodeIdx++;
    }
    return finalYNodeIdx*unitsPerNode();
  }

  public void updateNodesInEditor(in IEnumerable<TerrainGridNode> updateNodes) {
    if (Application.IsPlaying(gameObject)) { return; }
    var affectedTCs = regenerateMeshes(updateNodes);
    levelData.updateFromNodes(nodes, updateNodes);
    EditorUtility.SetDirty(levelData);
    EditorSceneManager.MarkSceneDirty(gameObject.scene);
  }

  public void fillCoreMaterial() {
    // This is similar to a rasterization algorithm - we trace along each line of nodes, when we encounter a node
    // that is inside the terrain we begin filling nodes with the core material until we find a node that is no
    // longer inside the terrain.
    int numXNodes = numNodesX(); int numYNodes = numNodesY(); int numZNodes = numNodesZ();
    bool inTerrainOnZ = false;
    for (int x = 0; x < numXNodes; x++) {
      for (int y = 0; y < numYNodes; y++) {
        inTerrainOnZ = false;
        for (int z = 0; z < numZNodes; z++) {
          var currNode = nodes[x,y,z];
          if (currNode.isTerrain()) {
            if (!inTerrainOnZ) { inTerrainOnZ = true; }
            else {
              // We're already inside the terrain along the z-axis, check to see if any of the nodes surrounding this one are not inside the terrain
              if (x == 0 || x == numXNodes-1 || !nodes[x+1,y,z].isTerrain() || !nodes[x-1,y,z].isTerrain() ||
                  y == numYNodes-1 || !nodes[x,y+1,z].isTerrain() ||
                  z == numZNodes-1 || !nodes[x,y,z+1].isTerrain()) { 
                continue; 
              }
              else { 
                currNode.materials.Clear();
                // TODO: Change this based on the outer material!!
                currNode.materials.Add(new NodeMaterialContrib(Resources.Load<Material>("Materials/DefaultCoreMat"), 1f));
              }
            }
          }
          else {
            inTerrainOnZ = false;
          }
        }
      }
    }
  }

  public void changeTerrainColumnHeight(
    int xIdx, int zIdx, float newYPos, float maxHeight, int insetXAmount, int insetNegXAmount,
    int insetZAmount, int insetNegZAmount, Material mat
  ) {

    var colIdx = new Vector3Int(xIdx, 0, zIdx);

    TerrainColumn terrainCol;
    if (!terrainColumns.TryGetValue(colIdx, out terrainCol)) { return; }

    // Get the current height of the column
    float currHeight = fastSampleHeight(xIdx, zIdx);
    float heightChange = (newYPos - currHeight);
    //Debug.Log("Prev scale: " + currScale + ", New scale: " + newScale);
    if (Mathf.Abs(heightChange) >= halfUnitsPerNode()) {
      // Adjust the height of the terrain column based on the given change
      float newHeight = Mathf.Clamp(currHeight + heightChange, 0, Mathf.Min(maxHeight, TerrainColumn.size*ySize));
      //Debug.Log("Prev Height: " + currHeight + ", New Height: " + newHeight);

      var yStartNodeIdx = unitsToNodeIndex(currHeight);
      var yEndNodeIdx = unitsToNodeIndex(newHeight);
      float isoVal = 1f;
      if (heightChange < 0) {
        // Removing nodes... swap values
        int temp = yStartNodeIdx;
        yStartNodeIdx = yEndNodeIdx;
        yEndNodeIdx = temp;
        isoVal = -1f;
      }

      var editNodes = new List<TerrainGridNode>();
      var idxRange = getIndexRangeForTerrainColumn(terrainCol);
      idxRange.xStartIdx = (int)Mathf.Clamp(idxRange.xStartIdx + insetXAmount,  0, numNodesX()-1); 
      idxRange.xEndIdx   = (int)Mathf.Clamp(idxRange.xEndIdx - insetNegXAmount, 0, numNodesX()-1);
      idxRange.zStartIdx = (int)Mathf.Clamp(idxRange.zStartIdx + insetZAmount,  0, numNodesZ()-1); 
      idxRange.zEndIdx   = (int)Mathf.Clamp(idxRange.zEndIdx - insetNegZAmount, 0, numNodesZ()-1);

      for (int x = idxRange.xStartIdx; x <= idxRange.xEndIdx; x++) {
        for (int y = yStartNodeIdx; y <= yEndNodeIdx; y++) {
          for (int z = idxRange.zStartIdx; z <= idxRange.zEndIdx; z++) {
            editNodes.Add(getNode(new Vector3Int(x,y,z)));
          }
        }
      }
      if (mat) { addIsoValuesAndMaterialToNodes(isoVal, 1f, mat, editNodes); }
      else { addIsoValuesToNodes(isoVal, editNodes); }
    }
  }

  // Intersect the given ray with this grid, returns the closest relevant edit point
  // when there's a collision (on return true). Returns false on no collision.
  public bool intersectEditorRay(in Ray ray, float yOffset, out Vector3 editPt) {
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
    editPt.y += yOffset;

    return true;
  }

  #endif // UNITY_EDITOR
}
