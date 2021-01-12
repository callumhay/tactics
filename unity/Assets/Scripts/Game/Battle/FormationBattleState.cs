using System.Collections;


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
public class FormationBattleState : BattleState {

  public override IEnumerator EnterEvent(BattleStateMachine battleSM) {
    // Examine the current level data, figure out what placement looks like
    var terrainGrid = battleSM.TerrainGrid;
    /*
    // ??? var factions = terrainGrid.levelData.Factions;
    var playerPlacements = new List<CharacterPlacement>();
    foreach (var placement in terrainGrid.levelData.Placements) {
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
    // allow the player to place them up to the number of available locations
    // if (remainingPlayerPlacements.Count > 0) {
      // TODO: Place the selection caret in the level, give the player control over it,
      // add functionality for placing and removing characters from the player's roster
      // ...
      // add  ... ???.SpawnCharacter(location, character);
      // remove ... ???.UnspawnCharacter(location);
    //}

    */
    
    yield break;
  }

  /*

  public override IEnumerator Update(BattleStateMachine battleSM) {
    yield break;
  }
  public override IEnumerator Exit(BattleStateMachine battleSM) {
    yield break;
  }


  */



}