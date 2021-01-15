using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BattleState : ScriptableObject {
  public virtual IEnumerator EnterEvent(BattleStateMachine battleSM) { yield break; }
  public virtual IEnumerator UpdateEvent(BattleStateMachine battleSM) { yield break; }
  public virtual IEnumerator ExitEvent(BattleStateMachine battleSM) { yield break; }

  // Input Events
  public virtual void OnSubmit(BattleStateMachine battleSM) {}
  public virtual void OnCancel(BattleStateMachine battleSM) {}
}
