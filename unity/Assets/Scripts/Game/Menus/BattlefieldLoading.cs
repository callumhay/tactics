using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#pragma warning disable 649
public class BattlefieldLoading : MonoBehaviour {

  public static LevelData levelToLoad = null; // Used to load and pass instantiated level data to the loading scene
  
  [SerializeField] private LevelLoaderData levelLoader;
  [SerializeField] private Image progressBar;
  [SerializeField] private TextMeshProUGUI levelNameText;

  // This allows us to load multiple additive scenes so that we can make our scenes more modular
  // by included multiple scene loads which might have functionality shared across multiple levels
  private List<AsyncOperation> asyncScenesToLoad;

  private void Awake() {
    progressBar.fillAmount = 0;
    Resources.UnloadUnusedAssets();
  }

  private void Start() {
    // Set any loading screen elements based on the level data
    if (levelToLoad == null) {
      Debug.LogWarning("The levelToLoad must be set on the " + this.GetType().Name + " before loading its scene, using default level: " + LevelLoaderData.DEFAULT_LEVEL_STR);
      levelToLoad = levelLoader.loadLevelDataInstance(LevelLoaderData.DEFAULT_LEVEL_STR);
    }
    levelNameText.text = levelToLoad.levelName;

    // Determine all the scenes we need to load and load them asynchronously
    asyncScenesToLoad = levelLoader.loadLevelAsyncOperations(levelToLoad);
    StartCoroutine(asyncLoadScene());
  }

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
 
}
