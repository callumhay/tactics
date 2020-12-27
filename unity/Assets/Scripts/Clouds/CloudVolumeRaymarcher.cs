using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class CloudVolumeRaymarcher : MonoBehaviour {

  public static readonly float CLOUD_HEIGHT = 200;
  public static readonly float REAL_WORLD_TO_GAME_SCALE = CLOUD_HEIGHT / 1828f;
  public static readonly float DIST_TO_HORIZON = 5000 * REAL_WORLD_TO_GAME_SCALE;

  public TerrainGrid terrainGrid;

  // Private members
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  public void initAll() {
    // Create the mesh above the battlefield/terrain
    var aboveContainerSize = new Vector3(5000+2*terrainGrid.xUnitSize(), 500, 5000+2*terrainGrid.zUnitSize());
    var mesh = new Mesh();
    int[] triangles = null;
    Vector3[] vertices = null;
    MeshHelper.BuildCubeData(aboveContainerSize, out triangles, out vertices);
    var translation = terrainGrid.transform.position + new Vector3(-aboveContainerSize.x/2, CLOUD_HEIGHT, -aboveContainerSize.z/2);
    for (int i = 0; i < vertices.Length; i++) { vertices[i] += translation; }
    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;

    // Setup the cloud material
    if (meshRenderer.sharedMaterial) {
      var bounds = mesh.bounds;
      meshRenderer.sharedMaterial.SetVector("boundsMax", bounds.max);
      meshRenderer.sharedMaterial.SetVector("boundsMin", bounds.min);

      var sunLightGO = GameObject.Find("Directional Light");
      var sunLight = sunLightGO.GetComponent<Light>();
      meshRenderer.sharedMaterial.SetVector("sunLightDir", -sunLightGO.transform.forward.normalized);
      meshRenderer.sharedMaterial.SetVector("sunLightColour", sunLight.color);
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
}
