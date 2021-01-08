using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JobClassData : ScriptableObject {

  public string jobClassName;
  [Range(1,10)] public int level;

  //public List<SkillData> unlockedSkills;
  //public List<AbilityData> unlockedAbilities;

  // Job Level-up Point Spending
  //public int unspentJobPoints;

}
