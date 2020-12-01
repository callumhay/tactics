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

  private TerrainGridToolWindow settingsWindow;

  // Free Paint Editing Mode Variables
  private bool editPtActive = false;
  private Vector3 lastEditPt = new Vector3();

  // Column Editing Mode Variables
  private Vector2 mouseDragStartPos;
  private Vector2 mouseDragCurrentPos;
  private bool isDragging = false;
  //private Dictionary<int, float> selectedColumnCtrlIds = new Dictionary<int, float>();
  private static readonly Color dragRectColour = new Color(0, 1.0f, 1.0f, 0.5f);
  
  private void OnEnable() {
    //EditorTools.activeToolChanged += activeToolChanged;
    SceneView.duringSceneGui += this.onSceneGUI;
  }
  private void OnDisable() {
    //EditorTools.activeToolChanged -= activeToolChanged;
    SceneView.duringSceneGui -= this.onSceneGUI;
  }

  void Awake() {
    settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
  }

  private bool noMouseEventModifiers(in Event e) {
    return e.modifiers == EventModifiers.None || e.modifiers == EventModifiers.Control;
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
  
    switch (settingsWindow.editorType) {
      case TGTWSettings.EditorType.FreePaintEditor: {
        //HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        var wsRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        var yOffset = settingsWindow.paintMode == TGTWSettings.PaintMode.Floating ? 0.5f*settingsWindow.brushSize : 0;
        if (terrainGrid.intersectEditorRay(wsRay, yOffset, out lastEditPt)) {
          editPtActive = true;
          if (e.type == EventType.MouseDown && noMouseEventModifiers(e)) {
            // Grab all the nodes inside the brush
            List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(lastEditPt, terrainGrid);
            if (nodes == null) { return; }

            if (e.button == 1 || (e.button == 0 && e.modifiers == EventModifiers.Control)) {
              Undo.RecordObject(terrainGrid.levelData, "Erased Terrain Nodes");
              settingsWindow.eraseNodes(nodes, terrainGrid);
              GUI.changed = true;
            }
            else if (e.button == 0) {
              Undo.RecordObject(terrainGrid.levelData, "Painted Terrain Nodes");
              settingsWindow.paintNodes(nodes, terrainGrid);
              GUI.changed = true;
            }
          }
        }
        else {
          editPtActive = false;
        }

        break;
      }

      case TGTWSettings.EditorType.ColumnEditor: {
         
        if (noMouseEventModifiers(e)) {
          if (e.type == EventType.MouseDrag) {
            mouseDragCurrentPos = Event.current.mousePosition;
            if (!isDragging) {
              // Start dragging
              isDragging = true;
              mouseDragStartPos = mouseDragCurrentPos;
            }
          }
          else if (e.type == EventType.MouseUp) {
            // Stop dragging
            isDragging = false;
          }
        }

        break;
      }

      default:
        break;
    }
  }

  void onSceneGUI(SceneView sceneView) {
    var terrainGrid = target as TerrainGrid;
    if (!InternalEditorUtility.isApplicationActive || !terrainGrid || !settingsWindow) { return; }

    bool redraw = false;
    var rot = new Quaternion(0,0,0,1);

    switch (settingsWindow.editorType) {
      case TGTWSettings.EditorType.FreePaintEditor: {
        if (editPtActive && Event.current.type == EventType.Repaint) {
          // Draw all the nodes that the tool is colliding with / affecting
          List<TerrainGridNode> nodes = settingsWindow.getAffectedNodesAtPoint(lastEditPt, terrainGrid);
          if (nodes != null) {
            redraw = nodes.Count > 0;
            var halfUnitsPerNode = TerrainGrid.halfUnitsPerNode();
            foreach (var node in nodes) {
              var currColour = node.editorUnselectedColour();
              currColour.a = 0.5f;
              Handles.color = currColour;
              Handles.CubeHandleCap(GUIUtility.GetControlID(FocusType.Passive), node.position, rot, halfUnitsPerNode, EventType.Repaint);
            }
          }
        }
        break;
      }

      case TGTWSettings.EditorType.ColumnEditor: {
        drawTerrainColumnHeightEditHandles(terrainGrid);

        if (isDragging) {
          
          /*
          Handles.BeginGUI();
          EditorGUI.DrawRect(new Rect(mouseDragStartPos.x, mouseDragStartPos.y, 
            mouseDragCurrentPos.x - mouseDragStartPos.x, 
            mouseDragCurrentPos.y - mouseDragStartPos.y), dragRectColour);
          Handles.EndGUI();
          */
        }
        
        break;
      }

      default:
        break;
    }

    if (settingsWindow.showGridOverlay) {
      redraw = true;
      drawTerrainGridOverlay(terrainGrid);
    }

    if (redraw) { sceneView.Repaint(); }
  }

  private void drawTerrainColumnHeightEditHandles(in TerrainGrid terrainGrid) {
    Handles.color = Color.green;
    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var halfUnitsPerNode = TerrainGrid.halfUnitsPerNode();
    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.size + 0.5f*TerrainColumn.size;
      for (int z = 0; z < terrainGrid.zSize; z++) {
        var zPos = z*TerrainColumn.size + 0.5f*TerrainColumn.size;
        var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode;
        var baseHandlePos = new Vector3(xPos, yPos, zPos) + translation;
        var name = "TCHeightHandle(" + xPos + "," + zPos + ")";
        var controlId = EditorGUIUtility.GetControlID(name.GetHashCode(), FocusType.Keyboard);
        
        EditorGUI.BeginChangeCheck();
        var newPos = Handles.Slider(baseHandlePos, Vector3.up, 0.1f, Handles.CubeHandleCap, TerrainGrid.unitsPerNode());
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          Undo.RecordObject(terrainGrid.levelData, "Edited Terrain Column Height");
          terrainGrid.changeTerrainColumnHeight(x, z, newPos.y);
        }
        //var size = 0.05f*HandleUtility.GetHandleSize(baseHandlePos);
        //var clicked = Handles.Button(baseHandlePos, rot, size, size, Handles.DotHandleCap);
        //if (clicked) { 
        //  selectedColumnCtrlIds.Clear();
        //  selectedColumnCtrlIds.Add(controlId, yPos);
        //}
      }
    }
  }

  private void drawTerrainGridOverlay(in TerrainGrid terrainGrid) {
    Handles.color = new Color(0.4f, 0.4f, 1.0f, 0.25f);
    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
    
    var faceColour = new Color(0.5f, 0.5f, 1.0f, 0.5f);
    var outlineColour = new Color(1, 1, 1, 0.75f);
    var halfUnitsPerNode = TerrainGrid.halfUnitsPerNode();

    var translation = terrainGrid.transform.position;
    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.size;
      for (int z = 0; z < terrainGrid.zSize; z++) {
        if ((x+z) % 2 == 0) {
          var zPos = z*TerrainColumn.size;
          var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode + 1e-4f;
          var quadVerts = new Vector3[4];
          quadVerts[0] = new Vector3(xPos, yPos, zPos) + translation;
          quadVerts[1] = new Vector3(xPos+TerrainColumn.size, yPos, zPos) + translation;
          quadVerts[2] = new Vector3(xPos+TerrainColumn.size, yPos, zPos+TerrainColumn.size) + translation;
          quadVerts[3] = new Vector3(xPos, yPos, zPos+TerrainColumn.size) + translation;
          Handles.DrawSolidRectangleWithOutline(quadVerts, faceColour, outlineColour);
        }
      }
    }
  }

}
