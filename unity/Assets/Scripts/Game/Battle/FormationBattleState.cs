using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

/// <summary>
/// Handles the player battle formation setup - where the player chooses and places 
/// their team on the battlefield, just before the battle starts.
/// </summary>
//[CreateAssetMenu]
public class FormationBattleState : BattleState {

  private List<CharacterPlacement> playerPlacements = new List<CharacterPlacement>();
  private List<CharacterPlacement> availablePlayerPlacements = new List<CharacterPlacement>();
  private List<PlacementStatus> playerPlacementStatuses = new List<PlacementStatus>();

  public override IEnumerator EnterEvent(BattleStateMachine battleSM) {
    var terrainGrid = battleSM.TerrainGrid;
    var levelData = terrainGrid.levelData;
    var selectionCaret = battleSM.SelectionCaret;

    // Enable/Disable objects to initialize the state of the game
    selectionCaret.gameObject.SetActive(false);

    // Examine the current level data, figure out what placement looks like
    playerPlacementStatuses.Clear();
    playerPlacements.Clear();
    foreach (var placement in levelData.Placements) {
      if (placement.Team.IsPlayerControlled) { 
        playerPlacements.Add(placement);
        playerPlacementStatuses.Add(new PlacementStatus(placement.Location, placement.Character, placement.Character != null));
      }
    }

    // Add indicator for each player placement
    ToggleLandingIndicators(true, playerPlacements, terrainGrid);

    // Determine what player characters are placed automatically
    availablePlayerPlacements.Clear();
    int numPlayerCharactersPlaced = 0;
    foreach (var placement in playerPlacements) {
      // Placement is considered automatic if there is a character already attached to the placement
      if (placement.Character != null) {
        battleSM.CharacterManager.PlaceCharacter(placement.Location, placement.Character);
        numPlayerCharactersPlaced++;
      }
      else {
        availablePlayerPlacements.Add(placement);
      }
    }

    var infoAndPlacementUI = battleSM.InfoAndPlacementUI;
    if (availablePlayerPlacements.Count > 0 && numPlayerCharactersPlaced < levelData.MaxPlayerPlacements) {
      // Place the selection caret in the level at the first available placement location
      var landing = terrainGrid.GetLanding(availablePlayerPlacements[0]);
      selectionCaret.PlaceCaret(landing);
      selectionCaret.gameObject.SetActive(true);
      infoAndPlacementUI.gameObject.SetActive(true);
      infoAndPlacementUI.Init(playerPlacementStatuses, battleSM.LevelLoader.Instance().playerRoster);
    }
    else {
      // TODO: There's nothing for the player to do in this state, all characters that can be placed have
      // been placed automatically into the battlefield, let the player look around until they confirm that
      // they're ready to start the battle
      infoAndPlacementUI.Init();
    }

    
    yield break;
  }

  public override IEnumerator UpdateEvent(BattleStateMachine battleSM) {
    // If placement is complete then we move on to the next state
    //if (isFinishedPlacement) {
    //  battleSM.SetState(battleSM.CommenceState);
    //  yield break;
    //}
    yield break;
  }

  public override IEnumerator ExitEvent(BattleStateMachine battleSM) {
    // De-indicate all of the placement landings
    ToggleLandingIndicators(false, playerPlacements, battleSM.TerrainGrid);
    
    // Disable UI elements used in placement, leave it to the next state to reenable as needed
    battleSM.SelectionCaret.gameObject.SetActive(false);
    battleSM.InfoAndPlacementUI.gameObject.SetActive(false);

    yield break;
  }

  #region Input Events
  public override void OnSubmitInputEvent(BattleStateMachine battleSM) {
    var selectionCaret = battleSM.SelectionCaret;

    // If there are remaining player character placement locations then we 
    // allow the player to place them up to the number of allowable placements
    var terrainGrid = battleSM.TerrainGrid;
    var infoAndPlacementUI = battleSM.InfoAndPlacementUI;

    // Is the caret on an available placement space?
    var foundPlacement = availablePlayerPlacements.Find(x => terrainGrid.GetLanding(x) == selectionCaret.CurrentLanding);
    if (foundPlacement != null) {

      // If the selection is already active then this means the player is placing a character
      if (selectionCaret.IsSelectionActive && infoAndPlacementUI.IsRosterSelectionActive) {

        // Place the selected character in the level and update their placement status
        var selectedLocation = selectionCaret.CurrentLanding.location;
        var selectedStatus = playerPlacementStatuses.Find(x => x.location == foundPlacement.Location);
        Debug.Assert(selectedStatus != null);

        if (!selectedStatus.isLocked) {
          var characterData = infoAndPlacementUI.GetSelectedCharacter();
          Debug.Assert(characterData != null);

          // If there's already a character placed at this location then we need to replace them
          // and update the placement statuses appropriately
          if (selectedStatus.characterData != characterData) {
            if (selectedStatus.characterData != null) {
              // Remove the previous character from the placement location
              battleSM.CharacterManager.RemoveCharacter(foundPlacement.Location);
              selectedStatus.characterData = null;
            }

            // Another special case to consider is that the player is placing a character that has already
            // been placed elsewhere that isn't locked
            var existingStatus = playerPlacementStatuses.Find(x => x.characterData == characterData);
            if (existingStatus != null) {
              if (existingStatus.isLocked) { return; } // This shouldn't happen
              battleSM.CharacterManager.RemoveCharacter(existingStatus.location);
              existingStatus.characterData = null;
            }

            battleSM.CharacterManager.PlaceCharacter(foundPlacement.Location, characterData);
            selectedStatus.characterData = characterData;
          }
        }

        // Deactivate the selection
        selectionCaret.IsSelectionActive = false;
        infoAndPlacementUI.IsRosterSelectionActive = false;
      }
      else {
        // Active selection is valid, begin the process of allowing the player to choose a character from
        // their roster to place on the selected landing
        selectionCaret.IsSelectionActive = true;
        infoAndPlacementUI.IsRosterSelectionActive = true;
      }
    }
  }

  public override void OnCancelInputEvent(BattleStateMachine battleSM) {
    var selectionCaret = battleSM.SelectionCaret;
    if (!selectionCaret.IsSelectionActive) { return; }
    selectionCaret.IsSelectionActive = false;
    battleSM.InfoAndPlacementUI.IsRosterSelectionActive = false;
  }

  public override void OnRemoveInputEvent(BattleStateMachine battleSM) {
    var selectionCaret = battleSM.SelectionCaret;
    var terrainGrid = battleSM.TerrainGrid;

    // Check to see if the selection caret is on an available placement and whether that placement
    // has a character on it that isn't locked
    var foundPlacement = availablePlayerPlacements.Find(x => terrainGrid.GetLanding(x) == selectionCaret.CurrentLanding);
    if (foundPlacement == null) { return; }
    var foundCharacter = battleSM.CharacterManager.GetCharacterAt(foundPlacement.Location);
    if (foundCharacter == null) { return; }
    var placementStatus = playerPlacementStatuses.Find(x => x.characterData == foundCharacter.CharacterData);
    Debug.Assert(placementStatus != null);
    
    if (battleSM.CharacterManager.RemoveCharacter(foundPlacement.Location)) {
      placementStatus.characterData = null;
    }
    else {
      Debug.Assert(false);
    }

  }
  #endregion


  private void ToggleLandingIndicators(bool toggle, List<CharacterPlacement> placements, TerrainGrid terrainGrid) {
    foreach (var placement in placements) {
      var landing = terrainGrid.GetLanding(placement);
      landing.SetIdleIndicated(toggle);
    }
  }

}