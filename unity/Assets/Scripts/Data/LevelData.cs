using System;
using UnityEngine;
using UnityEditor;

[PreferBinarySerialization]
[CreateAssetMenu(fileName="New Level", menuName="Tactics/Level")]
public class LevelData : ScriptableObject {
  public static string emptyLevelAssetPath = "Assets/Resources/Levels/Empty Level.asset";

  public string levelName;
  [Range(1,32)]
  public int xSize = 10, ySize = 10, zSize = 10; // Size in units (not nodes!) of the level
  public TerrainGridNode[] nodes;

  private void OnEnable() {
  }

  private void OnValidate() {
    var terrainGrid = FindObjectOfType<TerrainGrid>();
    if (terrainGrid != null) {
      terrainGrid.OnValidate();
    }
  }

  public static int node3DIndexToFlatIndex(int x, int y, int z, int numNodesX, int numNodesY) {
    return z + (y*numNodesX) + (x*numNodesX*numNodesY);
  }
  public static int numNodesToSize(int numNodes) {
    return (numNodes - 1)/(TerrainGrid.nodesPerUnit*TerrainColumn.size - 1);
  }
  
  /// <summary>
  /// Convert a size in terrain columns to the total number of nodes spanning that size.
  /// </summary>
  /// <param name="size">Number of terrain columns.</param>
  /// <returns>Number of nodes spanning the given number of terrain columns.</returns>
  public static int sizeToNumNodes(int size) {
    return (size*TerrainGrid.nodesPerUnit*TerrainColumn.size) + 1 - size;
  }

  public void setNodesFrom3DArray(in TerrainGridNode[,,] _nodes) {
    if (_nodes == null) { return; }

    var numNodesX = _nodes.GetLength(0);
    var numNodesY = _nodes.GetLength(1);
    var numNodesZ = _nodes.GetLength(2);

    if (numNodesX*numNodesY*numNodesZ != nodes.GetLength(0)) { return; }

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

    var maxX = numNodesX;
    var maxY = numNodesY;
    var maxZ = numNodesZ;

    if (numNodesX*numNodesY*numNodesZ != nodes.GetLength(0)) {
      return null;
    //  maxX = Math.Min(maxX, sizeToNumNodes(oldXSize));
    //  maxY = Math.Min(maxY, sizeToNumNodes(oldYSize));
    //  maxZ = Math.Min(maxZ, sizeToNumNodes(oldZSize));
    }

    TerrainGridNode[,,] result = null;
    if (nodes != null) {
      result = new TerrainGridNode[numNodesX,numNodesY,numNodesZ];
      for (int x = 0; x < maxX; x++) {
        for (int y = 0; y < maxY; y++) {
          for (int z = 0; z < maxZ; z++) {
            var idx = node3DIndexToFlatIndex(x,y,z,maxX,maxY);
            if (idx < nodes.GetLength(0)) { result[x,y,z] = nodes[idx]; }
          }
        }
      }
    }

    return result;
  }

}


/*
[CustomEditor(typeof(LevelData))]
class LevelDataEditor : Editor {
  public override void OnInspectorGUI() {
    //base.OnInspectorGUI();

    levelData.levelName = EditorGUILayout.TextField("Level Name", levelData.levelName);

    int newXSize = EditorGUILayout.IntSlider("X Size", levelData.xSize, 1, 32);
    if (newXSize != levelData.xSize) { 
      levelData.oldXSize = levelData.xSize;
      levelData.xSize = newXSize;
    }
    int newYSize = EditorGUILayout.IntSlider("Y Size", levelData.ySize, 1, 32);
    if (newYSize != levelData.ySize) { 
      levelData.oldYSize = levelData.ySize;
      levelData.ySize = newYSize;
    }
    int newZSize = EditorGUILayout.IntSlider("Z Size", levelData.zSize, 1, 32);
    if (newZSize != levelData.zSize) { 
      levelData.oldZSize = levelData.zSize;
      levelData.zSize = newZSize;
    }

    EditorUtility.SetDirty(levelData);
    AssetDatabase.SaveAssets();
    
  }
}
*/
