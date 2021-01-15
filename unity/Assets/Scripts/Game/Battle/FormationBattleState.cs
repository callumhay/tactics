using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

  private List<CharacterPlacement> playerPlacements = new List<CharacterPlacement>();
  private List<CharacterPlacement> availablePlayerPlacements = new List<CharacterPlacement>();
  private bool isFinishedPlacement = false;

  public override IEnumerator EnterEvent(BattleStateMachine battleSM) {
    var terrainGrid = battleSM.TerrainGrid;
    var levelData = terrainGrid.levelData;
    var selectionCaret = battleSM.SelectionCaret;

    // Enable/Disable objects to initialize the state of the game
    selectionCaret.gameObject.SetActive(false);

    // Examine the current level data, figure out what placement looks like
    playerPlacements.Clear();
    foreach (var placement in levelData.Placements) {
      if (placement.Team.IsPlayerControlled) { 
        playerPlacements.Add(placement);
      }
    }

    // Add indicator for each player placement
    ToggleLandingIndicators(true, playerPlacements, terrainGrid);

    
    var rosterStatuses = new Dictionary<CharacterData, PlacementStatus>();
    /*
    var roster = battleSM.LevelLoader.Instance().playerRoster;
    foreach (var character in roster.Characters) {
      rosterStatuses[character] = new PlacementStatus(character);
    }
    */

    // Determine what player characters can be placed automatically
    availablePlayerPlacements.Clear();
    PlacementStatus placementStatus = null;
    foreach (var placement in playerPlacements) {
      // Placement is considered automatic if there is a character already attached to the placement
      if (placement.Character != null) {
        if (rosterStatuses.TryGetValue(placement.Character, out placementStatus)) {
          placementStatus.hasBeenPlaced = true;
          placementStatus.isLocked = true;
          battleSM.SpawnCharacter(placement);
        }
        else {
          Debug.LogWarning("Player character placement found that isn't in the roster: " + placement.Character);
        }
      }
      else {
        availablePlayerPlacements.Add(placement);
      }
    }

    var numPlacedPlayerCharacters = playerPlacements.Count - availablePlayerPlacements.Count;
    if (availablePlayerPlacements.Count > 0 && numPlacedPlayerCharacters < levelData.MaxPlayerPlacements) {
      // Place the selection caret in the level at the first available placement location
      var landing = terrainGrid.GetLanding(availablePlayerPlacements[0]);
      selectionCaret.PlaceCaret(landing);
      selectionCaret.gameObject.SetActive(true);
    }

    isFinishedPlacement = false;
    yield break;
  }

  public override IEnumerator UpdateEvent(BattleStateMachine battleSM) {
    // If placement is complete then we move on to the next state
    if (isFinishedPlacement) {
      battleSM.SetState(battleSM.CommenceState);
      yield break;
    }

    //var terrainGrid = battleSM.TerrainGrid;



    // TODO: Add functionality for placing and removing characters in the level, based on the player's roster
    // ...
    // add  ... ???.SpawnCharacter(location, character);
    // remove ... ???.UnspawnCharacter(location);
  }

  public override IEnumerator ExitEvent(BattleStateMachine battleSM) {
    // De-indicate all of the placement landings
    ToggleLandingIndicators(false, playerPlacements, battleSM.TerrainGrid);

    yield break;
  }

  public override void OnSubmit(BattleStateMachine battleSM) {
    var selectionCaret = battleSM.SelectionCaret;
    if (selectionCaret.IsSelectionActive) { return; }

    // If there are remaining player character placement locations then we 
    // allow the player to place them up to the number of allowable placements
    var terrainGrid = battleSM.TerrainGrid;
    // Is the caret on an available placement space?
    var availableLandings = new HashSet<TerrainColumnLanding>();
    foreach (var placement in availablePlayerPlacements) {
      availableLandings.Add(terrainGrid.GetLanding(placement));
    }

    if (availableLandings.Contains(selectionCaret.CurrentLanding)) {
      // Active selection is valid, begin the process of allowing the player to choose a character from
      // their roster to place on the selected landing
      selectionCaret.IsSelectionActive = true;
      
    }
  }

  public override void OnCancel(BattleStateMachine battleSM) {
    var selectionCaret = battleSM.SelectionCaret;
    if (!selectionCaret.IsSelectionActive) { return; }
    selectionCaret.IsSelectionActive = false;
  }

  private void ToggleLandingIndicators(bool toggle, List<CharacterPlacement> placements, TerrainGrid terrainGrid) {
    foreach (var placement in placements) {
      var landing = terrainGrid.GetLanding(placement);
      landing.SetIdleIndicated(toggle);
    }
  }

}