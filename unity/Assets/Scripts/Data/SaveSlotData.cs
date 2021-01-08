using System.IO;
using UnityEngine;

#pragma warning disable 414

/// <summary>
/// Represents all of the data required to save a game state and load it back into the game.
/// There are a finite number of save slots that the player can save their game into, the slot is chosen
/// when the player starts a new game or continues from a previously saved game.
/// </summary>
[CreateAssetMenu(fileName="New Save Slot", menuName="Tactics/SaveSlot")]
public class SaveSlotData : ScriptableObject {

  [SerializeField] private string version = "0.01";
  [SerializeField] private int slotNumber = 0;
  [SerializeField] private string currentLevel = "tutorial";

  private string saveFilename() { return "slot" + slotNumber + ".json"; }
  private string saveFilepath() { return Path.Combine(Application.persistentDataPath, saveFilename()); }

  public void save() {
    File.WriteAllText(saveFilepath(), JsonUtility.ToJson(this));
  }

  public void load(int slotNumber) {
    var json = File.ReadAllText(saveFilepath());
    JsonUtility.FromJsonOverwrite(json, this);
  }
}
