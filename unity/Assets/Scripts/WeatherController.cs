using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherController : MonoBehaviour {

  public TerrainGrid terrainGrid;
  public WeatherData weather;

  void Start() {
    if (terrainGrid == null) {
      terrainGrid = TerrainGrid.FindTerrainGrid();
    }

    // Disable all of the weather effects and let the WeatherData class enable what it needs
    var rainGO = transform.Find(RainWeatherData.GAME_OBJ_NAME).gameObject;
    rainGO.SetActive(false);

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
