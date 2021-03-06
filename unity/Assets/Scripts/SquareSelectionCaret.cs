﻿using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 649

public class SquareSelectionCaret : MonoBehaviour {
  public static readonly float CARET_HEIGHT = 0.25f;
  public static readonly float CARET_OVERHANG_SPACING = CARET_HEIGHT/2f;

  [Header("Caret Settings")]
  [Range(0,360)]
  [Tooltip("Rotation speed of the caret in degrees per second")]
  [SerializeField]
  private float rotationSpeed = 45f;
  [Range(0,1)]
  [Tooltip("Caret move speed when the direction is held down")]
  [SerializeField]
  private float moveSpeed = 0.25f;

  [Header("Required GameObjects")]
  [SerializeField] private TerrainGrid terrainGrid;

  [Header("Events")]
  [SerializeField] private GameObjectEvent onCaretMovedEvent;

  //[Header("Materials")]
  //[SerializeField] private Material defaultCaretMaterial;
  //[SerializeField] private Material activeCaretMaterial;

  private TerrainColumnLanding currentLanding;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  // Whether the player has "activated" the currently selected landing 
  // i.e., They are performing some action within that selection 
  // (they must cancel out in order to continue moving the caret)
  private bool isSelectionActive = false;

  // Input state variables
  private bool moveAxisInUse = false;
  private float moveTimeCount = 0f;
  private Vector2 inputMoveVec = new Vector2(0,0);

  public TerrainColumnLanding CurrentLanding { get { return currentLanding; } }

  public bool IsSelectionActive { 
    get { return isSelectionActive; } 
    set { 
      if (gameObject.activeSelf && currentLanding != null) {
        currentLanding.SetActiveSelected(value);
      }
      isSelectionActive = value;
    }
  }

  public void OnMoveCaret(InputAction.CallbackContext inputContext) {
    inputMoveVec = inputContext.ReadValue<Vector2>();
  }

  public void HandleInput() {
    if (isSelectionActive) { 
      moveAxisInUse = false;
      return;
    }

    if (inputMoveVec.x != 0 || inputMoveVec.y != 0) {
      if (!moveAxisInUse) {
        moveAxisInUse = true;
        moveTimeCount = 0;
        MoveCaret(inputMoveVec.x, inputMoveVec.y);
      }
    }
    else {
      moveAxisInUse = false;
    }
  }

  private void MoveCaret(float xAxisDir, float yAxisDir) {
    if (currentLanding == null) { return; }

    // Determine the direction the active camera is pointing in to figure out how to move the caret
    // based on the current controls - i.e., move the controls into worldspace
    var currCamera = Camera.main;
    var adjustedHoriz = currCamera.transform.right * xAxisDir;
    var adjustedVert  = currCamera.transform.up * yAxisDir;

    float dirSign(float value) {
      return Mathf.Abs(value) < 1e-10f ? 0 : (value < 0 ? -1 : 1);
    }

    // Project the controls onto the x and z axis, based on which has a larger magnitude of
    // projected contribution, favour the horizontal projection when there are ties
    var nextIndex = currentLanding.location;
    var absAdjHorizX = Mathf.Abs(adjustedHoriz.x);
    var absAdjVertX  = Mathf.Abs(adjustedVert.x);
    var absAdjHorizZ = Mathf.Abs(adjustedHoriz.z);
    var absAdjVertZ  = Mathf.Abs(adjustedVert.z);

    // Take the maximum projection
    float xVal = absAdjHorizX >= absAdjVertX ? adjustedHoriz.x : adjustedVert.x;
    float zVal = absAdjHorizZ >= absAdjVertZ ? adjustedHoriz.z : adjustedVert.z;
    float absXVal = Mathf.Abs(xVal);
    float absZVal = Mathf.Abs(zVal);

    // We need to watch out for close ties between the |xVal| and |zVal|,
    // if they are close enough to each other then use the |xAxisDir| and |zAxisDir| to break the tie
    if (Mathf.Abs(absXVal - absZVal) < 1e-2f) {
      float absXAxisDir = Mathf.Abs(xAxisDir);
      float absYAxisDir = Mathf.Abs(yAxisDir);
      if (absXAxisDir > absYAxisDir) { zVal = 0; }
      else if (absXAxisDir < absYAxisDir) { xVal = 0; }
    }
    else if (absXVal > absZVal) { zVal = 0; }
    else if (absXVal < absZVal) { xVal = 0; }

    nextIndex.x = (int)Mathf.Clamp(nextIndex.x + dirSign(xVal), 0, terrainGrid.xSize-1);
    nextIndex.z = (int)Mathf.Clamp(nextIndex.z + dirSign(zVal), 0, terrainGrid.zSize-1);
    PlaceCaret(terrainGrid.GetTerrainColumn(nextIndex));
    //Debug.Log("HORIZONTAL: " + adjustedHoriz + " xAxisDir: " + xAxisDir);
    //Debug.Log("VERTICAL: " + adjustedVert + " yAxisDir: " + yAxisDir);
  }

  public void PlaceCaret(in TerrainColumn terrainCol) {
    if (terrainCol == null || terrainCol.landings.Count == 0) { return; }
    var landing = ClosestLanding(terrainCol);
    PlaceCaret(landing);
  }
  public void PlaceCaret(in TerrainColumnLanding landing) {
    if (currentLanding == landing || landing == null) { return; }
    var centerPos = landing.CenterPosition();
    transform.position = centerPos + CaretLocalPosition();
    if (currentLanding != null) { 
      currentLanding.SetSelected(false);
    }
    landing.SetSelected(true);
    currentLanding = landing;
    onCaretMovedEvent?.FireEvent(gameObject);
  }

  private void Awake() {
    meshFilter = GetComponent<MeshFilter>();
    meshRenderer = GetComponent<MeshRenderer>();

    // Scale and position the caret so that it's the right size and positioned
    // above whatever terrain column landing that it's highlighting
    var bounds = meshFilter.mesh.bounds;
    var scale = CARET_HEIGHT / bounds.size.y;
    transform.localScale = new Vector3(scale,scale,scale);
  }

  private void Start() { }

  private void OnEnable() {
    if (currentLanding == null) { return; }
    currentLanding.SetSelected(true);
    currentLanding.SetActiveSelected(isSelectionActive);
  }
  private void OnDisable() {
    if (currentLanding == null) { return; }
    currentLanding.SetSelected(false);
  }

  private void Update() {
    if (currentLanding == null) { 
      gameObject.SetActive(false);
      return;
    }

    var currEulerAngles = transform.localRotation.eulerAngles;
    currEulerAngles.y += rotationSpeed * Time.deltaTime;
    transform.localRotation = Quaternion.Euler(currEulerAngles);

    HandleInput();
    moveTimeCount += Time.deltaTime;
    if (moveTimeCount > moveSpeed) {
      moveAxisInUse = false;
      moveTimeCount = 0;
    }
  }

  private TerrainColumnLanding ClosestLanding(in TerrainColumn terrainCol) {
    if (currentLanding == null && terrainCol.landings.Count > 0) { return terrainCol.landings[0]; }
    TerrainColumnLanding result = null;
    float closestSqrDist = float.MaxValue;
    var currPos = transform.position - CaretLocalPosition();
    foreach (var landing in terrainCol.landings) {
      var sqrDist = Vector3.SqrMagnitude(landing.CenterPosition() - currPos);
      if (sqrDist < closestSqrDist) {
        result = landing;
        closestSqrDist = sqrDist;
      }
    }
    return result;
  }

  private Vector3 CaretLocalPosition() {
    return new Vector3(0, TerrainColumn.MIN_LANDING_OVERHANG_UNITS-CARET_OVERHANG_SPACING, 0);
  }
}
