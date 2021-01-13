using System;
using UnityEngine;

/// <summary>
/// Used to update and store where Characters are placed (or can be placed) as well as the
/// nature of their placement in a level at the start of a battle.
/// </summary>
[Serializable]
public class CharacterPlacement {
  // The team the character placed here must belong to
  [SerializeField] private CharacterTeamData team;
  
  // The location in the level (x,z) is the TerrainColumn index, y is the TerrainColumnLanding index
  [SerializeField] private Vector3Int location = new Vector3Int(0,0,0);

  public CharacterTeamData Team { get { return team; } set { team = value; } }
  public Vector3Int Location { get { return location; } }

  public CharacterPlacement(CharacterTeamData _team, Vector3Int _location) {
    team = _team;
    location = _location;
  }
}
