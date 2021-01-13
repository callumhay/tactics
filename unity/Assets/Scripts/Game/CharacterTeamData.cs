using UnityEngine;

#pragma warning disable 649

[CreateAssetMenu(fileName="New Character Team", menuName="Tactics/Team")]
public class CharacterTeamData : ScriptableObject {
  
  [SerializeField] private Color placementColour;
  [SerializeField] private bool isPlayerControlled;

  public Color PlacementColour { get { return placementColour; } }
  public bool IsPlayerControlled { get { return isPlayerControlled; } }
}
