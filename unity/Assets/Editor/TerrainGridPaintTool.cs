using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
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
  private TerrainGridToolWindow settingsWindow;

  void Awake() {
    settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
  }

  public override void OnToolGUI(EditorWindow window) {

    var terrainGrid = target as TerrainGrid;
    if (!InternalEditorUtility.isApplicationActive || !terrainGrid || !settingsWindow) { 
      editPtActive = false;
      return;
    }

    // Take control of the mouse so that we can properly paint - this MUST be called first!
    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

    var e = Event.current;
    var wsRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
    var yOffset = settingsWindow.paintMode == TGTWSettings.PaintMode.Floating ? 0.5f*settingsWindow.brushSize : 0;
    if (terrainGrid.intersectEditorRay(wsRay, yOffset, out lastEditPt)) {
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
        if (nodes == null) { return; }

        EditorGUI.BeginChangeCheck();
        if (mouseDownButton == 0 || mouseDownButton == 1) {
          GUI.changed = true;
        }
        if (EditorGUI.EndChangeCheck()) {
          //Undo.RegisterCompleteObjectUndo(terrainGrid, "Terrain Grid Nodes Edited"); // Not working... no idea why.
          
          switch (mouseDownButton) {
            case 0: // Left Click: Paint
              settingsWindow.paintNodes(nodes, terrainGrid);
              break;
            case 1: // Right Click: Erase
              settingsWindow.eraseNodes(nodes, terrainGrid);
              break;
            case 2: // Middle Click
              break;
            default: // Ignore
              break;
          }
        }
      }
      else {
        mouseDownButton = NO_MOUSE_BUTTON;
      }
    }
  }

  private void OnEnable() {
    //EditorTools.activeToolChanged += activeToolChanged;
    SceneView.duringSceneGui += this.onSceneGUI;
  }
  private void OnDisable() {
    //EditorTools.activeToolChanged -= activeToolChanged;
    SceneView.duringSceneGui -= this.onSceneGUI;
  }

  void onSceneGUI(SceneView sceneView) {
    
    var terrainGrid = target as TerrainGrid;
    if (!InternalEditorUtility.isApplicationActive || !terrainGrid || !settingsWindow) { return; }

    if (editPtActive) {
      var rot = new Quaternion(0,0,0,1);
      /*
      if (settingsWindow.paintMode == TGTWSettings.PaintMode.Floating) {
        // Draw the brush shape
        Handles.color = new Color(0.0f, 0.8f, 1.0f, 0.25f);
        switch (settingsWindow.brushType) {
          case TGTWSettings.BrushType.Sphere:
            Handles.SphereHandleCap(GUIUtility.GetControlID(FocusType.Passive), lastEditPt, rot, settingsWindow.brushSize, EventType.Repaint);
            break;
          case TGTWSettings.BrushType.Cube:
            Handles.CubeHandleCap(GUIUtility.GetControlID(FocusType.Passive), lastEditPt, rot, settingsWindow.brushSize, EventType.Repaint);
            break;
          default:
            return;
        }
      }
      */


      // Draw all the nodes that the tool is colliding with / affecting
      List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(lastEditPt, terrainGrid);
      if (nodes != null) {
        foreach (var node in nodes) {
          var currColour = node.editorUnselectedColour();
          currColour.a = 0.25f;
          Handles.color = currColour;
          Handles.CubeHandleCap(GUIUtility.GetControlID(FocusType.Passive), node.position, rot, terrainGrid.halfUnitsPerNode(), EventType.Repaint);
        }
        if (nodes.Count > 0) { sceneView.Repaint(); }
      }
    }
    
  }
}
