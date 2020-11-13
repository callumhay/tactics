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
  public float matPaintIntensity { get { return settings.matPaintIntensity; } }
  public bool gridSnaping { get { return settings.gridSnaping; } }

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
    var matIntensityProp = serializedObj.FindProperty("matPaintIntensity");
    var gridSnapProp  = serializedObj.FindProperty("gridSnaping");
    var paintMatProp = serializedObj.FindProperty("paintMaterial");

    EditorGUILayout.PropertyField(paintTypeProp);
    EditorGUILayout.PropertyField(paintModeProp);
    EditorGUILayout.PropertyField(brushTypeProp);
    brushSizeProp.floatValue = EditorGUILayout.Slider("Brush Size", brushSizeProp.floatValue, 0.25f, 5.0f);
    EditorGUILayout.PropertyField(gridSnapProp);
    matIntensityProp.floatValue = EditorGUILayout.Slider("Material Intensity", matIntensityProp.floatValue, 0.01f, 1.0f);
    EditorGUILayout.PropertyField(paintMatProp);
    EditorGUILayout.Space();
    if (GUILayout.Button(new GUIContent(){text = "Fill with Core Material", tooltip = "Paint core materials into all terrain interiors."})) {
      var terrainGameObj = GameObject.Find("Terrain");
      if (!terrainGameObj) { terrainGameObj = GameObject.FindWithTag("Terrain"); }
      if (terrainGameObj) {
        var terrainGrid = terrainGameObj.GetComponent<TerrainGrid>();
        if (terrainGrid) {
          terrainGrid.fillCoreMaterial();
        }
        else {
          Debug.LogWarning("Could not find component 'TerrainGrid' on found 'Terrain' GameObject.");
        }
      }
      else {
        Debug.LogWarning("Could not find GameObject named 'Terrain' or with the 'Terrain' tag.");
      }
    }


    serializedObj.ApplyModifiedProperties();
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    var paintMode3D = (paintMode == TGTWSettings.PaintMode.Floating);
    var snappedPt = editPt;
    if (gridSnaping) {
      terrainGrid?.getGridSnappedPoint(ref snappedPt);
    }
    
    switch (brushType) {
      case TGTWSettings.BrushType.Sphere:
        var radius = 0.5f * brushSize;
        nodes = paintMode3D ? terrainGrid?.getNodesInsideSphere(snappedPt, radius) : terrainGrid?.getNodesInsideProjXZCircle(snappedPt, radius);
        break;
      case TGTWSettings.BrushType.Cube:
        var bounds = new Bounds(snappedPt, new Vector3(brushSize, brushSize, brushSize));
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
        if (settings.paintMaterial) { terrainGrid?.addIsoValuesAndMaterialToNodes(1f, matPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.addIsoValuesToNodes(1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintMaterial(nodes, matPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      default:
        break;
    }
  }
  
  public void eraseNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case TGTWSettings.PaintType.IsoValues:
        if (settings.paintMaterial) { terrainGrid?.addIsoValuesAndMaterialToNodes(-1f, -matPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.addIsoValuesToNodes(-1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintMaterial(nodes, -matPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      default:
        break;
    }
  }

  private void paintMaterial(in List<TerrainGridNode> nodes, float paintAmount) {
    if (!settings.paintMaterial) { return; }

    var clampedPaintAmt = Mathf.Clamp(paintAmount, 0f, 1f);    
    foreach (var node in nodes) { 
      bool foundMat = false;
      foreach (var matContrib in node.materials) {
        if (matContrib.material == settings.paintMaterial) {
          matContrib.contribution = Mathf.Clamp(matContrib.contribution + paintAmount, 0.0f, 1.0f);
          foundMat = true;
          break;
        }
      }
      if (!foundMat) {
        if (node.materials.Count >= TerrainGridNode.maxMaterialsPerNode) {
          Debug.LogWarning("A node already has the maximum number of materials, erase those materials first.");
        }
        else {
          node.materials.Add(new NodeMaterialContrib(settings.paintMaterial, clampedPaintAmt));
        }
      }
    }
  }

}