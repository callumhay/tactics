using System;
using UnityEngine;

/// <summary>
/// Used to update and store where Characters are placed (or can be placed) as well as the
/// nature of their placement in a level at the start of a battle.
/// </summary>
[Serializable]
public class CharacterPlacement {
  // The location in the level (x,z) is the TerrainColumn index, y is the TerrainColumnLanding index
  [SerializeField] private Vector3Int location = new Vector3Int(0,0,0);

  // The team the character placed here must belong to
  [SerializeField] private CharacterTeamData team;

  // The character placed here, if this is null then the placement is made manually by the player before battle
  [SerializeField] private CharacterData character;

  public Vector3Int Location { get { return location; } }
  public CharacterTeamData Team { get { return team; } set { team = value; } }
  public CharacterData Character { get { return character; } set { character = value; } }
  
  public CharacterPlacement(Vector3Int _location, CharacterTeamData _team, CharacterData _character = null) {
    location = _location;
    team = _team;
    character = _character;
  }
}
