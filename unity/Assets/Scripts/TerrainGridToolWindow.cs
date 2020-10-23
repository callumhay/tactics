using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TerrainGridToolWindow : EditorWindow {
  public enum PaintType { IsoValues = 0 };
  public enum BrushType { Sphere = 0, Cube = 1 };

  public PaintType paintType { get; protected set; } = PaintType.IsoValues;
  public BrushType brushType { get; protected set; } = BrushType.Sphere;
  public float brushSize { get; protected set; } = 0.1f;
  public float brushIntensity { get; protected set; } = 0.5f;

  [MenuItem("Window/Terrain Grid Tool")]
  private static void ShowWindow() {
    var window = GetWindow<TerrainGridToolWindow>();
    window.titleContent = new GUIContent("Terrain Grid Tool Settings");
    window.Show();
  }

  private void OnGUI() {
    paintType = (PaintType)EditorGUILayout.EnumPopup("Paint Type:", paintType);
    brushType = (BrushType)EditorGUILayout.EnumPopup("Brush Type:", brushType);
    brushSize = EditorGUILayout.Slider("Brush Size:", brushSize, 0.01f, 5.0f);
    brushIntensity = EditorGUILayout.Slider("Brush Intensity:", brushIntensity, 0.01f, 1.0f);
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    switch (brushType) {
      case TerrainGridToolWindow.BrushType.Sphere:
        nodes = terrainGrid.GetNodesInsideSphere(editPt, brushSize);
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
        terrainGrid.addIsoValuesToNodes(Time.deltaTime*brushIntensity, nodes);
        break;
      default:
        break;
    }
  }
  public void eraseNodes(in TerrainGrid terrainGrid, in List<TerrainGridNode> nodes) {
    switch (paintType) {
      case PaintType.IsoValues:
        terrainGrid.addIsoValuesToNodes(-Time.deltaTime*brushIntensity, nodes);
        break;
      default:
        break;
    }
  }
}

/*
private static int NO_MOUSE_BUTTON = -1;

public override void OnInspectorGUI() {
  base.OnInspectorGUI();
  paintButtonOn = EditorGUILayout.ToggleLeft("Paint", paintButtonOn);
  brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.1f, 2.0f);

  var terrainGrid = target as TerrainGrid;
  if (terrainGrid == null) { return; }

}

private void OnSceneGUI() {
  // Check what the current GameObject is, make sure we're editing the TerrainGrid
  var terrainGrid = target as TerrainGrid;
  if (terrainGrid == null) { return; }
  
  // Mouse events only
  var e = Event.current;
  if (!e.isMouse) { return; }

  // Left mouse button
  switch (e.type) {
    case EventType.MouseDown:
      mouseDownButton = e.button;
      break;
    case EventType.MouseUp:
      mouseDownButton = NO_MOUSE_BUTTON;
      break;
    default:
      break;
  }
  //if (mouseDownButton != NO_MOUSE_BUTTON) {
  //  Debug.Log("Mouse Button: " + mouseDownButton);
  //}
}
*/