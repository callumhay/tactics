using UnityEngine;

public class World : MonoBehaviour {

  public static World instance { get; private set;}

  private void Awake() {
    if (instance != null && instance == this) {
      Debug.LogWarning("More than one instance of World found, removing duplicate.");
      GameObject.Destroy(this.gameObject);
    }
    else {
      instance = this;
    }
  }

  private void Start(){
  }

  private void Update() {
  }

  private void FixedUpdate() {
    // NOTE: Time.fixedDeltaTime gives the fixed frame time!!!
  }
}
