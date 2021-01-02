using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CloudVolumeRaymarcher : MonoBehaviour {

  public static readonly float CLOUD_CONTAINER_THICKNESS = 500;

  public TerrainGrid terrainGrid;
  public float cloudHeight = 500;
  public int domeLongitudeSlices = 10;
  public int domeLatitudeSlices  = 13;

  // Private members
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  public float realWorldToGameScale() { return cloudHeight / 1828f; }
  public float distToHorizon() { return 5000f * realWorldToGameScale(); }

  public void initAll() {
    // Create a dome that will hold the clouds - horizon to sky across the panorama
    float domeHeight = cloudHeight + CLOUD_CONTAINER_THICKNESS;
    float domeRadius = distToHorizon() + CLOUD_CONTAINER_THICKNESS;
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
    var baseSpeed = Mathf.Lerp(0.1f, 0.35f, weatherData.windIntensity/10f);

    var sharedMaterial = meshRenderer.sharedMaterial;
    sharedMaterial.SetVector("windDir", new Vector3(windDir.x, windDir.y, 0));
    sharedMaterial.SetFloat("baseSpeed", baseSpeed);
    sharedMaterial.SetFloat("detailSpeed", 0.5f*baseSpeed);

    sharedMaterial.SetFloat("cloudScale", 0.4f); // TODO: Cloud type
    sharedMaterial.SetFloat("detailNoiseScale", 2f); // TODO: Cloud type
    //sharedMaterial.SetFloat("detailNoiseWeight", 2f); // TODO: Cloud Type
    
    sharedMaterial.SetFloat("densityMultiplier", weatherData.overcast);
    sharedMaterial.SetFloat("densityOffset", Mathf.Lerp(-8.3f, 8.3f, weatherData.overcast));
  }



}
