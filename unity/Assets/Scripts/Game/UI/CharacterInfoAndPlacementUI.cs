using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 649

[Serializable]
public class PlacementStatus {
  
  public Vector3Int location;
  public CharacterData characterData;
  public bool isLocked;

  public PlacementStatus(Vector3Int loc, CharacterData c=null, bool locked=false) {
    location = loc;
    characterData = c;
    isLocked = locked;
  }
  public bool HasBeenPlaced() { return characterData != null; }
}

public class CharacterInfoAndPlacementUI : MonoBehaviour {
  private static readonly int INVALID_SELECTED_IDX = -1;

  [Header("Required Game Objects")]
  [SerializeField] private CharacterManager characterManager;
  [SerializeField] private SquareSelectionCaret selectionCaret;
  [SerializeField] private CharacterCardUI displayedCharacterCard;

  [Header("UI State Info")]
  [SerializeField] private List<PlacementStatus> placementStatuses = new List<PlacementStatus>();
  [SerializeField] private PlayerRosterData playerRoster;


  private int selectedIndex = INVALID_SELECTED_IDX;
  private bool isRosterSelectionActive = false;

  public List<PlacementStatus> PlacementStatuses { get { return placementStatuses; } }
  
  public bool IsPlacementEnabled { get { return placementStatuses.Count > 0; } }

  public bool IsRosterSelectionActive {
    get { return isRosterSelectionActive; }
    set {
      bool prevValue = isRosterSelectionActive;
      isRosterSelectionActive = value && IsPlacementEnabled;
      if (prevValue != isRosterSelectionActive) { Refresh(); }
    }
  }

  public CharacterData GetSelectedCharacter() {
    if (selectedIndex < 0) { return null; }
    return playerRoster.Characters[selectedIndex];
  }

  public void Init(List<PlacementStatus> initialPlacementStatuses=null, PlayerRosterData roster=null) {
    placementStatuses = initialPlacementStatuses ?? new List<PlacementStatus>();
    playerRoster = roster;
    SetSelectedIndex(0);
  }

  public void OnCaretMovedEvent(GameObject _) { Refresh(); }

  #region InputEvents
  public void OnNextCharacter(InputAction.CallbackContext inputContext) {
    if (!IsRosterSelectionActive || !inputContext.performed) { return; }
    SetSelectedIndex((selectedIndex+1) % playerRoster.Characters.Count);
  }

  public void OnPrevCharacter(InputAction.CallbackContext inputContext) {
    if (!IsRosterSelectionActive || !inputContext.performed) { return; }
    SetSelectedIndex((selectedIndex-1+playerRoster.Characters.Count) % playerRoster.Characters.Count);
  }
  #endregion

  private void Refresh() {
    var selectedLanding = selectionCaret.CurrentLanding;
    if (!selectedLanding) {
      ShowCharacter(null);
      return;
    }
    
    // Figure out if there are any characters on the current landing and then display their information
    var character = characterManager.GetCharacterAt(selectedLanding.location);
    if (character) {
      ShowCharacter(character.CharacterData);
    }
    else if (IsPlacementEnabled) {
      // When placement is enabled we determine whether the current landing is a placement location
      // if it is then show the current index of the player roster (unactivated)
      var idx = placementStatuses.FindIndex(x => x.location == selectedLanding.location);
      if (idx < 0) { SetSelectedIndex(INVALID_SELECTED_IDX); }
      else {
        if (selectedIndex == INVALID_SELECTED_IDX) { SetSelectedIndex(0); }
        else { ShowCharacterAtSelectedIndex(); }
      }
    }
  }

  private void SetSelectedIndex(int value) {
    if (placementStatuses.Count == 0 || value == INVALID_SELECTED_IDX) {
      selectedIndex = INVALID_SELECTED_IDX;
    }
    else {
      selectedIndex = Mathf.Clamp(value, 0, playerRoster.Characters.Count);
    }
    ShowCharacterAtSelectedIndex();
  }

  private void ShowCharacterAtSelectedIndex() {
    if (!IsPlacementEnabled) { return; }
    if (selectedIndex == INVALID_SELECTED_IDX) { 
      displayedCharacterCard.gameObject.SetActive(false);
      return;
    }
    ShowCharacter(playerRoster.Characters[selectedIndex]);
  }

  private void ShowCharacter(CharacterData character) {
    if (!character) { 
      displayedCharacterCard.gameObject.SetActive(false);
      return;
    }
    displayedCharacterCard.SetCharacter(character);
    displayedCharacterCard.SetIsHighlighted(IsPlacementEnabled && isRosterSelectionActive);
    displayedCharacterCard.gameObject.SetActive(true);
  }

  private void Awake() {
    gameObject.SetActive(false);
  }
  private void OnEnable() {
    OnCaretMovedEvent(selectionCaret.gameObject);
  }

}
