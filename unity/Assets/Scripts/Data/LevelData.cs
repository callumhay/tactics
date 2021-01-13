using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

[PreferBinarySerialization]
[CreateAssetMenu(fileName="New Level", menuName="Tactics/Level")]
public class LevelData : ScriptableObject {

  public string levelName;
  public List<SceneReference> levelScenes;

  [Range(1,32)] public int xSize = 10, ySize = 10, zSize = 10; // Size in units (not nodes!) of the level
  public TerrainGridNode[] nodes;
  
  [SerializeField] private List<CharacterPlacement> placements = new List<CharacterPlacement>();
  [SerializeField] private int maxPlayerPlacements;

  public List<CharacterPlacement> Placements { get { return placements; } }
  public int MaxPlayerPlacements { get { return maxPlayerPlacements; } set { maxPlayerPlacements = value; } }

  public CharacterPlacement GetPlacementAt(Vector3Int location) {
    CharacterPlacement result = null;
    foreach (var placement in placements) {
      if (placement.Location == location) {
        result = placement;
        break;
      }
    }
    return result;
  }
  public List<CharacterPlacement> GetPlayerControlledPlacements() {
    var result = new List<CharacterPlacement>();
    foreach (var placement in placements) {
      if (placement.Team.IsPlayerControlled) { result.Add(placement); }
    }
    return result;
  }

  public bool HasLiquid() {
    foreach (var node in nodes) {
      if (node.isLiquid()) { return true; }
    }
    return false;
  }

  public static int node3DIndexToFlatIndex(int x, int y, int z, int numNodesY, int numNodesZ) {
    return z + (y*numNodesZ) + (x*numNodesZ*numNodesY);
  }
  public static Vector3Int flatIndexToNode3DIndex(int flatIdx, int numNodesY, int numNodesZ) {
    int x = flatIdx / (numNodesZ*numNodesY);
    int y = (flatIdx - x*numNodesZ*numNodesY) / numNodesZ;
    int z = flatIdx - x*numNodesY*numNodesY - y*numNodesZ;
    return new Vector3Int(x,y,z);
  }
  public static int numNodesToSize(int numNodes) {
    return (numNodes - 1)/(TerrainGrid.NODES_PER_UNIT*TerrainColumn.SIZE - 1);
  }
  
  /// <summary>
  /// Convert a size in terrain columns to the total number of nodes spanning that size.
  /// </summary>
  /// <param name="size">Number of terrain columns.</param>
  /// <returns>Number of nodes spanning the given number of terrain columns.</returns>
  public static int sizeToNumNodes(int size) {
    return (size*TerrainGrid.NODES_PER_UNIT*TerrainColumn.SIZE) + 1 - size;
  }

  public void updateFromNodes(in TerrainGridNode[,,] allNodes, in IEnumerable<TerrainGridNode> nodeUpdates) {
    // If this LevelData hasn't been initialized then we use all the nodes to initialze it and we're done
    if (nodes == null || nodes.Length != allNodes.Length) { 
      nodes = new TerrainGridNode[allNodes.Length]; 
      setNodesFrom3DArray(allNodes); 
    }
    else {
      // ... otherwise we just update the given nodeUpdates
      int numNodesY = allNodes.GetLength(1);
      int numNodesZ = allNodes.GetLength(2);
      
      foreach (var nodeUpdate in nodeUpdates) {
        var gridIdx = nodeUpdate.gridIndex;
        var flatIdx = LevelData.node3DIndexToFlatIndex(gridIdx.x, gridIdx.y, gridIdx.z, numNodesY, numNodesZ);
        var node = nodes[flatIdx];
        node.isoVal = nodeUpdate.isoVal;
        node.materials = nodeUpdate.materials;
      }
    }
  }

  public void setNodesFrom3DArray(in TerrainGridNode[,,] _nodes) {
    if (_nodes == null) { return; }

    var numNodesX = _nodes.GetLength(0);
    var numNodesY = _nodes.GetLength(1);
    var numNodesZ = _nodes.GetLength(2);

    if (numNodesX*numNodesY*numNodesZ != nodes.Length) { return; }

    nodes = new TerrainGridNode[numNodesX*numNodesY*numNodesZ];
    for (int x = 0; x < numNodesX; x++) {
      for (int y = 0; y < numNodesY; y++) {
        for (int z = 0; z < numNodesZ; z++) {
          nodes[node3DIndexToFlatIndex(x,y,z,numNodesY,numNodesZ)] = _nodes[x,y,z];
        }
      }
    }
  }

  public TerrainGridNode[,,] getNodesAs3DArray() {
    if (nodes == null) { return null; }

    var numNodesX = sizeToNumNodes(xSize);
    var numNodesY = sizeToNumNodes(ySize);
    var numNodesZ = sizeToNumNodes(zSize);

    TerrainGridNode[,,] result = null;
    if (nodes != null) {
      result = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];
      for (int x = 0; x < numNodesX; x++) {
        for (int y = 0; y < numNodesY; y++) {
          for (int z = 0; z < numNodesZ; z++) {
            var idx = node3DIndexToFlatIndex(x,y,z,numNodesY,numNodesZ);
            if (idx < nodes.GetLength(0)) { result[x,y,z] = nodes[idx]; }
          }
        }
      }
    }

    return result;
  }

}
