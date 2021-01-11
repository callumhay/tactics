using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="RainData", menuName="Tactics/Weather/Rain")]
public class RainWeatherData : WeatherData {

  public static readonly string GAME_OBJ_NAME = "Rain";
  
  private static readonly float LIGHT_RAIN_RATE_PER_SQR_UNIT = 0.5f;
  private static readonly float HEAVY_RAIN_RATEE_PER_SQR_UNIT = 3.5f;
  private static readonly float ABS_MIN_FALL_VEL = 10f;
  private static readonly float ABS_MAX_FALL_VEL = 12.5f; 

  [Range(0,1)] [Tooltip("Intensity of rain (0 is light, 1 is heavy)")]
  public float rainIntensity = 0.5f;

  private GameObject rainGO;
  private ParticleSystem parentParticleSystem;
  private ParticleSystem[] particleSystems;

  public override void init(WeatherController weatherController) {
    if (!weatherController) { return; }
    base.init(weatherController);
    
    var terrain = weatherController.terrainGrid;

    rainGO = weatherController.transform.Find(GAME_OBJ_NAME).gameObject;
    rainGO.transform.position = terrain.transform.position;
    parentParticleSystem = rainGO.GetComponent<ParticleSystem>();
    particleSystems = rainGO.GetComponentsInChildren<ParticleSystem>();

    // Make sure the rain is properly positioned over the terrain
    var unitSize = (Vector3)terrain.UnitSizeVec3();

    var psShapeXZScale = unitSize;
    psShapeXZScale.x += 4*TerrainColumn.SIZE;
    psShapeXZScale.z += 4*TerrainColumn.SIZE;

    var psShapePos = unitSize / 2f;
    psShapePos.y = unitSize.y + TerrainColumn.SIZE*2;

    // Depending on wind intensity we may need to move the emitter and/or have a larger emitter for the particle system
    var windIntensityVec = windDirection() * windIntensity;

    // Conservative estimate of lifetime
    var particleMinLifetime = (new Vector3(windIntensityVec.x, -psShapePos.y, windIntensityVec.y)).magnitude / 6.66f;

    // How far will the rain travel over it's lifetime?
    var rainXZDistOverLife = particleMinLifetime * windIntensityVec;
    psShapePos.x -= rainXZDistOverLife.x*0.5f;
    psShapePos.z -= rainXZDistOverLife.y*0.5f;
    psShapeXZScale.x += rainXZDistOverLife.x;
    psShapeXZScale.z += rainXZDistOverLife.y;

    var rateOverTime = Mathf.Lerp(LIGHT_RAIN_RATE_PER_SQR_UNIT, HEAVY_RAIN_RATEE_PER_SQR_UNIT, rainIntensity)*psShapeXZScale.x*psShapeXZScale.z;
    var maxParticles = (int)(rateOverTime*(particleMinLifetime+0.5));

    var psLengthScale = Mathf.Lerp(2f, 5f, rainIntensity);

    foreach (var particleSystem in particleSystems) {
      // Scale to the emitter to the terrain
      var psShape = particleSystem.shape;
      psShape.enabled = true;
      psShape.scale = new Vector3(psShapeXZScale.x, psShape.scale.y, psShapeXZScale.z);
      psShape.position = psShapePos;

      // The rain will blow in the direction of the wind
      var psVel = particleSystem.velocityOverLifetime;
      psVel.x = new ParticleSystem.MinMaxCurve(windIntensityVec.x*0.75f, windIntensityVec.x);
      psVel.y = new ParticleSystem.MinMaxCurve(-ABS_MIN_FALL_VEL, -ABS_MAX_FALL_VEL);
      psVel.z = new ParticleSystem.MinMaxCurve(windIntensityVec.y*0.75f, windIntensityVec.y);

      // Make sure the spawn rate and lifetime of the rain is enough to cover and hit the terrain
      var psMain = particleSystem.main;
      psMain.startLifetime = particleMinLifetime;
      psMain.maxParticles = maxParticles;
      
      var psEmission = particleSystem.emission;
      psEmission.enabled = true;
      psEmission.rateOverTime = rateOverTime;

      var psRenderer = particleSystem.gameObject.GetComponent<ParticleSystemRenderer>();
      psRenderer.lengthScale = psLengthScale;
    }

    // Reinitialize and resimulate all the particle systems so we can visualize the changes immediately
    var rainParentPS = rainGO.GetComponent<ParticleSystem>();
    rainParentPS.Simulate(2*particleMinLifetime, true, true, true);
    rainParentPS.Play(true);

    rainGO.SetActive(true);
  }

}
