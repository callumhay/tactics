﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

public class BattleStateMachine : MonoBehaviour {

  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private SquareSelectionCaret selectionCaret;
  [SerializeField] private LevelLoaderData levelLoader;

  //[SerializeField] private StartingCutsceneBattleState startCutsceneState;
  [SerializeField] private FormationBattleState formationState;
  //[SerializeField] private CommenceBattleState commenceState;
  //[SerializeField] private TurnBasedBattleState turnBasedState;

  protected BattleState currentState;

  public TerrainGrid TerrainGrid { get { return terrainGrid; } }
  public SquareSelectionCaret SelectionCaret { get { return selectionCaret; } }
  public LevelLoaderData LevelLoader { get { return levelLoader; } }

  public void Init() {
    SetState(formationState);
  }

  public void SetState(BattleState nextState) {
    if (currentState != null) {
      StartCoroutine(currentState.ExitEvent(this));
    }
    currentState = nextState;
    StartCoroutine(currentState.EnterEvent(this));
  }

  private void Update() {
    StartCoroutine(currentState.UpdateEvent(this));
  }


  public void SpawnCharacter(CharacterPlacement placement) {
     SpawnCharacter(placement.Location, placement.Character); 
  }
  public void SpawnCharacter(Vector3Int location, CharacterData character) {
    Debug.Assert(character != null, "Attempting to spawn a character that doesn't exist!");

    var terrainCol = terrainGrid.GetTerrainColumn(location);
    Debug.Assert(terrainCol != null, "No TerrainColumn found at location " + location + ". Check GameObject initialization ordering!");



  }


}
