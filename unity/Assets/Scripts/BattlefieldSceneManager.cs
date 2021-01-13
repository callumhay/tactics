using UnityEngine;

#pragma warning disable 649

/// <summary>
/// Responsible for centralizing all initialization and teardown operations for the
/// Battlefield scene to make sure things are called in the proper order (since Unity
/// function calls like Start() have undefined ordering).
/// </summary>
public class BattlefieldSceneManager : MonoBehaviour {

  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private BattleStateMachine battleStateMachine;

  private void Start() {
    terrainGrid.Init();
    battleStateMachine.Init();
  }

}
