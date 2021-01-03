using UnityEngine;

public class CameraManager : MonoBehaviour {

  public Camera mainCamera;
  public Camera debugCamera;

  private void Start() {
    mainCamera.gameObject.SetActive(true);
    debugCamera.gameObject.SetActive(false);
  }

  private void Update() {
    if (Input.GetKeyDown(KeyCode.C)) {
      mainCamera.gameObject.SetActive(!mainCamera.gameObject.activeSelf);
      debugCamera.gameObject.SetActive(!debugCamera.gameObject.activeSelf);
    }
  }
}
