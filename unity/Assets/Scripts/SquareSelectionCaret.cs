using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SquareSelectionCaret : MonoBehaviour {
  public static readonly float CARET_HEIGHT = 0.25f;
  public static readonly float CARET_OVERHANG_SPACING = CARET_HEIGHT/2f;

  [Range(0,360)]
  [Tooltip("Rotation speed of the caret in degrees per second")]
  public float rotationSpeed = 45f;
  [Range(0,1)]
  [Tooltip("Caret move speed when the direction is held down")]
  public float moveSpeed = 0.25f;

  public TerrainGrid terrainGrid;
  private TerrainColumn.Landing currLanding;

  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  private bool moveAxisInUse = false;
  private float moveTimeCount = 0f;

  public void handleInput() {
    var horizInput = Input.GetAxisRaw("Horizontal");
    var vertInput  = Input.GetAxisRaw("Vertical");
    if (horizInput != 0 || vertInput != 0) {
      if (!moveAxisInUse) {
        moveAxisInUse = true;
        moveTimeCount = 0;
        moveCaret(horizInput, vertInput);
      }
    }
    else {
      moveAxisInUse = false;
    }
  }

  private void moveCaret(float xAxisDir, float yAxisDir) {
    if (currLanding == null) { return; }
    var terrainCol = currLanding.terrainColumn;
    var terrainGrid = terrainCol.terrain;

    // Determine the direction the active camera is pointing in to figure out how to move the caret
    // based on the current controls - i.e., move the controls into worldspace
    var currCamera = Camera.main;
    var adjustedHoriz = currCamera.transform.right * xAxisDir;
    var adjustedVert  = currCamera.transform.up * yAxisDir;

    float dirSign(float value) {
      return Mathf.Abs(value) < 1e-4f ? 0 : (value < 0 ? -1 : 1);
    }

    // Project the controls onto the x and z axis, based on which has a larger magnitude of
    // projected contribution, favour the horizontal projection when there are ties
    var nextIndex = terrainCol.index;
    var absAdjHorizX = Mathf.Abs(adjustedHoriz.x);
    var absAdjVertX  = Mathf.Abs(adjustedVert.x);
    var absAdjHorizZ = Mathf.Abs(adjustedHoriz.z);
    var absAdjVertZ  = Mathf.Abs(adjustedVert.z);

    // Take the maximum projection
    float xVal = absAdjHorizX >= absAdjVertX ? adjustedHoriz.x : adjustedVert.x;
    float zVal = absAdjHorizZ >= absAdjVertZ ? adjustedHoriz.z : adjustedVert.z;
    if (Mathf.Abs(xVal) >= Mathf.Abs(zVal)) { zVal = 0; }
    else { xVal = 0; }

    nextIndex.x = (int)Mathf.Clamp(nextIndex.x + dirSign(xVal), 0, terrainGrid.xSize-1);
    nextIndex.z = (int)Mathf.Clamp(nextIndex.z + dirSign(zVal), 0, terrainGrid.zSize-1);
    placeCaret(terrainGrid.terrainColumn(nextIndex));
    //Debug.Log("HORIZONTAL: " + adjustedHoriz + " xAxisDir: " + xAxisDir);
    //Debug.Log("VERTICAL: " + adjustedVert + " yAxisDir: " + yAxisDir);
  }

  public void placeCaret(in TerrainColumn terrainCol) {
    if (terrainCol == null || terrainCol.landings.Count == 0) { return; }
    var landing = closestLanding(terrainCol);
    placeCaret(landing);
  }
  public void placeCaret(in TerrainColumn.Landing landing) {
    if (currLanding == landing || landing == null) { return; }
    var centerPos = landing.centerPosition();
    transform.position = centerPos + caretLocalPosition();
    if (currLanding != null) { 
      currLanding.gameObj.SetActive(false);
    }
    landing.gameObj.SetActive(true);
    currLanding = landing;
  }

  void Start() {
    if (terrainGrid == null) {
      var terrainGO = GameObject.Find(TerrainGrid.GAME_OBJ_NAME);
      if (terrainGO) { terrainGrid = terrainGO.GetComponent<TerrainGrid>(); }
    }
    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) { meshFilter = gameObject.AddComponent<MeshFilter>(); }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) { meshRenderer = gameObject.AddComponent<MeshRenderer>(); }
    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

    // Scale and position the caret so that it's the right size and positioned
    // above whatever terrain column landing that it's highlighting
    var bounds = meshFilter.mesh.bounds;
    var scale = CARET_HEIGHT / bounds.size.y;
    transform.localScale = new Vector3(scale,scale,scale);
    placeCaret(terrainGrid.terrainColumn(new Vector2Int(0,0)));
  }

  void Update() {
    if (currLanding == null || currLanding.gameObj == null) { 
      gameObject.SetActive(false);
      return;
    }

    var currEulerAngles = transform.localRotation.eulerAngles;
    currEulerAngles.y += rotationSpeed * Time.deltaTime;
    transform.localRotation = Quaternion.Euler(currEulerAngles);

    handleInput();
    moveTimeCount += Time.deltaTime;
    if (moveTimeCount > moveSpeed) {
      moveAxisInUse = false;
      moveTimeCount = 0;
    }
  }

  private TerrainColumn.Landing closestLanding(in TerrainColumn terrainCol) {
    if (currLanding == null && terrainCol.landings.Count > 0) { return terrainCol.landings[0]; }
    TerrainColumn.Landing result = null;
    float closestSqrDist = float.MaxValue;
    foreach (var landing in terrainCol.landings) {
      var sqrDist = Vector3.SqrMagnitude(landing.centerPosition() - transform.position);
      if (sqrDist < closestSqrDist) {
        result = landing;
        closestSqrDist = sqrDist;
      }
    }
    return result;
  }

  private Vector3 caretLocalPosition() {
    return new Vector3(0, TerrainColumn.MIN_LANDING_OVERHANG_UNITS-CARET_OVERHANG_SPACING, 0);
  }
}
