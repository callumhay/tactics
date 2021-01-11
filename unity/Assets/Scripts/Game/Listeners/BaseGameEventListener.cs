using UnityEngine;
using UnityEngine.Events;
#pragma warning disable 649

public abstract class BaseGameEventListener<T, E> : MonoBehaviour, IGameEventListener<T> where E : BaseGameEvent<T> {

  [SerializeField] private E gameEvent;
  [SerializeField] private UnityEvent<T> unityEvent;

  public void OnEventFired(T eventData) {
    unityEvent?.Invoke(eventData);
  }

  private void OnEnable() {
    Debug.Assert(gameEvent != null, "Attempting to register a listener with a GameEvent that doesn't exist.\n" +
        "Did you forget to call SetActive(false) on the GameObject first?");
    gameEvent?.RegisterListener(this);
  }

  private void OnDisable() {
    gameEvent?.UnregisterListener(this);
  }

}
