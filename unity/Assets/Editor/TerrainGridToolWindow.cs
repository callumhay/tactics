﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class TerrainGridToolWindow : EditorWindow {
  public static string windowTitle = "Terrain Grid Tool";

  private static TGTWSettings settings;

  public CharacterTeamData PlacementTeam { get {
    CharacterTeamData teamData = null;
    if (settings.selectedTeamIdx < settings.loadedTeams.Count()) { 
      teamData = settings.loadedTeams[settings.selectedTeamIdx];
    }
    return teamData;
  }}
  public CharacterData PlacementCharacterData { get { return settings.placementCharacterData; } }
  public TGTWSettings.EditorType EditorType { get { return settings.editorType; } }
  public TGTWSettings.PaintType PaintType { get { return settings.paintType; } }
  public TGTWSettings.PaintMode PaintMode { get { return settings.paintMode; } }
  public TGTWSettings.BrushType BrushType { get { return settings.brushType; } }
  public Material PaintMaterial { get { return settings.paintMaterial; } }
  public float BrushSize { get { return settings.brushSize; } }
  public float MaterialPaintIntensity { get { return settings.matPaintIntensity; } }
  public bool GridSnaping { get { return settings.gridSnaping; } }
  public bool ShowGridOverlay { get { return settings.showGridOverlay; } }
  public bool GroundUpOnly { get { return settings.groundUpOnly; } }
  public float SetLevelValue { get { return settings.setLevelValue; } }
  public int ColumnInsetXAmount { 
    get { return settings.columnInsetXAmount; }
    set { updateSettingsIntValue("columnInsetXAmount", value); }
   }
  public int ColumnInsetNegXAmount { 
    get { return settings.columnInsetNegXAmount; } 
    set { updateSettingsIntValue("columnInsetNegXAmount", value); }
  }
  public int ColumnInsetZAmount { 
    get { return settings.columnInsetZAmount; } 
    set { updateSettingsIntValue("columnInsetZAmount", value); }
  }
  public int ColumnInsetNegZAmount { 
    get { return settings.columnInsetNegZAmount; } 
    set { updateSettingsIntValue("columnInsetNegZAmount", value); }
  }
  public bool ShowTerrainNodes { get { return settings.showTerrainNodes; } }
  public bool ShowEmptyNodes { get { return settings.showEmptyNodes; } }
  public bool ShowSurfaceNodes { get { return settings.showSurfaceNodes; } }
  public bool ShowAboveSurfaceNodes { get { return settings.showAboveSurfaceNodes; } }

  [InitializeOnLoadMethod]
  private static void OnLoad() {
    if (!settings) {
      settings = AssetHelper.LoadOrCreateScriptableObjectFromPath<TGTWSettings>(TGTWSettings.assetPath);
    }
  }

  public static int maxInset() { return (TerrainGrid.NODES_PER_UNIT-1)/2 - 1; }
  private void updateSettingsIntValue(string settingName, int value) {
      var serializedObj = new SerializedObject(settings);
      serializedObj.Update();
      serializedObj.FindProperty(settingName).intValue = value;
      serializedObj.ApplyModifiedProperties();
      Repaint();
  }

  [MenuItem("Window/Terrain Grid Tool")]
  static void Open() {
    var window = GetWindow<TerrainGridToolWindow>();
    window.titleContent = new GUIContent(windowTitle);
    window.Show();
  }

  private void OnGUI() {
    var terrainGrid = TerrainGrid.FindTerrainGrid();
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
    var prevPlacementEditToggled = editorTypeEnumVal == TGTWSettings.EditorType.PlacementEditor;

    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    var freePaintEditToggled = GUILayout.Toggle(prevFreePaintEditToggled, "Paint", "button", GUILayout.ExpandWidth(false));
    var columnEditToggled    = GUILayout.Toggle(prevColumnEditToggled, "Column Edit", "button", GUILayout.ExpandWidth(false));
    var nodeEditToggled      = GUILayout.Toggle(prevNodeEditToggled, "Node Edit", "button", GUILayout.ExpandWidth(false));
    var placementEditToggled = GUILayout.Toggle(prevPlacementEditToggled, "Placement Edit", "button", GUILayout.ExpandWidth(false));
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
    else if (placementEditToggled != prevPlacementEditToggled) {
      editorTypeProp.intValue = (int)TGTWSettings.EditorType.PlacementEditor;
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
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.YUnitSize(), "Set Level", GUILayout.ExpandWidth(true));
        
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
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.YUnitSize(), "Set Level", GUILayout.ExpandWidth(true));
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
        EditorGUILayout.Slider(setLevelValProp, 1.0f, terrainGrid.YUnitSize(), "Set Level", GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(paintMatProp);

        break;
      } 

      case TGTWSettings.EditorType.PlacementEditor: {
        var placementCharacterProp = serializedObj.FindProperty("placementCharacterData");
        var selectedTeamIdxProp = serializedObj.FindProperty("selectedTeamIdx");
        var loadedTeamsProp = serializedObj.FindProperty("loadedTeams");

        var teamSet = new HashSet<CharacterTeamData>();
        foreach (var placement in terrainGrid.levelData.Placements) {
          teamSet.Add(placement.Team);
        }
        teamSet.UnionWith(settings.loadedTeams);
        settings.loadedTeams = teamSet.ToList();

        var teamNames = TeamNames();
        if (teamNames != null && teamNames.Count() > 0) {
          selectedTeamIdxProp.intValue = EditorGUILayout.Popup(
            "Placement Team", selectedTeamIdxProp.intValue, TeamNames(), GUILayout.ExpandWidth(true)
          );
        }

        EditorGUILayout.PropertyField(placementCharacterProp, GUILayout.ExpandWidth(true));

        var numPlayerPlacements = terrainGrid.levelData.GetPlayerControlledPlacements().Count;
        terrainGrid.levelData.MaxPlayerPlacements = EditorGUILayout.IntSlider(
          "Max Player Placements", terrainGrid.levelData.MaxPlayerPlacements, 1, numPlayerPlacements
        );
        
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(loadedTeamsProp, GUILayout.ExpandWidth(true));

        break;
      }

      default:
        break;
    }

    if (editorTypeEnumVal != TGTWSettings.EditorType.PlacementEditor) {
      EditorGUILayout.Space();
      if (GUILayout.Button(new GUIContent(){text = "Fill with Core Material", tooltip = "Paint core materials into all terrain interiors."})) {
        terrainGrid.fillCoreMaterial();
      }
    }

    serializedObj.ApplyModifiedProperties();
  }

  private string[] TeamNames() {
    var teamNames = new string[settings.loadedTeams.Count];
    for (int i = 0; i < settings.loadedTeams.Count; i++) {
      var currTeam = settings.loadedTeams[i];
      if (currTeam) {
        teamNames[i] = settings.loadedTeams[i].name;
      }
      else {
        return null;
      }
    }
    return teamNames;
  }

  public List<TerrainGridNode> getAffectedNodesAtPoint(in Vector3 editPt, in TerrainGrid terrainGrid) {
    List<TerrainGridNode> nodes = null;
    var paintMode3D = (PaintMode == TGTWSettings.PaintMode.Floating);
    var finalEditPt = editPt;
    if (GridSnaping) {
      terrainGrid?.GetGridSnappedPoint(ref finalEditPt);
    }

    bool groundFirst = GroundUpOnly && PaintType != TGTWSettings.PaintType.Water;
    bool waterFirst  = GroundUpOnly && PaintType == TGTWSettings.PaintType.Water;
    
    switch (BrushType) {
      case TGTWSettings.BrushType.Sphere:
        var radius = 0.5f * BrushSize;
        nodes = paintMode3D ? 
          terrainGrid?.GetNodesInsideSphere(finalEditPt, radius, groundFirst, waterFirst, SetLevelValue) : 
          terrainGrid?.GetNodesInsideProjXZCircle(finalEditPt, radius, groundFirst, waterFirst, SetLevelValue);
        break;
      case TGTWSettings.BrushType.Cube:
        var bounds = new Bounds(finalEditPt, new Vector3(BrushSize, BrushSize, BrushSize));
        nodes = paintMode3D ? 
          terrainGrid?.GetNodesInsideBox(bounds, groundFirst, waterFirst, SetLevelValue) : 
          terrainGrid?.GetNodesInsideProjXZSquare(bounds, groundFirst, waterFirst, SetLevelValue);
        break;
      default:
        break;
    }

    return nodes;
  }

  public void paintNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (PaintType) {
      case TGTWSettings.PaintType.Terrain:
        if (settings.paintMaterial) { terrainGrid?.AddIsoValuesAndMaterialToNodes(1f, MaterialPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.AddIsoValuesToNodes(1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintNodesWithMaterial(nodes, MaterialPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      case TGTWSettings.PaintType.Water:
        terrainGrid?.AddLiquidToNodes(1f, nodes);
        break;
      default:
        break;
    }
  }
  
  public void eraseNodes(in List<TerrainGridNode> nodes, in TerrainGrid terrainGrid) {
    switch (PaintType) {
      case TGTWSettings.PaintType.Terrain:
        if (settings.paintMaterial) { terrainGrid?.AddIsoValuesAndMaterialToNodes(-1f, -MaterialPaintIntensity, settings.paintMaterial, nodes); }
        else { terrainGrid?.AddIsoValuesToNodes(-1f, nodes); }
        break;
      case TGTWSettings.PaintType.MaterialsOnly:
        paintNodesWithMaterial(nodes, -MaterialPaintIntensity);
        terrainGrid?.updateNodesInEditor(nodes);
        break;
      case TGTWSettings.PaintType.Water:
        terrainGrid?.AddLiquidToNodes(-1f, nodes);
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