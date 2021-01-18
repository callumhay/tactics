using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

[CreateAssetMenu(fileName="New Roster", menuName="Tactics/Player Roster")]
public class PlayerRosterData : ScriptableObject {
  
  [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

  public List<CharacterData> Characters { get { return characters; } }

}
