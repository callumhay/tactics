using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherController : MonoBehaviour {

  public TerrainGrid terrainGrid;
  public WeatherData weather;

  //private ParticleSystem rain;

  void Start() {
    if (terrainGrid == null) {
      terrainGrid = TerrainGrid.FindTerrainGrid();
    }

    //rain = transform.Find("Rain").GetComponent<ParticleSystem>();

    if (weather == null) {
      Debug.LogWarning("No WeatherData object is set for the WeatherController.");
      // TODO: Default weather?
    }
    else {
      weather.init(this);
    }
  }

  //void Update() { weather?.update(this); }
}
