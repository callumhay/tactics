using UnityEngine;

#pragma warning disable 649

[CreateAssetMenu(fileName="New Character Team", menuName="Tactics/Team")]
public class CharacterTeamData : ScriptableObject {
  
  [SerializeField] private Color placementColour;

  public Color PlacementColour { get { return placementColour; } }
}
