using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class BattlefieldLoading : MonoBehaviour {

  //public static string levelDataToLoad = "";
  [SerializeField] private Image progressBar = default;
  [SerializeField] private TextMeshProUGUI levelNameText = default;

  // This allows us to load multiple additive scenes so that we can make our scenes more modular
  // by included multiple scene loads which might have functionality shared across multiple levels
  private List<AsyncOperation> asyncScenesToLoad = new List<AsyncOperation>();

  private void Awake() {
    progressBar.fillAmount = 0;

    // TODO: Change this so that it loads the scene from a database or something else and not the incremented index!
    var currSceneBuildIdx = SceneManager.GetActiveScene().buildIndex;
    asyncScenesToLoad.Add(SceneManager.LoadSceneAsync(currSceneBuildIdx + 1)); // Load the battlefield scene itself 

    // TODO: Add scene modules here e.g., "Gameplay", "Environment Manager", etc.
    //asyncScenesToLoad.Add(SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Additive));
  }

  private void Start() {
    StartCoroutine(asyncLoadScene());
  }

  IEnumerator asyncLoadScene() {
    float totalProgress = 0f;
    for (int i = 0; i < asyncScenesToLoad.Count; i++) {
      var asyncSceneLoad = asyncScenesToLoad[i];
      totalProgress += asyncSceneLoad.progress;
    }
    totalProgress /= asyncScenesToLoad.Count;
    while (totalProgress < 1f) {
      progressBar.fillAmount = totalProgress;
      yield return new WaitForEndOfFrame();
    }
    progressBar.fillAmount = 1f;
  }
 
}
