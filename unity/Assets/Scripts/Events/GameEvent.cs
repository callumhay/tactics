using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tactics/Events/Basic Game Event", order = 2)]
public class GameEvent : ScriptableObject {
  public static readonly string DEBRIS_FELL_OFF_EVENT = "Events/DebrisFellOffTerrainEvent";
  public static readonly string DEBRIS_SLEEP_EVENT    = "Events/DebrisSleepEvent";

  private List<GameEventListener> subscribers = new List<GameEventListener>();

  public void FireEvent() {
    for (int i = 0; i < subscribers.Count; ++i) {
      subscribers[i].OnEventFired();
    }
  }

  public void FireEvent(GameObject eventGO) {
    for (int i = 0; i < subscribers.Count; ++i) {
      subscribers[i].OnEventFired(eventGO);
    }
  }

  public static GameEvent operator+(GameEvent evt, GameEventListener sub) {
    evt.subscribers.Add(sub);
    return evt;
  }

  public static GameEvent operator-(GameEvent evt, GameEventListener sub) {
    evt.subscribers.Remove(sub);
    return evt;
  }
}
