using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable 649
public class MainMenuSystem : MonoBehaviour {

  [SerializeField] private SceneReference battleLoadingScene;

  private void Awake() {
    // Make sure only the root menu is active
    deactivateAllMenus();
    var rootMenu = transform.Find("RootMenu")?.gameObject;
    if (rootMenu) {
      rootMenu.SetActive(true);
    }
    else { 
      Debug.Log("Could not find RootMenu!");
    }
  }

  public void startGame() {
    SceneManager.LoadScene(battleLoadingScene);
  }
  
  public void deactivateAllMenus() {
    var numChildren = transform.childCount;
    for (int i = 0; i < numChildren; i++) {
      var currChildGO = transform.GetChild(i).gameObject;
      currChildGO.SetActive(false);
    }
  }

  public void quitGame() {
    Debug.Log("Quitting game.");
    Application.Quit();
  }
}
