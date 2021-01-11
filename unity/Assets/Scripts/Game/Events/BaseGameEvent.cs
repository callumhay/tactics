using System.Collections.Generic;
using UnityEngine;

public abstract class BaseGameEvent<T> : ScriptableObject {
  private readonly List<IGameEventListener<T>> eventListeners = new List<IGameEventListener<T>>();

  public void FireEvent(T eventData) {
    for (int i = eventListeners.Count-1; i >= 0; i--) {
      eventListeners[i].OnEventFired(eventData);
    }
  }

  public void RegisterListener(IGameEventListener<T> listener) {
    if (!eventListeners.Contains(listener)) {
      eventListeners.Add(listener);
    }
  }

  public void UnregisterListener(IGameEventListener<T> listener) {
    eventListeners.Remove(listener);
  }

}
