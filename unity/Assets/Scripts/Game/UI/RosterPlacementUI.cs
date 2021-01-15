using System;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

[Serializable]
public class PlacementStatus {
  
  public CharacterData character;
  public bool hasBeenPlaced;
  public bool isLocked;

  public PlacementStatus(CharacterData c, bool placed=false, bool locked=false) {
    character = c;
    hasBeenPlaced = placed;
    isLocked = locked;
  }
}

public class RosterPlacementUI : MonoBehaviour {

  [SerializeField] private CharacterCardUI displayedCharacterCard;

  [SerializeField]
  [Tooltip("List of characters that can/can't be placed")] 
  private List<PlacementStatus> placementStatuses = new List<PlacementStatus>();
  
  [SerializeField]
  [Tooltip("The currently selected placement/character index to display")] 
  private int selectedIndex = -1;


  public void Init(List<PlacementStatus> initialPlacementStatuses) {
    placementStatuses = initialPlacementStatuses;
    SetSelectedIndex(0);
  }

  public void SelectCharacter(CharacterData character) {
    for (int i = 0; i < placementStatuses.Count; i++) {
      var status = placementStatuses[i];
      if (status.character == character) {
        SetSelectedIndex(i);
        break;
      }
    }
  }

  private void SetSelectedIndex(int value) {
    var prevIdx = selectedIndex;
    selectedIndex = Mathf.Clamp(value, 0, placementStatuses.Count);
    if (placementStatuses.Count > 0) {
      ShowCharacter(selectedIndex);
    }
    else {
      selectedIndex = -1;
      gameObject.SetActive(false);
    }
  }

  private void ShowCharacter(int index) {
    var currInfo = placementStatuses[index];
    displayedCharacterCard.SetCharacter(currInfo.character);
  }

}
