using UnityEngine.Events;
using UnityEngine;

public class GameEventListener : MonoBehaviour {
    public GameEvent gameEvent;
    public UnityEvent<GameObject> unityEvent;

    public void OnEventFired() {
      unityEvent?.Invoke(null);
    }
    
    public void OnEventFired(GameObject eventGO) {
      unityEvent?.Invoke(eventGO);
    }

    private void OnEnable() {
      Debug.Assert(gameEvent != null, "Attempting to register a listener with a GameEvent that doesn't exist. Did you forget to call SetActive(false) on the GameObject first?");
      gameEvent += this;
    }

    private void OnDisable() {
      gameEvent -= this;
    }
}
