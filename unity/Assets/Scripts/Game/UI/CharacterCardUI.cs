using UnityEngine;
using UnityEngine.UI;
using TMPro;

#pragma warning disable 649

public class CharacterCardUI : MonoBehaviour {
  [SerializeField] private Image portrait;
  [SerializeField] private Image backPanel;
  [SerializeField] private Image greyOutPanel;
  [SerializeField] private TextMeshProUGUI nameText;
  [SerializeField] private TextMeshProUGUI levelText;

  public void SetCharacter(CharacterData character) {
    // TODO: Portrait...
    nameText.text = character.Name;
    levelText.text = character.Level.ToString();
    portrait.sprite = character.Portrait;
  }
  public void SetIsHighlighted(bool isHighlighted) {
    backPanel.gameObject.SetActive(isHighlighted);
  }
  public void SetIsGreyedOut(bool isGreyedOut) {
    greyOutPanel.gameObject.SetActive(isGreyedOut);
  }

}
