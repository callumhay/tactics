using System.Collections;

/// <summary>
/// Handles the player battle formation setup - where the player chooses and places 
/// their team on the battlefield, just before the battle starts.
/// </summary>
public class FormationBattleState : BattleState {
  
  public FormationBattleState(BattleSystem _battleSystem) : base(_battleSystem) {
  }

  /*
  public override IEnumerator enter() { 
    yield break;
  }
  public override IEnumerator update() {
    yield break;
  }
  public override IEnumerator exit() {
    yield break;
  }
  */
}