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
    SceneView.duringSceneGui += this.OnSceneGUI;
  }
  private void OnDisable() {
    //EditorTools.activeToolChanged -= activeToolChanged;
    SceneView.duringSceneGui -= this.OnSceneGUI;
  }

  void Awake() {
    settingsWindow = EditorWindow.GetWindow<TerrainGridToolWindow>();
  }

  private bool NoMouseEventModifiers(in Event e) {
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
          if (e.type == EventType.MouseDown && NoMouseEventModifiers(e)) {
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
        DrawTerrainColumnHeightEditHandles(terrainGrid);
        if (NoMouseEventModifiers(e)) {
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
        DrawTerrainColumnSelectionHandles(terrainGrid);
        DrawTerrainColumnNodeEditHandles(terrainGrid);
        break;
      }

      case TGTWSettings.EditorType.PlacementEditor: {
        DrawTerrainColumnButtons(terrainGrid);
        break;
      }

      default:
        break;
    }
  }

  void OnSceneGUI(SceneView sceneView) {
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
            var halfUnitsPerNode = TerrainGrid.HalfUnitsPerNode();
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

      case TGTWSettings.EditorType.PlacementEditor: {
        DrawCharacterPlacements(terrainGrid);
        break;
      }

      default:
        break;
    }

    if (settingsWindow.showGridOverlay) {
      redraw = true;
      DrawTerrainGridOverlay(terrainGrid);
    }

    if (redraw) { sceneView.Repaint(); }
  }

  private void DrawTerrainColumnHeightEditHandles(in TerrainGrid terrainGrid) {
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var maxInset = TerrainGridToolWindow.maxInset();
    var halfUnitsPerNode = TerrainGrid.HalfUnitsPerNode();
    var insetFaceColour = new Color(1.0f, 1.0f, 0.0f, 0.5f);
    var insetOutlineColour = new Color(1f,1f,1f,0.5f);
    var insetXUnits = settingsWindow.columnInsetXAmount*TerrainGrid.UnitsPerNode();
    var insetNegXUnits = settingsWindow.columnInsetNegXAmount*TerrainGrid.UnitsPerNode();
    var insetZUnits = settingsWindow.columnInsetZAmount*TerrainGrid.UnitsPerNode();
    var insetNegZUnits = settingsWindow.columnInsetNegZAmount*TerrainGrid.UnitsPerNode();
    var insetHandleSnap = TerrainGrid.UnitsPerNode()*1.5f;

    for (int x = 0; x < terrainGrid.xSize; x++) {
      float insetXPos = x*TerrainColumn.SIZE;
      var xPos = x*TerrainColumn.SIZE + 0.5f*TerrainColumn.SIZE;
      for (int z = 0; z < terrainGrid.zSize; z++) {

        var zPos = z*TerrainColumn.SIZE + 0.5f*TerrainColumn.SIZE;
        var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode;
        var baseHandlePos = new Vector3(xPos, yPos+TerrainGrid.UnitsPerNode(), zPos) + translation;
        var name = "TCHeightHandle(" + xPos + "," + zPos + ")";
        var controlId = EditorGUIUtility.GetControlID(name.GetHashCode(), FocusType.Keyboard);

        // Draw the inset as a rectangle on the column
        float insetZPos = z*TerrainColumn.SIZE;
        float insetYPos = yPos + halfUnitsPerNode*0.5f;
        var quadVerts = new Vector3[4];
        quadVerts[0] = new Vector3(insetXPos+insetXUnits, insetYPos, insetZPos+insetZUnits) + translation;
        quadVerts[1] = new Vector3(insetXPos+TerrainColumn.SIZE-insetNegXUnits, insetYPos, insetZPos+insetZUnits) + translation;
        quadVerts[2] = new Vector3(insetXPos+TerrainColumn.SIZE-insetNegXUnits, insetYPos, insetZPos+TerrainColumn.SIZE-insetNegZUnits) + translation;
        quadVerts[3] = new Vector3(insetXPos+insetXUnits, insetYPos, insetZPos+TerrainColumn.SIZE-insetNegZUnits) + translation;
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
        var newPos = Handles.Slider(baseHandlePos, Vector3.up, 0.1f, Handles.CubeHandleCap, TerrainGrid.UnitsPerNode());
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

  private void DrawTerrainColumnSelectionHandles(in TerrainGrid terrainGrid) {
    Handles.color = Color.green;
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var halfUnitsPerNode = TerrainGrid.HalfUnitsPerNode();

    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.SIZE + 0.5f*TerrainColumn.SIZE;
      for (int z = 0; z < terrainGrid.zSize; z++) {
        var currIdx = new Vector2Int(x,z);
        if (nodeEditSelectedColumns.Contains(currIdx)) { continue; }

        var zPos = z*TerrainColumn.SIZE + 0.5f*TerrainColumn.SIZE;
        var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode;
        var pos = new Vector3(xPos, yPos, zPos) + translation;

        var size = TerrainGrid.UnitsPerNode();
        if (Handles.Button(pos, rot, size, size, Handles.SphereHandleCap)) { 
          if (Event.current.modifiers != EventModifiers.Shift) {
            nodeEditSelectedColumns.Clear();
          }
          nodeEditSelectedColumns.Add(currIdx);
        }
      }
    }
  }

  private void DrawTerrainColumnNodeEditHandles(in TerrainGrid terrainGrid) {
    
    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
    var nodeDrawSize = TerrainGrid.HalfUnitsPerNode();
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
        TerrainGrid.UnitsToNodeIndex(settingsWindow.setLevelValue), nodeSelector);
      
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

  private void DrawTerrainColumnButtons(in TerrainGrid terrainGrid) {
    Handles.color = Color.white;
    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

    var halfUnitsPerNode = TerrainGrid.HalfUnitsPerNode();
    var translation = terrainGrid.transform.position;
    var rot = new Quaternion(0,0,0,1);
    var buttonSize = 0.5f*TerrainColumn.HALF_SIZE;
    var tcIdx = new Vector2Int();
    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.SIZE;
      for (int z = 0; z < terrainGrid.zSize; z++) {

        tcIdx.Set(x,z);
        var terrainColumn = terrainGrid.GetTerrainColumn(tcIdx);
        if (!terrainColumn) { return; }

        var zPos = z*TerrainColumn.SIZE;
        for (int i = 0; i < terrainColumn.landings.Count; i++) {
          var landing = terrainColumn.landings[i];
          var yPos = landing.AverageYPos() + buttonSize + 1e-4f;
          var buttonPos = new Vector3(xPos + TerrainColumn.HALF_SIZE, yPos, zPos + TerrainColumn.HALF_SIZE) + translation;

          if (Handles.Button(buttonPos, rot, buttonSize, buttonSize, Handles.CubeHandleCap)) {
            // Add, Change or remove a placement...
            Undo.RecordObject(terrainGrid.levelData, "Character Placement Change");
            var placementLocation = new Vector3Int(x,i,z);
            var existingPlacement = terrainGrid.levelData.GetPlacementAt(placementLocation);
            var selectedPlacementTeam = settingsWindow.Team;
            if (existingPlacement != null) {
              // If the selected team is the same as the team set on this placement then we remove it, 
              // otherwise we change it to the selected team
              if (existingPlacement.Team == selectedPlacementTeam) {
                terrainGrid.levelData.Placements.Remove(existingPlacement);
              }
              else {
                existingPlacement.Team = selectedPlacementTeam;
              }
            }
            else {
              terrainGrid.levelData.Placements.Add(new CharacterPlacement(placementLocation, selectedPlacementTeam));
            }
          }
        }

      }
    }
  }

  private void DrawTerrainGridOverlay(in TerrainGrid terrainGrid) {
    Handles.color = new Color(0.4f, 0.4f, 1.0f, 0.25f);
    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
    
    var faceColour = new Color(0.5f, 0.5f, 1.0f, 0.5f);
    var outlineColour = new Color(1, 1, 1, 0.75f);
    var halfUnitsPerNode = TerrainGrid.HalfUnitsPerNode();

    var translation = terrainGrid.transform.position;
    for (int x = 0; x < terrainGrid.xSize; x++) {
      var xPos = x*TerrainColumn.SIZE;
      for (int z = 0; z < terrainGrid.zSize; z++) {
        if ((x+z) % 2 == 0) {
          var zPos = z*TerrainColumn.SIZE;
          var yPos = terrainGrid.fastSampleHeight(x,z) + halfUnitsPerNode + 1e-4f;
          var quadVerts = new Vector3[4];
          quadVerts[0] = new Vector3(xPos, yPos, zPos) + translation;
          quadVerts[1] = new Vector3(xPos+TerrainColumn.SIZE, yPos, zPos) + translation;
          quadVerts[2] = new Vector3(xPos+TerrainColumn.SIZE, yPos, zPos+TerrainColumn.SIZE) + translation;
          quadVerts[3] = new Vector3(xPos, yPos, zPos+TerrainColumn.SIZE) + translation;
          Handles.DrawSolidRectangleWithOutline(quadVerts, faceColour, outlineColour);
        }
      }
    }
  }

  private void DrawCharacterPlacements(in TerrainGrid terrainGrid) {
    var outlineColour = new Color(1f,1f,1f,0.75f);
    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

    var translation = terrainGrid.transform.position;
    foreach (var placement in terrainGrid.levelData.Placements) {
      var terrainCol = terrainGrid.GetTerrainColumn(placement.Location);
      if (terrainCol == null) { return; }
      var landing = terrainCol.landings[placement.Location.y];
      if (landing == null) { return; }

      var position = landing.CenterPosition();
      position.y = landing.MaxYPos() + TerrainGrid.HalfUnitsPerNode();
      var colour = placement.Team.PlacementColour;
      position.y += 1e-4f;
      position.x -= TerrainColumn.HALF_SIZE;
      position.z -= TerrainColumn.HALF_SIZE;

      var quadVerts = new Vector3[4];
      quadVerts[0] = position + translation;
      quadVerts[1] = new Vector3(position.x+TerrainColumn.SIZE, position.y, position.z) + translation;
      quadVerts[2] = new Vector3(position.x+TerrainColumn.SIZE, position.y, position.z+TerrainColumn.SIZE) + translation;
      quadVerts[3] = new Vector3(position.x, position.y, position.z+TerrainColumn.SIZE) + translation;

      Handles.DrawSolidRectangleWithOutline(quadVerts, new Color(colour.r, colour.g, colour.b, 0.5f), outlineColour);
      Handles.Label(position, placement.Team.name);
    }
  }
  
}
