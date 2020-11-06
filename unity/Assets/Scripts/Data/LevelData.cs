using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[PreferBinarySerialization]
[CreateAssetMenu(fileName="New Level", menuName="Tactics/LevelData")]
public class LevelData : ScriptableObject {
  public static string emptyLevelAssetPath = "Assets/Resources/Levels/Empty Level.asset";

  public string levelName;
  [Range(1,32)]
  public int xSize = 10, ySize = 10, zSize = 10; // Size in units (not nodes!) of the level
  public TerrainGridNode[] nodes;

  private void OnValidate() {
    var terrainGrid = FindObjectOfType<TerrainGrid>();
    if (terrainGrid != null) {
      terrainGrid.OnValidate();
    }
  }


  public static int node3DIndexToFlatIndex(int x, int y, int z, int numNodesX, int numNodesY) {
    return z + (y*numNodesX) + (x*numNodesX*numNodesY);
  }

  public void setNodesFrom3DArray(in TerrainGridNode[,,] _nodes) {
    if (_nodes == null) { return; }

    var numNodesX = _nodes.GetLength(0);
    var numNodesY = _nodes.GetLength(1);
    var numNodesZ = _nodes.GetLength(2);

    nodes = new TerrainGridNode[numNodesX*numNodesY*numNodesZ];
    for (int x = 0; x < numNodesX; x++) {
      for (int y = 0; y < numNodesY; y++) {
        for (int z = 0; z < numNodesZ; z++) {
          nodes[node3DIndexToFlatIndex(x,y,z,numNodesX,numNodesY)] = _nodes[x,y,z];
        }
      }
    }
  }

  public TerrainGridNode[,,] getNodesAs3DArray(int numNodesX, int numNodesY, int numNodesZ) {
    if (nodes == null) { return null; }
    if (numNodesX*numNodesY*numNodesZ > nodes.GetLength(0)) {
      Debug.LogWarning("Mismatch between number of nodes to generate and the number stored in LevelData.");
      return null;
    }

    TerrainGridNode[,,] result = null;
    if (nodes != null) {
      result = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];
      for (int x = 0; x < numNodesX; x++) {
        for (int y = 0; y < numNodesY; y++) {
          for (int z = 0; z < numNodesZ; z++) {
            var idx = node3DIndexToFlatIndex(x,y,z,numNodesX,numNodesY);
            result[x,y,z] = nodes[idx];
          }
        }
      }
    }

    return result;
  }

}


