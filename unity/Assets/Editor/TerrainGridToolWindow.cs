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
    var brushTypeProp = serializedObj.FindProperty("brushType");
    var brushSizeProp = serializedObj.FindProperty("brushSize");
    
    EditorGUILayout.PropertyField(paintTypeProp);
    EditorGUILayout.PropertyField(brushTypeProp);
    brushSizeProp.floatValue = EditorGUILayout.Slider("Brush Size", brushSizeProp.floatValue, 0.25f, 5.0f);

    serializedObj.ApplyModifiedProperties();
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    switch (brushType) {
      case TGTWSettings.BrushType.Sphere:
        nodes = terrainGrid?.getNodesInsideSphere(editPt, brushSize/2);
        break;
      case TGTWSettings.BrushType.Cube:
        nodes = terrainGrid?.getNodesInsideBox(new Bounds(editPt, new Vector3(brushSize, brushSize, brushSize)));
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