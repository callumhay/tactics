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

  // Mouse Dragging functionality (NOT IMPLEMENTED YET)
  private static readonly Color dragRectColour = new Color(0, 1.0f, 1.0f, 0.5f);
  private Vector2 mouseDragStartPos;
  private Vector2 mouseDragCurrentPos;
  private bool isDragging = false;

  // Node Editing Mode Variables
  private HashSet<Vector2Int> nodeEditSelectedColumns = new HashSet<Vector2Int>();
  
  
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
        drawTerrainColumnHeightEditHandles(terrainGrid);
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

          if (isDragging) {
            /*
            Handles.BeginGUI();
            EditorGUI.DrawRect(new Rect(mouseDragStartPos.x, mouseDragStartPos.y, 
              mouseDragCurrentPos.x - mouseDragStartPos.x, 
              mouseDragCurrentPos.y - mouseDragStartPos.y), dragRectColour);
            Handles.EndGUI();
            */
          }

        }
        break;
      }

      case TGTWSettings.EditorType.NodeEditor: {
        drawTerrainColumnSelectionHandles(terrainGrid);
        drawTerrainColumnNodeEditHandles(terrainGrid);
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
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var maxInset = TerrainGridToolWindow.maxInset();
    var halfUnitsPerNode = TerrainGrid.halfUnitsPerNode();
    var insetFaceColour = new Color(1.0f, 1.0f, 0.0f, 0.5f);
    var insetOutlineColour = new Color(1f,1f,1f,0.5f);
    var insetXUnits = settingsWindow.columnInsetXAmount*TerrainGrid.unitsPerNode();
    var insetNegXUnits = settingsWindow.columnInsetNegXAmount*TerrainGrid.unitsPerNode();
    var insetZUnits = settingsWindow.columnInsetZAmount*TerrainGrid.unitsPerNode();
    var insetNegZUnits = settingsWindow.columnInsetNegZAmount*TerrainGrid.unitsPerNode();
    var insetHandleSnap = TerrainGrid.unitsPerNode()*1.5f;

    for (int x = 0; x < terrainGrid.xSize; x++) {
      float insetXPos = x*TerrainColumn.size;
      var xPos = x*TerrainColumn.size + 0.5f*TerrainColumn.size;
      for (int z = 0; z < terrainGrid.zSize; z++) {

        var zPos = z*TerrainColumn.size + 0.5f*TerrainColumn.size;
        var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode;
        var baseHandlePos = new Vector3(xPos, yPos+TerrainGrid.unitsPerNode(), zPos) + translation;
        var name = "TCHeightHandle(" + xPos + "," + zPos + ")";
        var controlId = EditorGUIUtility.GetControlID(name.GetHashCode(), FocusType.Keyboard);

        // Draw the inset as a rectangle on the column
        float insetZPos = z*TerrainColumn.size;
        float insetYPos = yPos + halfUnitsPerNode*0.5f;
        var quadVerts = new Vector3[4];
        quadVerts[0] = new Vector3(insetXPos+insetXUnits, insetYPos, insetZPos+insetZUnits) + translation;
        quadVerts[1] = new Vector3(insetXPos+TerrainColumn.size-insetNegXUnits, insetYPos, insetZPos+insetZUnits) + translation;
        quadVerts[2] = new Vector3(insetXPos+TerrainColumn.size-insetNegXUnits, insetYPos, insetZPos+TerrainColumn.size-insetNegZUnits) + translation;
        quadVerts[3] = new Vector3(insetXPos+insetXUnits, insetYPos, insetZPos+TerrainColumn.size-insetNegZUnits) + translation;
        Handles.color = insetFaceColour;
        Handles.DrawSolidRectangleWithOutline(quadVerts, insetFaceColour, insetOutlineColour);

        // Draw handles for adjusting the insets
        // Positive X Handle
        Handles.color = Color.blue;
        var xInsetBaseHandlePos = (quadVerts[0] + quadVerts[3]) / 2f;
        EditorGUI.BeginChangeCheck();
        var xInsetVal = Handles.Slider(xInsetBaseHandlePos, Vector3.right, 0.1f, Handles.SphereHandleCap, insetHandleSnap);
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          settingsWindow.columnInsetXAmount = Mathf.RoundToInt(Mathf.Clamp((xInsetVal.x-xInsetBaseHandlePos.x) / insetHandleSnap, 0, maxInset));
        }
        // Negative X Handle
        var negXInsetBaseHandlePos = (quadVerts[1] + quadVerts[2]) / 2f;
        EditorGUI.BeginChangeCheck();
        var negXInsetVal = Handles.Slider(negXInsetBaseHandlePos, Vector3.left, 0.1f, Handles.SphereHandleCap, insetHandleSnap);
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          settingsWindow.columnInsetNegXAmount = Mathf.RoundToInt(Mathf.Clamp((negXInsetBaseHandlePos.x-negXInsetVal.x) / insetHandleSnap, 0, maxInset));
        }
        // Positive Z Handle
        var zInsetBaseHandlePos = (quadVerts[0] + quadVerts[1]) / 2f;
        EditorGUI.BeginChangeCheck();
        var zInsetVal = Handles.Slider(zInsetBaseHandlePos, Vector3.forward, 0.1f, Handles.SphereHandleCap, insetHandleSnap);
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          settingsWindow.columnInsetZAmount = Mathf.RoundToInt(Mathf.Clamp((zInsetVal.z-zInsetBaseHandlePos.x) / insetHandleSnap, 0, maxInset));
        }
        // Negative Z Handle
        var negZInsetBaseHandlePos = (quadVerts[2] + quadVerts[3]) / 2f;
        EditorGUI.BeginChangeCheck();
        var negZInsetVal = Handles.Slider(negZInsetBaseHandlePos, Vector3.back, 0.1f, Handles.SphereHandleCap, insetHandleSnap);
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          settingsWindow.columnInsetNegZAmount = Mathf.RoundToInt(Mathf.Clamp((negZInsetBaseHandlePos.z-negZInsetVal.z) / insetHandleSnap, 0, maxInset));
        }
        
        // Draw and allow for manipulation of the column height
        EditorGUI.BeginChangeCheck();
        Handles.color = Color.green;
        //Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        var newPos = Handles.Slider(baseHandlePos, Vector3.up, 0.1f, Handles.CubeHandleCap, TerrainGrid.unitsPerNode());
        if (EditorGUI.EndChangeCheck()) {
          Event.current.Use();
          Undo.RecordObject(terrainGrid.levelData, "Edited Terrain Column Height");
          terrainGrid.changeTerrainColumnHeight(x, z, newPos.y, settingsWindow.setLevelValue, 
            settingsWindow.columnInsetXAmount, settingsWindow.columnInsetNegXAmount,
            settingsWindow.columnInsetZAmount, settingsWindow.columnInsetNegZAmount, settingsWindow.paintMaterial);
        }
      }
    }
  }

  private void drawTerrainColumnSelectionHandles(in TerrainGrid terrainGrid) {
    Handles.color = Color.green;
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var halfUnitsPerNode = TerrainGrid.halfUnitsPerNode();

    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.size + 0.5f*TerrainColumn.size;
      for (int z = 0; z < terrainGrid.zSize; z++) {
        var currIdx = new Vector2Int(x,z);
        if (nodeEditSelectedColumns.Contains(currIdx)) { continue; }

        var zPos = z*TerrainColumn.size + 0.5f*TerrainColumn.size;
        var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode;
        var pos = new Vector3(xPos, yPos, zPos) + translation;

        var size = TerrainGrid.unitsPerNode();
        if (Handles.Button(pos, rot, size, size, Handles.SphereHandleCap)) { 
          if (Event.current.modifiers != EventModifiers.Shift) {
            nodeEditSelectedColumns.Clear();
          }
          nodeEditSelectedColumns.Add(currIdx);
        }
      }
    }
  }

  private void drawTerrainColumnNodeEditHandles(in TerrainGrid terrainGrid) {
    
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
    var nodeDrawSize = TerrainGrid.halfUnitsPerNode();
    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);

    var terrainColour = new Color(1f,0.5f,0f,0.33f);
    var emptyColour   = new Color(1f,0f,1f,0.33f);

    var nodeSelector = TerrainGrid.TerrainNodeSelectors.None;
    if (settingsWindow.showSurfaceNodes) { nodeSelector |= TerrainGrid.TerrainNodeSelectors.Surface; }
    if (settingsWindow.showAboveSurfaceNodes) { nodeSelector |= TerrainGrid.TerrainNodeSelectors.AboveSurface; }
    if (nodeSelector == TerrainGrid.TerrainNodeSelectors.None) { nodeSelector = TerrainGrid.TerrainNodeSelectors.All; }
    
    var tempList = new List<TerrainGridNode>();
    foreach (var terrainColIdx in nodeEditSelectedColumns) {
      var nodes = terrainGrid.getNodesInTerrainColumn(new Vector3Int(terrainColIdx.x, 0, terrainColIdx.y), 
        TerrainGrid.unitsToNodeIndex(settingsWindow.setLevelValue), nodeSelector);
      
      foreach (var node in nodes) {
        float toggleIsoVal = 1f;
        if (node.isTerrain()) {
          toggleIsoVal = -1f;
          if (!settingsWindow.showTerrainNodes) { continue; }
          Handles.color = terrainColour;
        }
        else {
          if (!settingsWindow.showEmptyNodes) { continue; }
          Handles.color = emptyColour;
        }
        if (Handles.Button(node.position + translation, rot, nodeDrawSize, nodeDrawSize, Handles.CubeHandleCap)) {
          tempList.Clear(); tempList.Add(node);
          Undo.RecordObject(terrainGrid.levelData, "Edited Terrain Node");
          terrainGrid.addIsoValuesToNodes(toggleIsoVal, tempList);
        }
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
