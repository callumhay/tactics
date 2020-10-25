using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TerrainGridToolWindow : EditorWindow {
  public enum PaintType { IsoValues = 0 };
  public enum BrushType { Sphere = 0, Cube = 1 };

  public PaintType paintType { get; protected set; } = PaintType.IsoValues;
  public BrushType brushType { get; protected set; } = BrushType.Sphere;
  public float brushSize { get; protected set; } = 0.1f;

  [MenuItem("Window/Terrain Grid Tool")]
  private static void ShowWindow() {
    var window = GetWindow<TerrainGridToolWindow>();
    window.titleContent = new GUIContent("Terrain Grid Tool Settings");
    window.Show();
  }

  private void OnGUI() {
    paintType = (PaintType)EditorGUILayout.EnumPopup("Paint Type:", paintType);
    brushType = (BrushType)EditorGUILayout.EnumPopup("Brush Type:", brushType);
    brushSize = EditorGUILayout.Slider("Brush Size:", brushSize, 0.25f, 5.0f);
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    switch (brushType) {
      case TerrainGridToolWindow.BrushType.Sphere:
        nodes = terrainGrid.GetNodesInsideSphere(editPt, brushSize/2);
        break;
      case TerrainGridToolWindow.BrushType.Cube:
        nodes = terrainGrid.GetNodesInsideBox(new Bounds(editPt, new Vector3(brushSize, brushSize, brushSize)));
        break;
      default:
        break;
    }
    return nodes;
  }

  public void paintNodes(in TerrainGrid terrainGrid, in List<TerrainGridNode> nodes) {
    switch (paintType) {
      case PaintType.IsoValues:
        terrainGrid.addIsoValuesToNodes(1, nodes);
        break;
      default:
        break;
    }
  }
  public void eraseNodes(in TerrainGrid terrainGrid, in List<TerrainGridNode> nodes) {
    switch (paintType) {
      case PaintType.IsoValues:
        terrainGrid.addIsoValuesToNodes(-1, nodes);
        break;
      default:
        break;
    }
  }
}