using System.Collections;

public abstract class BattleState {
  protected BattleSystem battleSystem;

  public BattleState(BattleSystem _battleSystem) {
    battleSystem = _battleSystem;
  }

  public virtual IEnumerator enter() { yield break; }
  public virtual IEnumerator update() { yield break; }
  public virtual IEnumerator exit() { yield break; }
}
