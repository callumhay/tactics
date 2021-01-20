using UnityEngine;
using TMPro;

#pragma warning disable 649

public class RemainingPlacementUI : MonoBehaviour {
  [SerializeField] private TextMeshProUGUI numPlacedText;
  [SerializeField] private TextMeshProUGUI maxPlacementsText;

  public void SetNumPlaced(int numPlaced) {
    Debug.Assert(numPlaced >= 0);
    numPlacedText.text = numPlaced.ToString("D2");
  }
  public void SetMaxPlacements(int maxPlacements) {
    Debug.Assert(maxPlacements >= 0);
    maxPlacementsText.text = maxPlacements.ToString("D2");
  }


}
