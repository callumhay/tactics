using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherController : MonoBehaviour {

  public TerrainGrid terrainGrid;
  public WeatherData weather;

  //private ParticleSystem rain;

  void Start() {
    if (terrainGrid == null) {
      var terrainGO = GameObject.Find(TerrainGrid.GAME_OBJ_NAME);
      if (terrainGO) { terrainGrid = terrainGO.GetComponent<TerrainGrid>(); }
      else {
        Debug.LogError("No TerrainGrid was found in WeatherController.");
        return;
      }
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
