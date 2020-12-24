using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="RainData", menuName="Tactics/Weather/Rain")]
public class RainWeatherData : WeatherData {

  private static readonly string GAME_OBJ_NAME = "Rain";
  private static readonly float LIGHT_RAIN_RATE_PER_SQR_UNIT = 0.5f;
  private static readonly float HEAVY_RAIN_RATEE_PER_SQR_UNIT = 3.5f;

  [Range(0,1)] [Tooltip("Intensity of rain (0 is light, 1 is heavy)")]
  public float rainIntensity = 0.5f;

  private GameObject rainGO;
  private ParticleSystem[] particleSystems;

  public override void init(WeatherController weatherController) {
    var terrain = weatherController.terrainGrid;

    rainGO = weatherController.transform.Find(GAME_OBJ_NAME).gameObject;
    rainGO.transform.position = terrain.transform.position;
    particleSystems = rainGO.GetComponentsInChildren<ParticleSystem>();

    // Make sure the rain is properly positioned over the terrain
    var unitSize = (Vector3)terrain.unitSizeVec3();

    var particleXZScale = unitSize;
    particleXZScale.x += 4*TerrainColumn.SIZE;
    particleXZScale.z += 4*TerrainColumn.SIZE;

    var particlePos = unitSize / 2f;
    particlePos.y = unitSize.y + TerrainColumn.SIZE*2;

    var particleMinLifetime = particlePos.y / 6.66f;

    var intensityMultiplier = Mathf.Lerp(LIGHT_RAIN_RATE_PER_SQR_UNIT, HEAVY_RAIN_RATEE_PER_SQR_UNIT, rainIntensity);
    var rateOverTime = intensityMultiplier*unitSize.x*unitSize.z;
    var maxParticles = (int)(rateOverTime*(particleMinLifetime+0.5));

    foreach (var particleSystem in particleSystems) {
      var psShape = particleSystem.shape;
      psShape.scale = new Vector3(particleXZScale.x, psShape.scale.y, particleXZScale.z);
      psShape.position = particlePos;

      // Make sure the spawn rate and lifetime of the rain is enough to cover and hit the terrain
      var psMain = particleSystem.main;
      psMain.startLifetime = particleMinLifetime;
      psMain.maxParticles = maxParticles;
      
      var psEmission = particleSystem.emission;
      psEmission.rateOverTime = rateOverTime;
    }

    // Reinitialize and resimulate all the particle systems so we can visualize the changes immediately
    var rainParentPS = rainGO.GetComponent<ParticleSystem>();
    rainParentPS.Simulate(2*particleMinLifetime, true, true, true);
    rainParentPS.Play(true);
  }

  void OnValidate() {
    var weatherComponent = Object.FindObjectOfType<WeatherController>();
    init(weatherComponent);
  }

}
