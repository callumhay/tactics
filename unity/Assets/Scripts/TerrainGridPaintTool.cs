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
    // Take control of the mouse so that we can properly paint - this MUST be called first!
    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

    var terrainGrid = target as TerrainGrid;
    if (target == null) { editPtActive = false; return; }

    var settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
    if (settingsWindow == null) { editPtActive = false; return; }

    var e = Event.current;
    var wsRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
    if (terrainGrid.intersectEditorRay(wsRay, out lastEditPt)) {
      editPtActive = true;
    }
    else {
      editPtActive = false;
    }

    if (editPtActive) {
      if (e.type == EventType.MouseDown && e.modifiers == EventModifiers.None) {
        mouseDownButton = e.button;
        // Grab all the nodes inside the brush
        List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(lastEditPt, terrainGrid);
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
      else {
        mouseDownButton = NO_MOUSE_BUTTON;
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

    if (editPtActive) {
      var rot = new Quaternion(0,0,0,1);
      // Draw the brush shape
      Handles.color = new Color(0.0f, 0.8f, 1.0f, 0.25f);
      switch (settingsWindow.brushType) {
        case TerrainGridToolWindow.BrushType.Sphere:
          Handles.SphereHandleCap(GUIUtility.GetControlID(FocusType.Passive), lastEditPt, rot, settingsWindow.brushSize, EventType.Repaint);
          break;
        case TerrainGridToolWindow.BrushType.Cube:
          Handles.CubeHandleCap(GUIUtility.GetControlID(FocusType.Passive), lastEditPt, rot, settingsWindow.brushSize, EventType.Repaint);
          break;
        default:
          return;
      }

      // Draw all the nodes that the tool is colliding with / affecting
      List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(lastEditPt, terrainGrid);
      foreach (var node in nodes) {
        var currColour = node.editorUnselectedColour();
        currColour.a = 0.25f;
        Handles.color = currColour;
        Handles.CubeHandleCap(GUIUtility.GetControlID(FocusType.Passive), node.position, rot, terrainGrid.halfUnitsPerNode(), EventType.Repaint);
      }

      sceneView.Repaint();
    }
    
  }
  private void activeToolChanged() {
    if (!EditorTools.IsActiveTool(this)) { return; }
  }
}
