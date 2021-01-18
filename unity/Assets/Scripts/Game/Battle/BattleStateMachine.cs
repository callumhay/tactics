using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 649

public class BattleStateMachine : MonoBehaviour {

  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private CharacterManager characterManager;
  [SerializeField] private SquareSelectionCaret selectionCaret;
  [SerializeField] private CharacterInfoAndPlacementUI infoAndPlacementUI;


  //[SerializeField] private StartingCutsceneBattleState startCutsceneState;
  [SerializeField] private FormationBattleState formationState;
  [SerializeField] private CommenceBattleState commenceState;
  //[SerializeField] private TurnBasedBattleState turnBasedState;

  private BattleState currentState;
  private LevelLoaderData levelLoader;

  public Vector2 currentInputMoveVec { get; private set; } = new Vector2(0,0);

  public TerrainGrid TerrainGrid { get { return terrainGrid; } }
  public CharacterManager CharacterManager { get { return characterManager; } }
  public SquareSelectionCaret SelectionCaret { get { return selectionCaret; } }
  public CharacterInfoAndPlacementUI InfoAndPlacementUI { get { return infoAndPlacementUI; } }
  public LevelLoaderData LevelLoader { get { return levelLoader; } }

  public FormationBattleState FormationState { get { return formationState; } }
  public CommenceBattleState CommenceState { get { return commenceState; } }

  public void Init(LevelLoaderData lvlLoader) {
    levelLoader = lvlLoader;
    currentState = null;

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

  #region Input Events
  public void OnMove(InputAction.CallbackContext inputContext) {
    currentInputMoveVec = inputContext.ReadValue<Vector2>();
  }
  public void OnSubmit(InputAction.CallbackContext inputContext) {
    if (inputContext.performed) { currentState.OnSubmitInputEvent(this); }
  }
  public void OnCancel(InputAction.CallbackContext inputContext) {
    if (inputContext.performed) { currentState.OnCancelInputEvent(this); }
  }
  public void OnRemove(InputAction.CallbackContext inputContext) {
    if (inputContext.performed) { currentState.OnRemoveInputEvent(this); }
  }
  #endregion

}
