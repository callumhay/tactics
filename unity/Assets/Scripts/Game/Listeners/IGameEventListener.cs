using UnityEngine;

public interface IGameEventListener<T> {
  void OnEventFired(T eventData);
}
