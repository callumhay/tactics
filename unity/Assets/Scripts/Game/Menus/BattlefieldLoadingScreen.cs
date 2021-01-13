using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#pragma warning disable 649
public class BattlefieldLoadingScreen : MonoBehaviour {
  [SerializeField] private LevelLoaderData levelLoader;
  [SerializeField] private Image progressBar;
  [SerializeField] private TextMeshProUGUI levelNameText;
  

  // This allows us to load multiple additive scenes so that we can make our scenes more modular
  // by including multiple scene loads which might have functionality shared across multiple levels
  private List<AsyncOperation> asyncScenesToLoad;

  private void Awake() {
    progressBar.fillAmount = 0;
  }

  private void Start() {
    var levelToLoad = levelLoader.Instance().levelDataToLoad;
    // Set any loading screen elements based on the level data
    if (levelToLoad == null) {
      Debug.LogWarning("No level data was found to load, using default level data instead: " + LevelLoaderData.DEFAULT_LEVEL_STR);
      levelToLoad = levelLoader.Instance().levelDataToLoad = LevelLoaderData.LoadLevelData(LevelLoaderData.DEFAULT_LEVEL_STR);
    }
    levelNameText.text = levelToLoad.levelName;

    // Determine all the scenes we need to load asynchronously
    asyncScenesToLoad = levelLoader.LoadLevelSceneAsyncOperations(levelToLoad);
    StartCoroutine(asyncLoadScene());
  }

  /// <summary>
  /// Loads scenes asynchronously and updates the loading screen GUI as it does so.
  /// </summary>
  IEnumerator asyncLoadScene() {
    bool allDone = false;
    while (!allDone) {
      float totalProgress = 0f;
      allDone = true;
      for (int i = 0; i < asyncScenesToLoad.Count; i++) {
        var asyncSceneLoad = asyncScenesToLoad[i];
        totalProgress += asyncSceneLoad.progress;
        allDone &= asyncSceneLoad.isDone;
      }
      totalProgress /= asyncScenesToLoad.Count;
      progressBar.fillAmount = totalProgress;
      yield return new WaitForEndOfFrame();
    }
  }

  private void OnDestroy() {
    // Clean up our resources before heading in to the game
    Resources.UnloadUnusedAssets();
  }
 
}
