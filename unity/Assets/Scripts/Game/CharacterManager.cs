using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

/// <summary>
/// Manages the placement, collection, lifetime and overall state of the characters in the current battlefield.
/// </summary>
public class CharacterManager : MonoBehaviour {
  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private GameObject characterPrefab;

  private Dictionary<Vector3Int, Character> characters = new Dictionary<Vector3Int, Character>();


  public Character GetCharacterAt(Vector3Int location) {
    Character result = null;
    characters.TryGetValue(location, out result);
    return result;
  }

  public Character PlaceCharacter(Vector3Int location, CharacterData characterData) {
    {
      // Check to see if there's already a character placed at that location
      Character existingCharacter = null;
      if (characters.TryGetValue(location, out existingCharacter)) {
        return existingCharacter;
      }
    }

    var landing = terrainGrid.GetLanding(location);
    if (!landing) {
      Debug.LogError("Attempting to get " + typeof(TerrainColumnLanding) + " that doesn't exist at location " + location);
      return null;
    }
    
    // Instantiate and initialize the newly placed character, add it to the manager
    var characterGO = Instantiate(characterPrefab, landing.CenterPosition(), Quaternion.identity);
    characterGO.transform.SetParent(transform);
    characterGO.name = characterData.Name;
    var character = characterGO.GetComponent<Character>();
    character.Init(characterData);
    characters[location] = character;

    return character;
  }

  public bool RemoveCharacter(Vector3Int location) {
    Character existingCharacter = null;
    if (characters.TryGetValue(location, out existingCharacter)) {
      Destroy(existingCharacter.gameObject);
      return characters.Remove(location);
    }
    return false;
  }
  /*
  public bool RemoveCharacter(Character character) {
    foreach (var keyVal in characters) {
      if (keyVal.Value == character) {
        characters.Remove(keyVal.Key);
        Destroy(character.gameObject);
        return true;
      }
    }
    return false;
  }
  */
  //public bool MoveCharacter(Vector3Int startingLoc, Vector3Int endingLoc) ...
  //public bool MoveCharacter(Character character, Vector3Int endingLoc) ...


}
