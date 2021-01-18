using UnityEngine;

#pragma warning disable 649

/// <summary>
/// Responsible for centralizing all initialization and teardown operations for the
/// Battlefield scene to make sure things are called in the proper order (since Unity
/// function calls like Start() have undefined ordering).
/// </summary>
public class BattlefieldSceneManager : MonoBehaviour {

  [SerializeField] private LevelLoaderData levelLoader;
  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private BattleStateMachine battleStateMachine;

  // TODO: Group all this stuff together at some point or just move it out of here?
  [Header("Fallback/Default Initialization Data")]
  [SerializeField] private LevelData defaultLevelData;
  [SerializeField] private PlayerRosterData defaultPlayerRoster;

  private void Start() {
    // Set any defaults if necessary
    var llInstance = levelLoader.Instance();
    if (llInstance.levelDataToLoad == null && defaultLevelData != null) {
      llInstance.levelDataToLoad = defaultLevelData;
    }
    if (llInstance.playerRoster == null && defaultPlayerRoster != null) {
      llInstance.playerRoster = defaultPlayerRoster;
    }

    terrainGrid.Init(llInstance.levelDataToLoad);
    battleStateMachine.Init(levelLoader);
  }

}
