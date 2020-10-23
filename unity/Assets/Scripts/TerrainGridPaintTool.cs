using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

[EditorTool("Terrain Grid Painter", typeof(TerrainGrid))]
public class TerrainGridTool : EditorTool {
  
  private GUIContent _toolbarIcon;
  public override GUIContent toolbarIcon {
    get { 
      if (_toolbarIcon == null) {
        _toolbarIcon = new GUIContent(
          AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/TerrainGridPaintIcon.png"), 
          "Terrain Grid Painter Tool"
        );
      }
      return _toolbarIcon;
    }
  }

  private static int NO_MOUSE_BUTTON = -1;
  private int mouseDownButton = NO_MOUSE_BUTTON;
  private bool editPtActive = false;
  private Vector3 lastEditPt = new Vector3();

  public override void OnToolGUI(EditorWindow window) {
    if (!SceneView.sceneViews.Contains(window)) { return; }
    // Take control of the mouse so that we can properly paint - this MUST be called first!
    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

    var terrainGrid = target as TerrainGrid;
    if (target == null) { return; }

    var settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
    if (settingsWindow == null) { return; }

    var e = Event.current;
    if (!e.isMouse) { return; }

    var wsRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
    Vector3 editPt;
    if (terrainGrid.intersectEditorRay(wsRay, out editPt)) {
      editPtActive = true;
      lastEditPt = editPt;
    }
    else {
      editPtActive = false;
    }

    if (editPtActive) {
      
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
      if (mouseDownButton != NO_MOUSE_BUTTON) {
        // Grab all the nodes inside the brush
        List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(editPt, terrainGrid);
        if (nodes.Count == 0) { return; }

        EditorGUI.BeginChangeCheck();
        switch (mouseDownButton) {
          case 0: // Left Click: Paint
            settingsWindow.paintNodes(terrainGrid, nodes);
            break;

          case 1: // Right Click: Erase
            settingsWindow.eraseNodes(terrainGrid, nodes);
            break;

          case 2: // Middle Click
            break;
          default: // Ignore
            break;
        }
        if (EditorGUI.EndChangeCheck()) {}
      }
    }
  }

  private void OnEnable() {
    EditorTools.activeToolChanged += activeToolChanged;
    SceneView.duringSceneGui += this.onSceneGUI;
  }
  private void OnDisable() {
    EditorTools.activeToolChanged -= activeToolChanged;
    SceneView.duringSceneGui -= this.onSceneGUI;
  }

  void onSceneGUI(SceneView sceneView) {
    var terrainGrid = target as TerrainGrid;
    if (target == null) { return; }

    var settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
    if (settingsWindow == null) { return; }

    //Handles.BeginGUI();
    if (editPtActive) {
      // Draw a sphere in the painting area
      Handles.color = new Color(1,0,0,0.25f);
      switch (settingsWindow.brushType) {
        case TerrainGridToolWindow.BrushType.Sphere:
          Handles.SphereHandleCap(0, lastEditPt, new Quaternion(0,0,0,1), settingsWindow.brushSize, EventType.Repaint);
          break;
        case TerrainGridToolWindow.BrushType.Cube:
          Handles.CubeHandleCap(0, lastEditPt, new Quaternion(0,0,0,1), settingsWindow.brushSize, EventType.Repaint);
          break;
        default:
          return;
      }

      
      
      // Draw all the nodes that the sphere is colliding with / affecting

      sceneView.Repaint();
    }
    //Handles.EndGUI();
  }
  private void activeToolChanged() {
    if (!EditorTools.IsActiveTool(this)) { return; }
  }
}
