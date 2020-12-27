using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunController : MonoBehaviour {
  public enum TimeOfDay {
    Sunrise=0, Morning=10, Noon=75, Afternoon=120, Evening=170, Twilight=185, Night=270
  }

  [SerializeField] private Light sun;
  [SerializeField] private TimeOfDay timeOfDay = TimeOfDay.Noon;
  private float sunAngle;
  private float sunInitialIntensity;

  void Start() {
    if (sun == null) {
      // Try to find the directional light
      var sunGO = GameObject.Find("Directional Light");
      if (sunGO) { 
        sun = sunGO.GetComponent<Light>();
      }
    }
    sunInitialIntensity = sun.intensity;
    updateSun();
  }

  //void Update() {}

  void OnValidate() {
    updateSun();
  }

  private void updateSun() {
    if (sun == null) { Debug.Log("No sun light assigned to SunController."); return; }
    sun.transform.localRotation = Quaternion.Euler((float)timeOfDay, 135, 0);
  }
}
