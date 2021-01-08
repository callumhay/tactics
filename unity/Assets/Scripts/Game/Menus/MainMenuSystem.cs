using UnityEngine;

public class MainMenuSystem : MonoBehaviour {

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
