using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TerrainGridToolWindow : EditorWindow {
  public static string windowTitle = "Terrain Grid Tool";

  private static TGTWSettings settings;

  [InitializeOnLoadMethod]
  private static void OnLoad() {
    if (!settings) {
      settings = ScriptableObjectUtility.LoadOrCreateAssetFromPath<TGTWSettings>(TGTWSettings.assetPath);
    }
  }

  public TGTWSettings.PaintType paintType { get { return settings.paintType; } }
  public TGTWSettings.PaintMode paintMode { get { return settings.paintMode; } }
  public TGTWSettings.BrushType brushType { get { return settings.brushType; } }
  public float brushSize { get { return settings.brushSize; } }

  [MenuItem("Window/Terrain Grid Tool")]
  static void Open() {
    var window = GetWindow<TerrainGridToolWindow>();
    window.titleContent = new GUIContent(windowTitle);
    window.Show();
  }

  private void OnGUI() {
    var serializedObj = new SerializedObject(settings);
    serializedObj.Update();
    
    var paintTypeProp = serializedObj.FindProperty("paintType");
    var paintModeProp = serializedObj.FindProperty("paintMode");
    var brushTypeProp = serializedObj.FindProperty("brushType");
    var brushSizeProp = serializedObj.FindProperty("brushSize");
    
    EditorGUILayout.PropertyField(paintTypeProp);
    EditorGUILayout.PropertyField(paintModeProp);
    EditorGUILayout.PropertyField(brushTypeProp);

    brushSizeProp.floatValue = EditorGUILayout.Slider("Brush Size", brushSizeProp.floatValue, 0.25f, 5.0f);

    serializedObj.ApplyModifiedProperties();
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    var paintMode3D = (paintMode == TGTWSettings.PaintMode.Freeform_3D);
    
    switch (brushType) {
      case TGTWSettings.BrushType.Sphere:
        var radius = 0.5f * brushSize;
        nodes = paintMode3D ? terrainGrid?.getNodesInsideSphere(editPt, radius) : terrainGrid?.getNodesInsideProjXZCircle(editPt, radius);
        break;
      case TGTWSettings.BrushType.Cube:
        var bounds = new Bounds(editPt, new Vector3(brushSize, brushSize, brushSize));
        nodes = paintMode3D ? terrainGrid?.getNodesInsideBox(bounds) : terrainGrid?.getNodesInsideProjXZSquare(bounds);
        break;
      default:
        break;
    }

    return nodes;
  }

  public void paintNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case TGTWSettings.PaintType.IsoValues:
        terrainGrid?.addIsoValuesToNodes(1f, nodes);
        break;
      default:
        break;
    }
  }
  public void eraseNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case TGTWSettings.PaintType.IsoValues:
        terrainGrid?.addIsoValuesToNodes(-1, nodes);
        break;
      default:
        break;
    }
  }
}