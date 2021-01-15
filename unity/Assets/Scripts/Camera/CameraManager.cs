using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManager : MonoBehaviour {

  public Camera mainCamera;
  public Camera debugCamera;

  private void Start() {
    mainCamera.gameObject.SetActive(true);
    debugCamera.gameObject.SetActive(false);
  }

  public void OnToggleDebugCamera(InputAction.CallbackContext inputContext) {
    mainCamera.gameObject.SetActive(!mainCamera.gameObject.activeSelf);
    debugCamera.gameObject.SetActive(!debugCamera.gameObject.activeSelf);
  }
}
