using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649
[CreateAssetMenu]
public class PlayerRosterData : ScriptableObject {
  
  [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

  public List<CharacterData> Characters { get { return characters; } }

}
