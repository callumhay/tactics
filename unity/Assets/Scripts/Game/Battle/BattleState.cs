using System.Collections;
using UnityEngine;

public abstract class BattleState : ScriptableObject {
  public virtual IEnumerator EnterEvent(BattleStateMachine battleSM) { yield break; }
  public virtual IEnumerator UpdateEvent(BattleStateMachine battleSM) { yield break; }
  public virtual IEnumerator ExitEvent(BattleStateMachine battleSM) { yield break; }
}
