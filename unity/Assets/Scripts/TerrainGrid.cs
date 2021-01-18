using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

[ExecuteAlways]
public partial class TerrainGrid : MonoBehaviour, ISerializationCallbackReceiver {

  public static readonly string GAME_OBJ_NAME = "Terrain";
  public static TerrainGrid FindTerrainGrid() {
    var terrainGO = GameObject.Find(GAME_OBJ_NAME);
    if (!terrainGO) {
      Debug.LogError("Could not find '" + GAME_OBJ_NAME + "' GameObject!");
      return null;
    }
    var terrainGrid = terrainGO.GetComponent<TerrainGrid>();
    if (!terrainGrid) {
      Debug.LogError("Could not find TerrainGrid component!");
      return null;
    }
    return terrainGrid;
  }

  public static readonly int NODES_PER_UNIT = 5;

  public LevelData levelData;
  public GameObject columnsParent;
  
  [SerializeField] private LevelLoaderData levelLoader;
  [SerializeField] private FloatReference debrisUpdateFrequency;
  [SerializeField] private LiquidCompute liquidCompute;
  [SerializeField] private LiquidVolumeRaymarcher liquidVolumeRaymarcher;

  public TerrainSharedAssetContainer terrainAssetContainer { get; private set; }

  // Live data used in the editor and in-game, note that we serialize these fields so that we can undo/redo edit operations
  private float debrisTimeCounter = 0;
  private TerrainGridNode[,,] nodes; // Does not include the outer "ghost" nodes with zeroed isovalues
  private Dictionary<Vector3Int, TerrainColumn> terrainColumns = new Dictionary<Vector3Int, TerrainColumn>();
  private Dictionary<GameObject, TerrainDebrisDiff> debrisNodeDict = new Dictionary<GameObject, TerrainDebrisDiff>();
  private bool debrisToLiquidNeedsUpdate = false;
  private Bedrock bedrock;

  public int xSize { get { return levelData.xSize; } }
  public int ySize { get { return levelData.ySize; } }
  public int zSize { get { return levelData.zSize; } }

  public int XUnitSize() { return xSize * TerrainColumn.SIZE; }
  public int YUnitSize() { return ySize * TerrainColumn.SIZE; }
  public int ZUnitSize() { return zSize * TerrainColumn.SIZE; }
  public Vector3Int UnitSizeVec3() { return new Vector3Int(XUnitSize(), YUnitSize(), ZUnitSize()); }

  public int NumNodesX() { return LevelData.sizeToNumNodes(xSize); }
  public int NumNodesY() { return LevelData.sizeToNumNodes(ySize); }
  public int NumNodesZ() { return LevelData.sizeToNumNodes(zSize); }
  public int NumNodes()  { return NumNodesX()*NumNodesY()*NumNodesZ(); }

  public static float UnitsPerNode() { return (((float)TerrainColumn.SIZE) / ((float)TerrainGrid.NODES_PER_UNIT-1)); }
  public static float HalfUnitsPerNode() { return (0.5f * UnitsPerNode()); }
  public static Vector3 UnitsPerNodeVec3() { var u = UnitsPerNode(); return new Vector3(u,u,u); }
  public static Vector3 HalfUnitsPerNodeVec3() { var v = UnitsPerNodeVec3(); return 0.5f*v; }
  public static float UnitVolumePerNode() { return Mathf.Pow(UnitsPerNode(),3f); }
  public static int UnitsToNodeIndex(float unitVal) { return Mathf.FloorToInt(unitVal / UnitsPerNode()); }
  public static float NodeIndexToUnits(int idx) { return idx*UnitsPerNode(); }
  public static Vector3 NodeIndexToUnitsVec3(int x, int y, int z) { return NodeIndexToUnitsVec3(new Vector3Int(x,y,z)); }
  public static Vector3 NodeIndexToUnitsVec3(in Vector3Int nodeIdx) {
    var nodeUnitsVec = UnitsPerNodeVec3(); return Vector3.Scale(nodeIdx, nodeUnitsVec); 
  }

  public Vector3Int UnitsToNodeIndexVec3(float x, float y, float z) { 
    return new Vector3Int(
      Mathf.Clamp(UnitsToNodeIndex(x), 0, nodes.GetLength(0)-1), 
      Mathf.Clamp(UnitsToNodeIndex(y), 0, nodes.GetLength(1)-1), 
      Mathf.Clamp(UnitsToNodeIndex(z), 0, nodes.GetLength(2)-1));
  }
  public Vector3Int UnitsToNodeIndexVec3(in Vector3 unitPos) { return UnitsToNodeIndexVec3(unitPos.x, unitPos.y, unitPos.z);}

  public TerrainColumn GetTerrainColumn(in Vector2Int tcIdx) { return GetTerrainColumn(new Vector3Int(tcIdx.x, 0, tcIdx.y)); }
  public TerrainColumn GetTerrainColumn(in Vector3Int tcIdx) { 
    TerrainColumn result = null;
    terrainColumns?.TryGetValue(tcIdx, out result);
    return result;
  }

  public TerrainColumnLanding GetLanding(CharacterPlacement placement) {
    return GetLanding(placement.Location); 
  }
  public TerrainColumnLanding GetLanding(Vector3Int location) {
    var terrainCol = GetTerrainColumn(location);
    return terrainCol.landings[location.y];
  }


  public void GetGridSnappedPoint(ref Vector3 wsPt) {
    // Find the closest column center to the given point and snap to it
    var lsPt = wsPt - transform.position; // local space

    var unitsPerTC = (UnitsPerNode()*TerrainColumn.SIZE*(NODES_PER_UNIT-1));
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

  public sealed class IndexRange {
    public int xStartIdx;
    public int xEndIdx;
    public int yStartIdx;
    public int yEndIdx;
    public int zStartIdx;
    public int zEndIdx;
  }

  private IndexRange GetIndexRangeForBox(in Bounds bounds) {
    return GetIndexRangeForMinMax(bounds.min, bounds.max);
  }
  private IndexRange GetIndexRangeForMinMax(in Vector3 min, in Vector3 max) {
    return new IndexRange {
      xStartIdx = Mathf.Clamp(UnitsToNodeIndex(min.x), 0, nodes.GetLength(0)-1),
      xEndIdx   = Mathf.Clamp(UnitsToNodeIndex(max.x), 0, nodes.GetLength(0)-1),
      yStartIdx = Mathf.Clamp(UnitsToNodeIndex(min.y), 0, nodes.GetLength(1)-1),
      yEndIdx   = Mathf.Clamp(UnitsToNodeIndex(max.y), 0, nodes.GetLength(1)-1),
      zStartIdx = Mathf.Clamp(UnitsToNodeIndex(min.z), 0, nodes.GetLength(2)-1),
      zEndIdx   = Mathf.Clamp(UnitsToNodeIndex(max.z), 0, nodes.GetLength(2)-1)
    };
  }

  public IndexRange GetIndexRangeForTerrainColumn(in TerrainColumn terrainCol) {
    return GetIndexRangeForTerrainColumn(terrainCol.location);
  }
  public IndexRange GetIndexRangeForTerrainColumn(in Vector3Int terrainColIdx) {
    var numNodesPerTCMinus1 = TerrainColumn.SIZE*TerrainGrid.NODES_PER_UNIT-1;
    var nodeXIdxStart = terrainColIdx.x * numNodesPerTCMinus1;
    var nodeZIdxStart = terrainColIdx.z * numNodesPerTCMinus1;
    return new IndexRange {
      xStartIdx = nodeXIdxStart,
      xEndIdx   = nodeXIdxStart + numNodesPerTCMinus1,
      yStartIdx = 0,
      yEndIdx   = NumNodesY() - 1,
      zStartIdx = nodeZIdxStart,
      zEndIdx   = nodeZIdxStart + numNodesPerTCMinus1
    };
  }

  [Flags] public enum TerrainNodeSelectors { All=0xFF00, Surface=0x000F, AboveSurface=0x00F0, None=0x0000 };
  public HashSet<TerrainGridNode> getNodesInTerrainColumn(
    in Vector3Int terrainColIdx, int maxYNodeIdx, in TerrainNodeSelectors nodeSelector=TerrainNodeSelectors.All 
  ) {

    var tcNodes = new HashSet<TerrainGridNode>();
    var tcIndices = GetIndexRangeForTerrainColumn(terrainColIdx);
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
                var neighbours = Get6NeighboursForNode(currNode);
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

  public List<TerrainGridNode> Get6NeighboursForNode(in TerrainGridNode node) {
    var neighbours = new List<TerrainGridNode>();
    var idx = node.gridIndex;
    if (idx.x > 0) { neighbours.Add(nodes[idx.x-1,idx.y,idx.z]); }
    if (idx.x < nodes.GetLength(0)-1) { neighbours.Add(nodes[idx.x+1,idx.y,idx.z]); }
    if (idx.y > 0) { neighbours.Add(nodes[idx.x,idx.y-1,idx.z]); }
    if (idx.y < nodes.GetLength(1)-1) { neighbours.Add(nodes[idx.x,idx.y+1,idx.z]); }
    if (idx.z > 0) { neighbours.Add(nodes[idx.x,idx.y,idx.z-1]); }
    if (idx.z < nodes.GetLength(2)-1) { neighbours.Add(nodes[idx.x,idx.y,idx.z+1]); }
    return neighbours;
  }

  private static readonly Vector3Int[] NEIGHBOUR_26_INDICES = new Vector3Int[]{
    new Vector3Int(1,1,-1),  new Vector3Int(1,1,0),  new Vector3Int(1,1,1), 
    new Vector3Int(1,0,-1),  new Vector3Int(1,0,0),  new Vector3Int(1,0,1),
    new Vector3Int(1,-1,-1), new Vector3Int(1,-1,0), new Vector3Int(1,-1,1),

    new Vector3Int(0,1,-1),  new Vector3Int(0,1,0),  new Vector3Int(0,1,1),
    new Vector3Int(0,0,-1),                          new Vector3Int(0,0,1),
    new Vector3Int(0,-1,-1), new Vector3Int(0,-1,0), new Vector3Int(0,-1,1),

    new Vector3Int(-1,1,-1),  new Vector3Int(-1,1,0),  new Vector3Int(-1,1,1), 
    new Vector3Int(-1,0,-1),  new Vector3Int(-1,0,0),  new Vector3Int(-1,0,1),
    new Vector3Int(-1,-1,-1), new Vector3Int(-1,-1,0), new Vector3Int(-1,-1,1),
  };
  public List<TerrainGridNode> Get26NeighboursForNode(in TerrainGridNode node) {
    var neighbours = new List<TerrainGridNode>();
    var idx = node.gridIndex;

    foreach (var neighbourIdx in NEIGHBOUR_26_INDICES) {
      var currIdx = idx + neighbourIdx;
      if (currIdx.x > 0 && currIdx.x < nodes.GetLength(0)-1 && 
          currIdx.y > 0 && currIdx.y < nodes.GetLength(1)-1 &&
          currIdx.z > 0 && currIdx.z < nodes.GetLength(2)-1) {
        neighbours.Add(nodes[currIdx.x, currIdx.y, currIdx.z]);
      }
    }
    return neighbours;
  }

  public List<TerrainGridNode> GetNodesInsideBox(in Bounds box, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var result = new List<TerrainGridNode>();
    var indices = GetIndexRangeForBox(box);
    var maxYIdx = UnitsToNodeIndex(setLevelUnits);
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

  public List<TerrainGridNode> GetNodesInsideSphere(in Vector3 center, float radius, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = center - transform.position; // Get the center in localspace

    // Narrow the search down to the nodes inside the sphere's bounding box
    var dia = 2*radius;
    var nodesInBox = GetNodesInsideBox(new Bounds(lsCenter, new Vector3(dia, dia, dia)), groundFirst, liquidFirst, setLevelUnits);

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

  public List<TerrainGridNode> GetNodesInsideProjXZCircle(in Vector3 center, float radius, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = center - transform.position; // Get the center in localspace
    
    // Find the highest y node in the xz coordinates with an isovalue
    var isCenter = UnitsToNodeIndexVec3(lsCenter); isCenter.y++;
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
    isCenter.y = Mathf.Min(Mathf.Max(0, UnitsToNodeIndex(setLevelUnits-radius)), isCenter.y);

    // Get the nodes in a square box that encloses the circle
    var dia = 2*radius;
    var box = new Bounds(NodeIndexToUnitsVec3(isCenter), new Vector3(dia, 1.5f*HalfUnitsPerNode(), dia));
    var indices = GetIndexRangeForBox(box);
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
  public List<TerrainGridNode> GetNodesInsideProjXZSquare(in Bounds box, bool groundFirst, bool liquidFirst, float setLevelUnits) {
    var lsCenter = box.center - transform.position; // Get the center in localspace
    // Find the highest y node in the xz coordinates with an isovalue
    var isCenter = UnitsToNodeIndexVec3(lsCenter); isCenter.y++;
    while (isCenter.y > 1 && nodes[isCenter.x,isCenter.y-1,isCenter.z].isEmpty()) { isCenter.y--; }
    while (isCenter.y < nodes.GetLength(1)-1 && !nodes[isCenter.x,isCenter.y,isCenter.z].isEmpty()) { isCenter.y++; }

    var projBox = new Bounds(NodeIndexToUnitsVec3(isCenter), new Vector3(box.size.x, 1.5f*HalfUnitsPerNode(), box.size.z));
    return GetNodesInsideBox(projBox, groundFirst, liquidFirst, setLevelUnits);
  }


  public static Vector3Int TerrainColumnNodeIndex(in TerrainColumn terrainCol, in Vector3Int localIdx) {
    return TerrainColumnNodeIndex(terrainCol.location, localIdx);
  }
  public static Vector3Int TerrainColumnNodeIndex(in Vector3Int terrainColIdx, in Vector3Int localIdx) {
    return new Vector3Int(
      terrainColIdx.x * (TerrainGrid.NODES_PER_UNIT * TerrainColumn.SIZE - 1), 0, 
      terrainColIdx.z * (TerrainGrid.NODES_PER_UNIT * TerrainColumn.SIZE - 1)
    ) + localIdx;
  }

  public TerrainGridNode GetNode(in Vector3Int nodeIdx) {
    // If the index is outside of the node grid then we're dealing with a "ghost" node
    if (nodeIdx.x < 0 || nodeIdx.x > this.nodes.GetLength(0)-1 ||
        nodeIdx.y < 0 || nodeIdx.y > this.nodes.GetLength(1)-1 ||
        nodeIdx.z < 0 || nodeIdx.z > this.nodes.GetLength(2)-1) {
      var gridSpacePos = NodeIndexToUnitsVec3(nodeIdx);
      // NOTE: Since the node is outside the grid of TerrainColumns 
      // we don't associate any with this "ghost" node
      return new TerrainGridNode(gridSpacePos, nodeIdx, 0);
    }
    return nodes[nodeIdx.x,nodeIdx.y,nodeIdx.z];
  }

  private void LoadLevelDataNodes() {
    if (levelData != null) {
      // When we load the nodes we are only loading their index and isovalue, the rest of
      // the information inside each node needs to be rebuilt (this is done in generateNodes).
      nodes = levelData.getNodesAs3DArray();
      GenerateNodes();
    }
  }
  public void OnBeforeSerialize() {
  }
  public void OnAfterDeserialize() {
    LoadLevelDataNodes();
  }

  /// <summary>
  /// Called to initialize this in play mode from the Scene Manager.
  /// </summary>
  public void Init(LevelData level) {
    if (!Application.IsPlaying(gameObject)) {
      Debug.LogWarning("Init should not be called unless the game is playing!");
      return;
    }

    // If we're coming into this scene from the loading screen then we load the level it provides,
    // otherwise take whatever level data is already set in the inspector.
    // IMPORTANT NOTE: We must instantiate the level data in game mode or else we'll overwrite the asset while the game plays.
    if (level != null) {
      levelData = Instantiate<LevelData>(level);
    }
    else if (levelData != null) {
      levelData = Instantiate<LevelData>(levelData);
    }
    else {
      Debug.LogError("No level data is available for loading the terrain!");
      return;
    }
    LoadLevelDataNodes();

    // Build all the assets for the game that aren't stored in the level data
    BuildTerrainLiquid();
    BuildBedrock();
    RegenerateMeshes(BuildTerrainColumns());
    TerrainPhysicsCleanup();
  }

  private void Awake() {
    terrainAssetContainer = GetComponent<TerrainSharedAssetContainer>();
    Debug.Assert(terrainAssetContainer != null);
  }

  private void Start() {
    // NOTE: When the application is running, we set up the nodes in Awake(),
    // all other initialization is done via the scene manager and calling Init()

    #if UNITY_EDITOR
    if (!Application.IsPlaying(gameObject)) {
      if (levelData != null) {
        LoadLevelDataNodes(); // Always load the nodes first!
        BuildTerrainLiquid();
        BuildBedrock();
        BuildTerrainColumns();
        RegenerateMeshes(BuildTerrainColumns());
        // No terrainPhysicsCleanup in editor!
      }
    }
    #endif

    // TESTING saving scriptable object references
    //var saveSlot = ScriptableObject.CreateInstance<SaveSlotData>();
    //saveSlot.currentLevel = LevelLoaderData.loadLevelData(LevelLoaderData.DEFAULT_LEVEL_STR);
    //saveSlot.save();
    //Debug.Log("Saving slot data to " + saveSlot.saveFilepath());
  }

  private void FixedUpdate() {
    float debrisUpdateTime = (2f/debrisUpdateFrequency.value);

    debrisTimeCounter += Time.fixedDeltaTime;
    if (debrisToLiquidNeedsUpdate && debrisNodeDict != null && debrisNodeDict.Count > 0 && debrisTimeCounter >= debrisUpdateTime) {
      debrisTimeCounter = 0;
      debrisToLiquidNeedsUpdate = false;
      liquidCompute.ReadUpdateNodesFromLiquid(nodes);

      // Go through all the nodes that might be inside the debris, if any have liquid in them then we check to make sure
      // it's actually inside the debris and displace it to the closest empty non-terrain node if it is
      var allCurrAffectedNodes = new HashSet<TerrainGridNode>();
      var affectedLiquidNodes = new HashSet<TerrainGridNode>();
      foreach (var debrisDictPair in debrisNodeDict) {
        var debrisDiffs = debrisDictPair.Value;
        allCurrAffectedNodes.UnionWith(debrisDiffs.currDebrisNodes);
        float liquidAmt = 0f;
        foreach (var debrisNode in debrisDiffs.currDebrisNodes) {
          if (debrisNode.liquidVol > 0) {
            affectedLiquidNodes.Add(debrisNode);
            liquidAmt += debrisNode.liquidVol;
          }
        }
        // If there's liquid in the way of the debris then we set the drag (due to bouyancy) on its rigidbody
        // based on the fraction of its weight that's in the liquid
        var debrisGO = debrisDictPair.Key;
        var debrisRB = debrisGO.GetComponent<Rigidbody>();
        if (liquidAmt > 0) {
          debrisRB.drag = 15*liquidAmt*liquidCompute.liquidDensity / debrisRB.mass;
        }
        else {
          var debrisCollider = debrisGO.GetComponent<Collider>();
          debrisRB.drag = TerrainDebris.GetDrag(debrisCollider.bounds);
        }
      }
      var changedLiquidNodes = DisplaceNodeLiquid(affectedLiquidNodes, allCurrAffectedNodes);
      if (changedLiquidNodes.Count > 0) {
        liquidCompute.WriteUpdateDebrisDiffToLiquid(debrisNodeDict, changedLiquidNodes);
      }
    }
    
  }

  /// <summary>
  /// Generates and fills data in for the non-serialized node data based on the serialized data (array of isovalues) alone.
  /// This method is meant to be as robust as possible so it can properly initialize the runtime node array in almost any state.
  /// </summary>
  private void GenerateNodes() {
    var numNodesX = this.NumNodesX();
    var numNodesY = this.NumNodesY();
    var numNodesZ = this.NumNodesZ();

    // We use a temporary array to preserve any existing nodes that are still in the grid
    TerrainGridNode[,,] tempNodes = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];

    var tcXIndices = new List<int>();
    var tcZIndices = new List<int>();
    var tcIndices = new List<Vector3Int>();
    var nodesPerTCMinus1 = TerrainColumn.SIZE*TerrainGrid.NODES_PER_UNIT - 1;
    for (int x = 0; x < numNodesX; x++) {
      var xPos = NodeIndexToUnits(x);

      // A node might be associated with more than one TerrainColumn, figure out
      // all the indices that can be tied to the node
      var currXTCIdx = Math.Max(0, Mathf.FloorToInt((x-1)/nodesPerTCMinus1));
      tcXIndices.Clear();
      tcXIndices.Add(currXTCIdx);
      if (x % nodesPerTCMinus1 == 0 && x > 0 && currXTCIdx+1 < xSize) {
        tcXIndices.Add(currXTCIdx+1);
      }

      for (int z = 0; z < numNodesZ; z++) {
        var zPos = NodeIndexToUnits(z);

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
          var yPos = NodeIndexToUnits(y);
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

  private List<TerrainColumn> BuildTerrainColumns() {
    var keysToRemove = new List<Vector3Int>();
    foreach (var entry in terrainColumns) {
      var idx = entry.Key;
      if (idx.x >= xSize || idx.y >= ySize || idx.z >= zSize) {
        keysToRemove.Add(idx);
      }
    }
    foreach (var key in keysToRemove) {
      Destroy(terrainColumns[key]);
    }

    terrainColumns.Clear();
    var newTerrainCols = new List<TerrainColumn>();
    for (int x = 0; x < xSize; x++) {
      for (int z = 0; z < zSize; z++) {
        var currIdx = new Vector3Int(x,0,z);
        var terrainCol = TerrainColumn.GetUniqueTerrainColumn(this, currIdx);
        if (terrainCol) {
          terrainColumns.Add(currIdx, terrainCol);
          newTerrainCols.Add(terrainCol);
        }
      }
    }

    return newTerrainCols;
  }


  private void BuildBedrock() {
    bedrock = GetComponentInChildren<Bedrock>();
    bedrock.UpdateMesh(this);
  }
  private void BuildTerrainLiquid() {
    // Order matters here
    liquidVolumeRaymarcher.InitAll();
    liquidCompute.InitAll();
    liquidCompute.enableSimulation = Application.IsPlaying(gameObject);
    liquidCompute.WriteUpdateNodesAndDebrisToLiquid(nodes, debrisNodeDict);
  }

  private void RegenerateMeshes() {
    foreach (var terrainCol in terrainColumns.Values) {
      terrainCol.RegenerateMesh(this);
    }
    bedrock.UpdateMesh(this);
  }

  private void RegenerateMeshes(in ICollection<TerrainColumn> terrainCols) {
    foreach (var terrainCol in terrainCols) {
      terrainCol.RegenerateMesh(this);
    }
  }

  private HashSet<TerrainColumn> RegenerateMeshes(in IEnumerable<TerrainGridNode> nodes) {
    var terrainCols = FindAllAffectedTCs(nodes);
    RegenerateMeshes(terrainCols);
    return terrainCols;
  }

  /// <summary>
  /// Obtain all of the unique TerrainColumns that are affected and will need to be regenerated 
  /// for the changes to the given nodes.
  /// </summary>
  /// <param name="changedNodes">Nodes that have changed.</param>
  /// <returns>Unique set of TerrainColumns that are affected by the changed nodes.</returns>
  private HashSet<TerrainColumn> FindAllAffectedTCs(in IEnumerable<TerrainGridNode> changedNodes) {
    var terrainCols = new HashSet<TerrainColumn>();

    // We need to consider to add TerrainColumns associated with any adjacent nodes to avoid seams and other artifacts
    // caused by the node/cube dependancies that exist at the edges of each TerrainColumn mesh
    var allAffectedNodes = new HashSet<TerrainGridNode>(changedNodes);
    foreach (var node in changedNodes) {
      allAffectedNodes.UnionWith(Get26NeighboursForNode(node));
    }

    foreach (var node in allAffectedNodes) {
      foreach (var tcIndex in node.columnIndices) { terrainCols.Add(terrainColumns[tcIndex]); }
    }

    return terrainCols;
  }

  private GameObject BuildTerrainDebris(in HashSet<TerrainGridNode> nodes) {

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
    var nodeUnits = UnitsPerNode();
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
    var debrisObj = Instantiate<GameObject>(terrainAssetContainer.debrisPrefab);
    debrisObj.transform.position = debrisCenterPos;
    debrisObj.transform.SetParent(transform);
    debrisObj.GetComponent<TerrainDebris>().RegenerateMesh(this, nodeCorners);

    return debrisObj;
  }

  public HashSet<TerrainGridNode> FindPeripheryNodes(in HashSet<TerrainGridNode> internalNodes) {
    var peripheryNodes = new HashSet<TerrainGridNode>();
    foreach (var node in internalNodes) {
      var neighbours = Get6NeighboursForNode(node);
      foreach (var neighbour in neighbours) {
        if (!internalNodes.Contains(neighbour)) { peripheryNodes.Add(neighbour); }
      }
    }
    return peripheryNodes;
  }

  private List<TerrainGridNode> DisplaceNodeLiquid(IEnumerable<TerrainGridNode> affectedLiquidNodes, HashSet<TerrainGridNode> displacedNodes) {
    var peripheryNodes = FindPeripheryNodes(displacedNodes);
    var fillableNodes = new List<TerrainGridNode>();
    foreach (var peripheryNode in peripheryNodes) {
      if (!peripheryNode.isTerrain()) { fillableNodes.Add(peripheryNode); }
    }
    // Distribute liquid evenly across all the non-terrain periphery nodes
    if (fillableNodes.Count > 0) {
      foreach (var liquidNode in affectedLiquidNodes) {
        var liquidPerNode = liquidNode.liquidVol / (float)fillableNodes.Count;
        foreach (var fillableNode in fillableNodes) {
          fillableNode.liquidVol += liquidPerNode;
        }
        liquidNode.liquidVol = 0;
      }
    }
    return fillableNodes;
  } 
  
  // This function updates a dictionary of debris nodes then in the FixedUpdate method 
  // we go through that dictionary and perform the proper read/write updates from/to the liquid simulation
  // and the liquid quantities in nodes - The debris needs to be treated as solid nodes while it moves without being a part of the nodes array
  public void UpdateMovingDebris(GameObject debrisGO) {
    var debrisCollider = debrisGO.GetComponent<Collider>();
    var debrisRenderer = debrisGO.GetComponent<Renderer>();
    var debrisWSBounds = debrisRenderer.bounds;

    // Find the bounds in node index space    
    var nodeIdxRange = GetIndexRangeForMinMax(debrisWSBounds.min - transform.position, debrisWSBounds.max - transform.position);

    // Keep the nodes associated with the debris up-to-date in the debrisNodeDict
    var affectedNodes = new HashSet<TerrainGridNode>();
    var closenessEpsilon = 0.5f*HalfUnitsPerNode();
    for (int x = nodeIdxRange.xStartIdx; x <= nodeIdxRange.xEndIdx; x++) {
      for (int y = nodeIdxRange.yStartIdx; y <= nodeIdxRange.yEndIdx; y++) {
        for (int z = nodeIdxRange.zStartIdx; z <= nodeIdxRange.zEndIdx; z++) {
          var node = nodes[x,y,z];
          var nodeWSPos = node.position + transform.position;
          if (debrisCollider.IsPointInside(nodeWSPos, closenessEpsilon)) { affectedNodes.Add(node); }
        }
      }
    }
    TerrainDebrisDiff prevDiff;
    if (debrisNodeDict.TryGetValue(debrisGO, out prevDiff)) {
      // Only indicate that there's an update if the debris has changed position/orientation such that 
      // the set of nodes that fill it has also changed
      if (!affectedNodes.SetEquals(prevDiff.currDebrisNodes)) {
        prevDiff.prevDebrisNodes = prevDiff.currDebrisNodes;
        prevDiff.currDebrisNodes = affectedNodes;
        debrisToLiquidNeedsUpdate = true;
      }
    }
    else {
      debrisNodeDict.Add(debrisGO, new TerrainDebrisDiff(new HashSet<TerrainGridNode>(), affectedNodes));
      debrisToLiquidNeedsUpdate = true;
    }
  }

  public void MergeDebrisIntoTerrain(GameObject debrisGO) {
    // We MUST read the current state of the fluid simulation first so that we know what nodes have liquid in them
    // so that it can be properly found and displaced by the merging debris
    liquidCompute.ReadUpdateNodesFromLiquid(nodes);

    // Get the bounding box of the debris in worldspace - we use this as a rough estimate
    // of which nodes we need to iterate through to convert the mesh back into nodes
    var debrisRenderer = debrisGO.GetComponent<Renderer>();
    var debrisComponent = debrisGO.GetComponent<TerrainDebris>();
    var debrisWSBounds = debrisRenderer.bounds;

    // Find the bounds in node index space
    var nodeIdxRange = GetIndexRangeForMinMax(debrisWSBounds.min - transform.position, debrisWSBounds.max - transform.position);

    // Go through each potential node and cast a ray from the center of that node (in any direction)
    // If the ray intersects with the inside of the debris' mesh then add back the isovalue for that node
    var closenessEpsilon = 0.5f*HalfUnitsPerNode();
    var debrisCollider = debrisGO.GetComponent<Collider>();
    var affectedNodes = new HashSet<TerrainGridNode>();
    var affectedLiquidNodes = new List<TerrainGridNode>();
    for (int x = nodeIdxRange.xStartIdx; x <= nodeIdxRange.xEndIdx; x++) {
      for (int y = nodeIdxRange.yStartIdx; y <= nodeIdxRange.yEndIdx; y++) {
        for (int z = nodeIdxRange.zStartIdx; z <= nodeIdxRange.zEndIdx; z++) {
          var node = nodes[x,y,z];
          var nodeWSPos = node.position + transform.position;
          if (debrisCollider.IsPointInside(nodeWSPos, closenessEpsilon)) {
            // Map the world space position into the local space node index of the debris (via DebrisNodeMapper)
            // use this to determine what the original material was
            var origCorner = debrisComponent.MapFromWorldspace(nodeWSPos);
            node.isoVal = 1;
            node.materials = origCorner.materials;
            affectedNodes.Add(node);
            if (node.liquidVol > 0) { affectedLiquidNodes.Add(node); }
          }
        }
      }
    }

    // If there were liquid values in the nodes where we added the debris back to the terrain then
    // we need to displace that water to the nearest non-terrain node
    if (affectedLiquidNodes.Count > 0) {
      DisplaceNodeLiquid(affectedLiquidNodes, affectedNodes);
    }

    // Regenerate the new meshes which will incorporate the debris into the terrain
    RegenerateMeshes(affectedNodes);
    
    // Remove the debris now that it has been merged into the terrain nodes
    debrisNodeDict.Remove(debrisGO);
    Destroy(debrisGO);

    // Update the liquid simulation with the newly merged terrain nodes and any remaining debris
    liquidCompute.WriteUpdateNodesAndDebrisToLiquid(nodes, debrisNodeDict);
  }

  public HashSet<TerrainColumn> TerrainPhysicsCleanup() {
    return TerrainPhysicsCleanup(new List<TerrainColumn>());
  }
  public HashSet<TerrainColumn> TerrainPhysicsCleanup(in IEnumerable<TerrainColumn> affectedTCs) {
    Debug.Assert(Application.IsPlaying(gameObject), "This function should only be called while the application is playing.");

    TerrainNodeTraverser.UpdateGroundedNodes(this, affectedTCs);
    var islands = TerrainNodeTraverser.TraverseNodeIslands(this);
    var resultTCs = new HashSet<TerrainColumn>(affectedTCs);
    foreach (var islandNodeSet in islands) {
      // Build debris for each island
      var debrisGO = BuildTerrainDebris(islandNodeSet);
      // Add the nodes occupied by the debris to the debris dictionary
      debrisNodeDict.Add(debrisGO, new TerrainDebrisDiff(new HashSet<TerrainGridNode>(), islandNodeSet));
      // And clear the isovalues for all the nodes that make up the debris (which is now a separate mesh)
      foreach (var node in islandNodeSet) {
        foreach (var tcIndex in node.columnIndices) { resultTCs.Add(terrainColumns[tcIndex]); }
        node.isoVal = 0;
      }
    }

    RegenerateMeshes(resultTCs);

    // Read/write from/to the liquid simulation
    liquidCompute.ReadUpdateNodesFromLiquid(nodes);
    liquidCompute.WriteUpdateNodesAndDebrisToLiquid(nodes, debrisNodeDict);

    return resultTCs;
  }

  private void UpdateNodesInGameOrEditor(in IEnumerable<TerrainGridNode> changedNodes) {
    #if UNITY_EDITOR
    if (!Application.IsPlaying(gameObject)) {
      updateNodesInEditor(changedNodes);
    }
    else 
    #endif
    {
      var affectedTCs = FindAllAffectedTCs(changedNodes);
      TerrainPhysicsCleanup(affectedTCs);
    }
  }

  public void AddLiquidToNodes(float liquidVolPercentage, in List<TerrainGridNode> _nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    var maxNodeVol = UnitVolumePerNode();
    var liquidVol  = maxNodeVol*liquidVolPercentage;
    foreach (var node in _nodes) {
      var prevVol = node.liquidVol;
      var prevIsoVal = node.isoVal;

      if (node.isTerrain()) { node.liquidVol = 0f; } // If the node is land then we can't add liquid to it
      else { node.liquidVol = Mathf.Clamp(node.liquidVol + liquidVol, 0, maxNodeVol); node.isoVal = 0f; }

      if (node.liquidVol != prevVol || node.isoVal != prevIsoVal) { changedNodes.Add(node); }
    }
    UpdateNodesInGameOrEditor(changedNodes);
  }

  public void AddIsoValuesToNodes(float isoVal, in List<TerrainGridNode> _nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    foreach (var node in _nodes) {
      var prevIsoVal = node.isoVal;

      if (node.isLiquid()) { node.isoVal = 0f; } // If the node is liquid then we can't add land to it
      else { node.isoVal = Mathf.Clamp(node.isoVal + isoVal, 0, 1); }
      
      if (node.isoVal != prevIsoVal) { changedNodes.Add(node); }
    }
    UpdateNodesInGameOrEditor(changedNodes);
  }

  public void AddIsoValuesAndMaterialToNodes(float isoVal, float matAmt, Material mat, in List<TerrainGridNode> _nodes) {
    var changedNodes = new HashSet<TerrainGridNode>();
    var matToPaint = mat ? mat : terrainAssetContainer.defaultTerrainMaterial;
    foreach (var node in _nodes) {
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
    UpdateNodesInGameOrEditor(changedNodes);
  }

  // Editor-Only Stuff ----------------------------------------------
  #if UNITY_EDITOR

  public void OnValidate() {
    if (!Application.IsPlaying(gameObject)) {
      Invoke("delayedOnValidate", 0);
    }
  }
  void delayedOnValidate() {
    if (levelData != null) {
      LoadLevelDataNodes();
      BuildTerrainLiquid();
      BuildBedrock();
      RegenerateMeshes(BuildTerrainColumns());
    }
  }

  void OnDrawGizmos() {
    if (Application.IsPlaying(gameObject)) { return; }
    Gizmos.color = new Color(1,1,1,0.5f);
    Gizmos.DrawWireCube(transform.position + 
      new Vector3(((float)xSize)/2,((float)ySize)/2,((float)zSize)/2), new Vector3(xSize,ySize,zSize)
    );
  }

  public float fastSampleHeight(int terrainColX, int terrainColZ) {
    if (nodes == null) { return 0f; }
    int nodeIdxX = terrainColX*TerrainColumn.SIZE*(NODES_PER_UNIT-1) + (NODES_PER_UNIT/2)*TerrainColumn.SIZE;
    int nodeIdxZ = terrainColZ*TerrainColumn.SIZE*(NODES_PER_UNIT-1) + (NODES_PER_UNIT/2)*TerrainColumn.SIZE;
    //Debug.Log("X: " + nodeIdxX + ", Z: " + nodeIdxZ + " Grid size: " + nodes.GetLength(0) + ", " + nodes.GetLength(1) + ", " + nodes.GetLength(2));
    int numYNodes = NumNodesY();
    int finalYNodeIdx = 0;
    for (int y = 0; y < numYNodes-1; y++) {
      if (!nodes[nodeIdxX, y+1, nodeIdxZ].isTerrain()) { break; }
      finalYNodeIdx++;
    }
    return finalYNodeIdx*UnitsPerNode();
  }

  public void updateNodesInEditor(in IEnumerable<TerrainGridNode> updateNodes) {
    if (Application.IsPlaying(gameObject)) { return; }
    var affectedTCs = RegenerateMeshes(updateNodes);
    // Update the liquid so we can see it in the editor
    liquidCompute.WriteUpdateNodesToLiquid(nodes);
    // Update the LevelData - since we're in editor mode, this will directly change the asset permenantly
    levelData.updateFromNodes(nodes, updateNodes);
  }

  public void fillCoreMaterial() {
    // This is similar to a rasterization algorithm - we trace along each line of nodes, when we encounter a node
    // that is inside the terrain we begin filling nodes with the core material until we find a node that is no
    // longer inside the terrain.
    int numXNodes = NumNodesX(); int numYNodes = NumNodesY(); int numZNodes = NumNodesZ();
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
    if (Mathf.Abs(heightChange) >= HalfUnitsPerNode()) {
      // Adjust the height of the terrain column based on the given change
      float newHeight = Mathf.Clamp(currHeight + heightChange, 0, Mathf.Min(maxHeight, TerrainColumn.SIZE*ySize));
      //Debug.Log("Prev Height: " + currHeight + ", New Height: " + newHeight);

      var yStartNodeIdx = UnitsToNodeIndex(currHeight);
      var yEndNodeIdx = UnitsToNodeIndex(newHeight);
      float isoVal = 1f;
      if (heightChange < 0) {
        // Removing nodes... swap values
        int temp = yStartNodeIdx;
        yStartNodeIdx = yEndNodeIdx;
        yEndNodeIdx = temp;
        isoVal = -1f;
      }

      var editNodes = new List<TerrainGridNode>();
      var idxRange = GetIndexRangeForTerrainColumn(terrainCol);
      idxRange.xStartIdx = (int)Mathf.Clamp(idxRange.xStartIdx + insetXAmount,  0, NumNodesX()-1); 
      idxRange.xEndIdx   = (int)Mathf.Clamp(idxRange.xEndIdx - insetNegXAmount, 0, NumNodesX()-1);
      idxRange.zStartIdx = (int)Mathf.Clamp(idxRange.zStartIdx + insetZAmount,  0, NumNodesZ()-1); 
      idxRange.zEndIdx   = (int)Mathf.Clamp(idxRange.zEndIdx - insetNegZAmount, 0, NumNodesZ()-1);

      for (int x = idxRange.xStartIdx; x <= idxRange.xEndIdx; x++) {
        for (int y = yStartNodeIdx; y <= yEndNodeIdx; y++) {
          for (int z = idxRange.zStartIdx; z <= idxRange.zEndIdx; z++) {
            editNodes.Add(GetNode(new Vector3Int(x,y,z)));
          }
        }
      }
      if (mat) { AddIsoValuesAndMaterialToNodes(isoVal, 1f, mat, editNodes); }
      else { AddIsoValuesToNodes(isoVal, editNodes); }
    }
  }

  // Intersect the given ray with this grid, returns the closest relevant edit point
  // when there's a collision (on return true). Returns false on no collision.
  public bool intersectEditorRay(in Ray ray, float yOffset, out Vector3 editPt) {
    editPt = new Vector3(0,0,0);

    if (nodes == null) { return false; }
    var gridBounds = new Bounds();
    gridBounds.SetMinMax(transform.position, transform.position + new Vector3(xSize, ySize, zSize));
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
      (int)Mathf.Clamp(UnitsToNodeIndex(lsStartPt.x), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(UnitsToNodeIndex(lsStartPt.y), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(UnitsToNodeIndex(lsStartPt.z), 0, nodes.GetLength(2)-1)
    );
    var lastVoxelIdx = new Vector3Int(
      (int)Mathf.Clamp(UnitsToNodeIndex(lsEndPt.x), 0, nodes.GetLength(0)-1),
      (int)Mathf.Clamp(UnitsToNodeIndex(lsEndPt.y), 0, nodes.GetLength(1)-1),
      (int)Mathf.Clamp(UnitsToNodeIndex(lsEndPt.z), 0, nodes.GetLength(2)-1)
    );

    // Distance along the ray to the next voxel border from the current position (tMax).
    var nodeSize = UnitsPerNode();
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
