using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
public class CharacterFactionData : ScriptableObject {
  [SerializeField] private List<CharacterTeamData> friendlies = new List<CharacterTeamData>();
  [SerializeField] private List<CharacterTeamData> neutrals   = new List<CharacterTeamData>();
  [SerializeField] private List<CharacterTeamData> enemies    = new List<CharacterTeamData>();

  public List<CharacterTeamData> Friendlies { get { return friendlies; } }
  public List<CharacterTeamData> Neutrals { get { return neutrals; } }
  public List<CharacterTeamData> Enemies { get { return enemies; } }
}
*/

/// <summary>
/// Handles the player battle formation setup - where the player chooses and places 
/// their team on the battlefield, just before the battle starts.
/// </summary>
//[CreateAssetMenu]
public class FormationBattleState : BattleState {

  public override IEnumerator EnterEvent(BattleStateMachine battleSM) {
    var terrainGrid = battleSM.TerrainGrid;
    var levelData = terrainGrid.levelData;
    var selectionCaret = battleSM.SelectionCaret;

    // Enable/Disable objects to initialize the state of the game
    selectionCaret.gameObject.SetActive(false);

    // Examine the current level data, figure out what placement looks like


    // ??? var factions = levelData.Factions;
    var playerPlacements = new List<CharacterPlacement>();
    foreach (var placement in levelData.Placements) {
      if (placement.Team.IsPlayerControlled) { 
        playerPlacements.Add(placement);
      }
    }

    // Determine what player characters can be placed automatically
    var remainingPlayerPlacements = new List<CharacterPlacement>();
    foreach (var placement in playerPlacements) {
      // Placement is considered automatic if there is a character already attached to the placement
      if (placement.Character != null) {
        battleSM.SpawnCharacter(placement);
      }
      else {
        remainingPlayerPlacements.Add(placement);
      }
    }

    // If there are remaining player character placement locations then we 
    // allow the player to place them up to the number of allowable placements
    var numPlacedPlayerCharacters = playerPlacements.Count - remainingPlayerPlacements.Count;
    if (remainingPlayerPlacements.Count > 0 && numPlacedPlayerCharacters < levelData.MaxPlayerPlacements) {
      // Place the selection caret in the level at the first available placement location
      var firstLocation = remainingPlayerPlacements[0].Location;
      var terrainCol = terrainGrid.GetTerrainColumn(firstLocation);
      Debug.Assert(terrainCol != null);
      var landing = terrainCol.landings[firstLocation.y];
      Debug.Assert(landing != null);

      selectionCaret.PlaceCaret(landing);
      selectionCaret.gameObject.SetActive(true);

      // TODO: Add functionality for placing and removing characters in the level, based on the player's roster
      // ...
      // add  ... ???.SpawnCharacter(location, character);
      // remove ... ???.UnspawnCharacter(location);
    }

    
    yield break;
  }

  public override IEnumerator UpdateEvent(BattleStateMachine battleSM) {
    yield break;
  }

  public override IEnumerator ExitEvent(BattleStateMachine battleSM) {
    yield break;
  }

}