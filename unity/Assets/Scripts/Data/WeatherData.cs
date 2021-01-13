using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeatherData : ScriptableObject {
  [Range(-1,1)] public float windDirectionX = 0;
  [Range(-1,1)] public float windDirectionZ = 0;
  [Range(0,10)] public float windIntensity = 0;

  [Range(0,1)] public float overcast = 0.1f; // Make protected and change in the child classes


  public Vector2 WindDirection() {
    return (new Vector2(windDirectionX,windDirectionZ)).normalized;
  }

  public virtual void InitWeather(WeatherController weatherController) {
    // Update the cloud wind direction to match the weather data
    var cloudComponent = Component.FindObjectOfType<CloudVolumeRaymarcher>();
    cloudComponent.setWeather(this);
  }
  public virtual void UpdateWeather(WeatherController weatherController) {}

  protected void OnValidate() {
    var weatherComponent = Object.FindObjectOfType<WeatherController>();
    InitWeather(weatherComponent);
  }
}
