using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable 649

[CreateAssetMenu(fileName="New Character", menuName="Tactics/Character")]
public class CharacterData : ScriptableObject {

  [SerializeField] private string characterName = string.Empty;
  [SerializeField] private Sprite portrait;
  [SerializeField] [Range(1,99)] private int level = 1;


 

  //[Range(0,99)] public int xp;

  //[Header("Job Classes")]
  //public JobData primaryJob;   // Should never be null in-game
  //public JobData secondaryJob; // Can be null in-game

  //[Header("Primary Stats")]
  //public int strength;
  //public int speed;
  //public int constitution;
  //public int intelligence;

  //[Header("Equipment")]
  //public HelmetData helmet;
  //public ChestArmorData chestArmor;
  //public GlovesData gloves;
  //public BootsData boots;
  //public AccessoryData accessory1;
  //public AccessoryData accessory2;

  // Character Level-up Point Spending
  //public int unspentStatPoints;


  // Derived stats and attributes
  public int primaryStatModifier(int primaryStatVal) { return Mathf.FloorToInt((primaryStatVal-10f)/2f); } 
  //public float physicalEvade() {}
  //public float magicEvade() {}
  //public int movement() {}

  // Resistances
  //public float physicalResist() {}
  //public float fireResist() {}
  //public float waterResist() {}
  //public float iceResist() {}
  //public float windResist() {}
  //public float earthResist() {}
  //public float energyResist() {}
  //public float etherResist() {}

  public string Name { get { return characterName; } }
  public Sprite Portrait { get { return portrait; } }
  public int Level { get { return level; } }

 

  // TODO: For debugging only, remove this eventually
  [SerializeField] private Color colour = new Color(0,0,1,1);
  public Color Colour { get { return colour; } }


  public override string ToString() {
    return Name;
  }
}
