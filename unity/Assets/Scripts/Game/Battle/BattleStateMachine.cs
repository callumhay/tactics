using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 649

public class BattleStateMachine : MonoBehaviour {

  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private SquareSelectionCaret selectionCaret;
  [SerializeField] private LevelLoaderData levelLoader;

  //[SerializeField] private StartingCutsceneBattleState startCutsceneState;
  [SerializeField] private FormationBattleState formationState;
  [SerializeField] private CommenceBattleState commenceState;
  //[SerializeField] private TurnBasedBattleState turnBasedState;

  private BattleState currentState;
  public Vector2 currentInputMoveVec { get; private set; } = new Vector2(0,0);


  public TerrainGrid TerrainGrid { get { return terrainGrid; } }
  public SquareSelectionCaret SelectionCaret { get { return selectionCaret; } }
  public LevelLoaderData LevelLoader { get { return levelLoader; } }

  public FormationBattleState FormationState { get { return formationState; } }
  public CommenceBattleState CommenceState { get { return commenceState; } }

  public void Init() {
    SetState(formationState);
  }

  public void SetState(BattleState nextState) {
    if (currentState != null) {
      StartCoroutine(currentState.ExitEvent(this));
    }
    currentState = nextState;
    StartCoroutine(currentState.EnterEvent(this));
  }

  private void Update() {
    StartCoroutine(currentState.UpdateEvent(this));
  }

  


  public void SpawnCharacter(CharacterPlacement placement) {
     SpawnCharacter(placement.Location, placement.Character); 
  }
  public void SpawnCharacter(Vector3Int location, CharacterData character) {
    Debug.Assert(character != null, "Attempting to spawn a character that doesn't exist!");

    var terrainCol = terrainGrid.GetTerrainColumn(location);
    Debug.Assert(terrainCol != null, "No TerrainColumn found at location " + location + ". Check GameObject initialization ordering!");



  }

  // Player Input Functions ***********************
  public void OnMove(InputAction.CallbackContext inputContext) {
    currentInputMoveVec = inputContext.ReadValue<Vector2>();
  }

  public void OnSubmit(InputAction.CallbackContext inputContext) {
    if (inputContext.performed) { currentState.OnSubmit(this); }
  }

  public void OnCancel(InputAction.CallbackContext inputContext) {
    if (inputContext.performed) { currentState.OnCancel(this); }
  }



}
