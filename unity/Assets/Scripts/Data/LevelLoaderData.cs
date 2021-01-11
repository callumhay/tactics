using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable 649

// NB: the asset is created only once because it contains a singleton,
// so we remove the capability of creating others commenting out "CreateAssetMenu" 
//[CreateAssetMenu(fileName="New Level Loader", menuName="Tactics/LevelLoader")]
public class LevelLoaderData : ScriptableObject {
  public static readonly string DEFAULT_LEVEL_STR = "tutorial";
  private static readonly string LEVELS_DIRECTORY = "Levels";

  [SerializeField] private SceneReference battlefield;

  private static string levelDataFromLevelStr(string levelStr) { return levelStr + "_level_data"; }
  
  // NOTE: This does not instantiate the data, use this for saving purposes, but not for the game state itself!
  public static LevelData loadLevelData(string levelStr) {
    return Resources.Load<LevelData>(LEVELS_DIRECTORY + "/" + levelDataFromLevelStr(levelStr));
  }

  public List<AsyncOperation> loadLevelSceneAsyncOperations(LevelData levelData) {
    var result = new List<AsyncOperation>();

    // Load core/base scenes
    result.Add(SceneManager.LoadSceneAsync(battlefield));

    // Load level-specific scenes
    foreach (var scene in levelData.levelScenes) {
      result.Add(SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive));
    }

    return result;
  }

  public class LevelLoaderManager : SingletonSO<LevelLoaderManager> {
    public LevelData levelDataToLoad = null;
  }
  public LevelLoaderManager Instance() {
    return LevelLoaderManager.Instance;
  }
}
