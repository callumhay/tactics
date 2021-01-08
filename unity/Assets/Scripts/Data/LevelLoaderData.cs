using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable 649
[CreateAssetMenu(fileName="New Level Loader", menuName="Tactics/LevelLoader")]
public class LevelLoaderData : ScriptableObject {
  public static readonly string DEFAULT_LEVEL_STR = "tutorial";
  private static readonly string LEVELS_DIRECTORY = "Levels";

  [SerializeField] private SceneReference battlefield;

  private static string levelDataFromLevelStr(string levelStr) { return levelStr + "_level_data"; }
  
  public LevelData loadLevelDataInstance(string levelStr) {
    return ScriptableObject.Instantiate<LevelData>(Resources.Load<LevelData>(LEVELS_DIRECTORY + "/" + levelDataFromLevelStr(levelStr)));
  }

  public List<AsyncOperation> loadLevelAsyncOperations(LevelData levelData) {
    var result = new List<AsyncOperation>();

    // Load core/base scenes
    result.Add(SceneManager.LoadSceneAsync(battlefield));

    // Load level-specific scenes
    foreach (var scene in levelData.levelScenes) {
      result.Add(SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive));
    }

    return result;
  }

}
