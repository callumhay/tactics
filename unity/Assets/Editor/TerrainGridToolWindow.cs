using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TGTWSettings : ScriptableObject {
  public static string assetPath = "Assets/Editor/Settings/TGTWSettings.asset";
  public float brushSize;
  public TerrainGridToolWindow.PaintType paintType;
  public TerrainGridToolWindow.BrushType brushType;
}

public class TerrainGridToolWindow : EditorWindow {
  public static string windowTitle = "Terrain Grid Tool";

  private static TGTWSettings settings;

  [InitializeOnLoadMethod]
  private static void OnLoad() {
    if (!settings) {
      settings = AssetDatabase.LoadAssetAtPath<TGTWSettings>(TGTWSettings.assetPath);
      if (settings) { return; }
      settings = CreateInstance<TGTWSettings>();
      AssetDatabase.CreateAsset(settings, TGTWSettings.assetPath);
      AssetDatabase.Refresh();
    }
  }

  public enum PaintType { IsoValues = 0 };
  public enum BrushType { Sphere = 0, Cube = 1 };

  public PaintType paintType { get { return settings.paintType; } }
  public BrushType brushType { get { return settings.brushType; } }
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
      case TerrainGridToolWindow.BrushType.Sphere:
        nodes = terrainGrid?.getNodesInsideSphere(editPt, brushSize/2);
        break;
      case TerrainGridToolWindow.BrushType.Cube:
        nodes = terrainGrid?.getNodesInsideBox(new Bounds(editPt, new Vector3(brushSize, brushSize, brushSize)));
        break;
      default:
        break;
    }
    return nodes;
  }

  public void paintNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case PaintType.IsoValues:
        terrainGrid?.addIsoValuesToNodes(1f, nodes);
        break;
      default:
        break;
    }
  }
  public void eraseNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case PaintType.IsoValues:
        terrainGrid?.addIsoValuesToNodes(-1, nodes);
        break;
      default:
        break;
    }
  }
}