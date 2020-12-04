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

  public static int maxInset() { return (TerrainGrid.nodesPerUnit-1)/2 - 1; }
  private void updateSettingsIntValue(string settingName, int value) {
      var serializedObj = new SerializedObject(settings);
      serializedObj.Update();
      serializedObj.FindProperty(settingName).intValue = value;
      serializedObj.ApplyModifiedProperties();
      Repaint();
  }

  public TGTWSettings.EditorType editorType { get { return settings.editorType; } }
  public TGTWSettings.PaintType paintType { get { return settings.paintType; } }
  public TGTWSettings.PaintMode paintMode { get { return settings.paintMode; } }
  public TGTWSettings.BrushType brushType { get { return settings.brushType; } }
  public Material paintMaterial { get { return settings.paintMaterial; } }
  public float brushSize { get { return settings.brushSize; } }
  public float matPaintIntensity { get { return settings.matPaintIntensity; } }
  public bool gridSnaping { get { return settings.gridSnaping; } }
  public bool showGridOverlay { get { return settings.showGridOverlay; } }
  public bool groundUpOnly { get { return settings.groundUpOnly; } }
  public float setLevelValue { get { return settings.setLevelValue; } }
  public int columnInsetXAmount { 
    get { return settings.columnInsetXAmount; }
    set { updateSettingsIntValue("columnInsetXAmount", value); }
   }
  public int columnInsetNegXAmount { 
    get { return settings.columnInsetNegXAmount; } 
    set { updateSettingsIntValue("columnInsetNegXAmount", value); }
  }
  public int columnInsetZAmount { 
    get { return settings.columnInsetZAmount; } 
    set { updateSettingsIntValue("columnInsetZAmount", value); }
  }
  public int columnInsetNegZAmount { 
    get { return settings.columnInsetNegZAmount; } 
    set { updateSettingsIntValue("columnInsetNegZAmount", value); }
  }

  public bool showTerrainNodes { get { return settings.showTerrainNodes; } }
  public bool showEmptyNodes { get { return settings.showEmptyNodes; } }
  public bool showSurfaceNodes { get { return settings.showSurfaceNodes; } }
  public bool showAboveSurfaceNodes { get { return settings.showAboveSurfaceNodes; } }

  [MenuItem("Window/Terrain Grid Tool")]
  static void Open() {
    var window = GetWindow<TerrainGridToolWindow>();
    window.titleContent = new GUIContent(windowTitle);
    window.Show();
  }

  private void OnGUI() {
    var terrainGrid = TerrainGridToolWindow.findTerrainGrid();
    if (!terrainGrid) {
      EditorGUILayout.LabelField("Could not find TerrainGrid!");
      return;
    }

    var serializedObj = new SerializedObject(settings);
    serializedObj.Update();

    var showGridProp = serializedObj.FindProperty("showGridOverlay");
    var editorTypeProp = serializedObj.FindProperty("editorType");
    var editorTypeEnumVal = (TGTWSettings.EditorType)editorTypeProp.intValue;
    var prevFreePaintEditToggled = editorTypeEnumVal == TGTWSettings.EditorType.FreePaintEditor;
    var prevColumnEditToggled    = editorTypeEnumVal == TGTWSettings.EditorType.ColumnEditor;
    var prevNodeEditToggled      = editorTypeEnumVal == TGTWSettings.EditorType.NodeEditor;

    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    var freePaintEditToggled = GUILayout.Toggle(prevFreePaintEditToggled, "Paint", "button", GUILayout.ExpandWidth(false));
    var columnEditToggled    = GUILayout.Toggle(prevColumnEditToggled, "Column Edit", "button", GUILayout.ExpandWidth(false));
    var nodeEditToggled      = GUILayout.Toggle(prevNodeEditToggled, "Node Edit", "button", GUILayout.ExpandWidth(false));
    GUILayout.FlexibleSpace();
    EditorGUILayout.PropertyField(showGridProp);
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.Space();

    if (freePaintEditToggled != prevFreePaintEditToggled) {
      editorTypeProp.intValue = (int)TGTWSettings.EditorType.FreePaintEditor;
    }
    else if (columnEditToggled != prevColumnEditToggled) {
      editorTypeProp.intValue = (int)TGTWSettings.EditorType.ColumnEditor;
    }
    else if (nodeEditToggled != prevNodeEditToggled) {
      editorTypeProp.intValue = (int)TGTWSettings.EditorType.NodeEditor;
    }
    editorTypeEnumVal = (TGTWSettings.EditorType)editorTypeProp.intValue;

    var setLevelValProp  = serializedObj.FindProperty("setLevelValue");
    var paintMatProp = serializedObj.FindProperty("paintMaterial");

    switch (editorTypeEnumVal) {
      
      case TGTWSettings.EditorType.FreePaintEditor: {
        var paintTypeProp = serializedObj.FindProperty("paintType");
        var paintModeProp = serializedObj.FindProperty("paintMode");
        var brushTypeProp = serializedObj.FindProperty("brushType");
        var brushSizeProp = serializedObj.FindProperty("brushSize");
        var matIntensityProp = serializedObj.FindProperty("matPaintIntensity");
        var gridSnapProp = serializedObj.FindProperty("gridSnaping");
        
        var groundUpOnlyProp = serializedObj.FindProperty("groundUpOnly");
        
        EditorGUILayout.PropertyField(paintTypeProp);
        EditorGUILayout.PropertyField(paintModeProp);
        EditorGUILayout.PropertyField(brushTypeProp);
        EditorGUILayout.Slider(brushSizeProp, 0.25f, 5.0f,  "Brush Size");
        EditorGUILayout.PropertyField(gridSnapProp);
        
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(groundUpOnlyProp);
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.ySize*TerrainColumn.size, "Set Level", GUILayout.ExpandWidth(true));
        
        EditorGUILayout.Space();
        EditorGUILayout.Slider(matIntensityProp, 0.01f, 1.0f, "Material Intensity");
        EditorGUILayout.PropertyField(paintMatProp);
        break;
      }

      case TGTWSettings.EditorType.ColumnEditor: {
        var colInsetXAmtProp = serializedObj.FindProperty("columnInsetXAmount");
        var colInsetNegXAmtProp = serializedObj.FindProperty("columnInsetNegXAmount");
        var colInsetZAmtProp = serializedObj.FindProperty("columnInsetZAmount");
        var colInsetNegZAmtProp = serializedObj.FindProperty("columnInsetNegZAmount");

        int maxAllowableInset = maxInset();
        EditorGUILayout.IntSlider(colInsetXAmtProp, 0, maxAllowableInset, "Column Inset/Outset +X", GUILayout.ExpandWidth(true));
        EditorGUILayout.IntSlider(colInsetNegXAmtProp, 0, maxAllowableInset, "Column Inset/Outset -X", GUILayout.ExpandWidth(true));
        EditorGUILayout.IntSlider(colInsetZAmtProp, 0, maxAllowableInset, "Column Inset/Outset +Z", GUILayout.ExpandWidth(true));
        EditorGUILayout.IntSlider(colInsetNegZAmtProp, 0, maxAllowableInset, "Column Inset/Outset -Z", GUILayout.ExpandWidth(true));
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.ySize*TerrainColumn.size, "Set Level", GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(paintMatProp);
        break;
      }

      case TGTWSettings.EditorType.NodeEditor: {
        var showTerrainNodesProp = serializedObj.FindProperty("showTerrainNodes");
        var showEmptyNodesProp = serializedObj.FindProperty("showEmptyNodes");
        var showSurfaceNodesProp = serializedObj.FindProperty("showSurfaceNodes");
        var showAboveSurfaceNodesProp = serializedObj.FindProperty("showAboveSurfaceNodes");

        EditorGUILayout.PropertyField(showTerrainNodesProp, GUILayout.ExpandWidth(true));
        EditorGUILayout.PropertyField(showEmptyNodesProp, GUILayout.ExpandWidth(true));
        EditorGUILayout.PropertyField(showSurfaceNodesProp, GUILayout.ExpandWidth(true));
        EditorGUILayout.PropertyField(showAboveSurfaceNodesProp, GUILayout.ExpandWidth(true));
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.ySize*TerrainColumn.size, "Set Level", GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(paintMatProp);

        break;
      } 

      default:
        break;
    }

    EditorGUILayout.Space();
    if (GUILayout.Button(new GUIContent(){text = "Fill with Core Material", tooltip = "Paint core materials into all terrain interiors."})) {
      terrainGrid.fillCoreMaterial();
    }

    serializedObj.ApplyModifiedProperties();
  }

  public static TerrainGrid findTerrainGrid() {
    var terrainGameObj = GameObject.Find("Terrain");
    if (!terrainGameObj) { terrainGameObj = GameObject.FindWithTag("Terrain"); }
    if (terrainGameObj) { return terrainGameObj.GetComponent<TerrainGrid>(); }
    return null;
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    var paintMode3D = (paintMode == TGTWSettings.PaintMode.Floating);
    var finalEditPt = editPt;
    if (gridSnaping) {
      terrainGrid?.getGridSnappedPoint(ref finalEditPt);
    }

    bool groundFirst = groundUpOnly && paintType != TGTWSettings.PaintType.Water;
    bool waterFirst  = groundUpOnly && paintType == TGTWSettings.PaintType.Water;
    
    switch (brushType) {
      case TGTWSettings.BrushType.Sphere:
        var radius = 0.5f * brushSize;
        nodes = paintMode3D ? 
          terrainGrid?.getNodesInsideSphere(finalEditPt, radius, groundFirst, waterFirst, setLevelValue) : 
          terrainGrid?.getNodesInsideProjXZCircle(finalEditPt, radius, groundFirst, waterFirst, setLevelValue);
        break;
      case TGTWSettings.BrushType.Cube:
        var bounds = new Bounds(finalEditPt, new Vector3(brushSize, brushSize, brushSize));
        nodes = paintMode3D ? 
          terrainGrid?.getNodesInsideBox(bounds, groundFirst, waterFirst, setLevelValue) : 
          terrainGrid?.getNodesInsideProjXZSquare(bounds, groundFirst, waterFirst, setLevelValue);
        break;
      default:
        break;
    }

    return nodes;
  }

  public void paintNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case TGTWSettings.PaintType.Terrain:
        if (settings.paintMaterial) { terrainGrid?.addIsoValuesAndMaterialToNodes(1f, matPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.addIsoValuesToNodes(1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintNodesWithMaterial(nodes, matPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      case TGTWSettings.PaintType.Water:
        terrainGrid?.addLiquidToNodes(1f, nodes);
        break;
      default:
        break;
    }
  }
  
  public void eraseNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (paintType) {
      case TGTWSettings.PaintType.Terrain:
        if (settings.paintMaterial) { terrainGrid?.addIsoValuesAndMaterialToNodes(-1f, -matPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.addIsoValuesToNodes(-1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintNodesWithMaterial(nodes, -matPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      case TGTWSettings.PaintType.Water:
        terrainGrid?.addLiquidToNodes(-1f, nodes);
        break;
      default:
        break;
    }
  }

  private void paintNodesWithMaterial(in List<TerrainGridNode> nodes, float paintAmount) {
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
        if (node.materials.Count >= TerrainGridNode.MAX_MATERIALS_PER_NODE) {
          Debug.LogWarning("A node already has the maximum number of materials, erase those materials first.");
        }
        else {
          node.materials.Add(new NodeMaterialContrib(settings.paintMaterial, clampedPaintAmt));
        }
      }
    }
  }

}