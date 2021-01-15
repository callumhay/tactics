using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 649

public class TerrainSharedAssetContainer : MonoBehaviour {
  
  [Header("Prefabs")]
  public GameObject terrainColumnPrefab;
  public GameObject terrainColumnLandingPrefab;
  public GameObject debrisPrefab;

  [Header("Materials")]
  public Material defaultTerrainMaterial;
  public Material triplanar3BlendMaterial;
  [Space]
  public Material indicatorLandingMaterial;
  public Material selectedLandingMaterial;
  public Material activeLandingMaterial;


}
