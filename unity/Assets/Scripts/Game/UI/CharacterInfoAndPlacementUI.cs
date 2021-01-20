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
  [SerializeField] private CharacterCardUI displayedCharacterCardUI;
  [SerializeField] private RemainingPlacementUI remainingPlacementUI;

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

  public void Init(List<PlacementStatus> initialPlacementStatuses=null, PlayerRosterData roster=null, int maxPlacements=0) {
    placementStatuses = initialPlacementStatuses ?? new List<PlacementStatus>();
    playerRoster = roster;
    remainingPlacementUI.SetMaxPlacements(maxPlacements);
    SetSelectedIndex(0);
    Refresh();
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

  public void Refresh() {
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
        if (selectedIndex == INVALID_SELECTED_IDX) { SetSelectedIndexToNextAvailable(); }
        else { ShowCharacterAtSelectedIndex(); }
      }
    }

    if (IsPlacementEnabled) {
      int numPlaced = 0;
      foreach (var placement in placementStatuses) {
        if (placement.characterData != null) { numPlaced++; }
      }
      remainingPlacementUI.SetNumPlaced(numPlaced);
      remainingPlacementUI.gameObject.SetActive(true);
    }
    else {
      remainingPlacementUI.gameObject.SetActive(false);
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
  private void SetSelectedIndexToNextAvailable() {
    var index = INVALID_SELECTED_IDX;

    // Determine what characters have already been placed
    var alreadyPlacedCharacters = new HashSet<CharacterData>();
    foreach (var status in placementStatuses) {
      if (status.characterData != null) { alreadyPlacedCharacters.Add(status.characterData); }
    }

    // Find the next unplaced character index starting at the currently selected index
    var startingIdx = Mathf.Max(0,selectedIndex);
    for (int i = startingIdx; i < (startingIdx + playerRoster.Characters.Count); i++) {
      var currIdx = i % playerRoster.Characters.Count;
      var currCharacter = playerRoster.Characters[currIdx];
      if (!alreadyPlacedCharacters.Contains(currCharacter)) {
        index = currIdx;
        break;
      }
    }

    SetSelectedIndex(index);
  }

  private void ShowCharacterAtSelectedIndex() {
    if (!IsPlacementEnabled) { return; }
    if (selectedIndex == INVALID_SELECTED_IDX) { 
      displayedCharacterCardUI.gameObject.SetActive(false);
      return;
    }
    ShowCharacter(playerRoster.Characters[selectedIndex]);
  }

  private void ShowCharacter(CharacterData characterData) {
    if (!characterData) { 
      displayedCharacterCardUI.gameObject.SetActive(false);
      return;
    }
    displayedCharacterCardUI.SetCharacter(characterData);
    displayedCharacterCardUI.SetIsHighlighted(IsPlacementEnabled && isRosterSelectionActive);
    displayedCharacterCardUI.SetIsGreyedOut(placementStatuses.Find(x => x.characterData == characterData) != null);
    displayedCharacterCardUI.gameObject.SetActive(true);
  }

  private void Awake() {
    gameObject.SetActive(false);
  }
  private void OnEnable() {
    OnCaretMovedEvent(selectionCaret.gameObject);
  }

}
