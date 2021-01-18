using UnityEngine;

#pragma warning disable 649

[CreateAssetMenu(fileName="New Character Team", menuName="Tactics/Team")]
public class CharacterTeamData : ScriptableObject {
  
  [SerializeField] private Color placementColour;
  [SerializeField] private bool isPlayerControlled;

  public Color PlacementColour { get { return placementColour; } }
  public bool IsPlayerControlled { get { return isPlayerControlled; } }
}


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