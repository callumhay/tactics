using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleSystem : MonoBehaviour {

  protected BattleState state;

  public void setState(BattleState _state) {
    if (state != null) {
      StartCoroutine(state.exit());
    }
    state = _state;
    StartCoroutine(state.enter());
  }

  void Start() {
    setState(new FormationBattleState(this));
  }

  void Update() {
    StartCoroutine(state.update());
  }
}
