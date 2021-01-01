using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CloudVolumeRaymarcher : MonoBehaviour {

  public static readonly float CLOUD_HEIGHT = 200;
  public static readonly float REAL_WORLD_TO_GAME_SCALE = CLOUD_HEIGHT / 1828f;
  public static readonly float DIST_TO_HORIZON = 5000 * REAL_WORLD_TO_GAME_SCALE;
  public static readonly float CLOUD_CONTAINER_THICKNESS = 500;

  public TerrainGrid terrainGrid;
  public int domeLongitudeSlices = 10;
  public int domeLatitudeSlices  = 13;

  // Private members
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  public void initAll() {
    // Create a dome that will hold the clouds - horizon to sky across the panorama
    float domeHeight = CLOUD_HEIGHT + CLOUD_CONTAINER_THICKNESS;
    float domeRadius = DIST_TO_HORIZON + CLOUD_CONTAINER_THICKNESS;
    var tris = new List<int>();
    var verts = new List<Vector3>();
    var domeSphereData = MeshHelper.BuildDomeData(domeHeight, domeRadius, domeLongitudeSlices, domeLatitudeSlices, tris, verts);

    var translation = terrainGrid.transform.position + new Vector3(terrainGrid.xUnitSize()/2f, 0, terrainGrid.zUnitSize()/2f);
    domeSphereData.center += translation;
    for (int i = 0; i < verts.Count; i++) { verts[i] += translation; }

    var mesh = new Mesh();
    mesh.SetVertices(verts);
    mesh.SetTriangles(tris, 0);
    mesh.Optimize();
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;

    // Setup the cloud material
    if (meshRenderer.sharedMaterial) {

      var innerRadius = domeSphereData.radius-CLOUD_CONTAINER_THICKNESS;
      var bounds = mesh.bounds;

      meshRenderer.sharedMaterial.SetVector("boundsMax", bounds.max);
      meshRenderer.sharedMaterial.SetVector("boundsMin", bounds.min);

      meshRenderer.sharedMaterial.SetFloat("innerRadius", innerRadius);
      meshRenderer.sharedMaterial.SetFloat("outerRadius", domeSphereData.radius);
      meshRenderer.sharedMaterial.SetVector("sphereCenter", domeSphereData.center);

      updateSunLight();
    }
  }

  private void Start() {
    if (terrainGrid == null) {
      terrainGrid = TerrainGrid.FindTerrainGrid();
    }

    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) { meshFilter = gameObject.AddComponent<MeshFilter>(); }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) { meshRenderer = gameObject.AddComponent<MeshRenderer>(); }
    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    meshRenderer.allowOcclusionWhenDynamic = false;
    meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

    initAll();
  }

  private void FixedUpdate() {
    updateSunLight();
  }

  private void OnValidate() {
    Invoke("initAll", 0);
  }

  private void updateSunLight() {
    var sunLightGO = GameObject.Find("Directional Light");
    var sunLight = sunLightGO.GetComponent<Light>();
    meshRenderer.sharedMaterial.SetVector("sunLightDir", -sunLightGO.transform.forward.normalized);
    meshRenderer.sharedMaterial.SetVector("sunLightColour", sunLight.color);
  }

  public void setWeather(WeatherData weatherData) {
    var windDir = weatherData.windDirection();
    meshRenderer.sharedMaterial.SetVector("windDir", new Vector3(windDir.x, windDir.y, 0));
    var baseSpeed = Mathf.Lerp(0.1f, 0.35f, weatherData.windIntensity/10f);
    meshRenderer.sharedMaterial.SetFloat("baseSpeed", baseSpeed);
    meshRenderer.sharedMaterial.SetFloat("detailSpeed", 0.5f*baseSpeed);
  }



}
