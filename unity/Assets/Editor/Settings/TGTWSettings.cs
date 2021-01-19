using System.Collections.Generic;
using UnityEngine;

public class TGTWSettings : ScriptableObject {
  public static readonly string assetPath = "Assets/Editor/Settings/TGTWSettings.asset";

  public enum EditorType  { FreePaintEditor = 0, ColumnEditor = 1, NodeEditor = 2, PlacementEditor = 3 };
  public enum PaintType { Terrain = 0, MaterialsOnly = 1, Water = 2 };
  public enum PaintMode { Projection = 0, Floating = 1 };
  public enum BrushType { Sphere = 0, Cube = 1 };
  
  public EditorType editorType = EditorType.FreePaintEditor;
  public PaintType paintType = PaintType.Terrain;
  public PaintMode paintMode = PaintMode.Projection;
  public BrushType brushType = BrushType.Cube;
  
  public float brushSize = 1.0f;
  public float matPaintIntensity = 0.25f;
  public bool gridSnaping = false;
  public bool showGridOverlay = false;
  public bool groundUpOnly = false;
  public float setLevelValue = 1.0f;
  public Material paintMaterial;
  
  public int columnInsetXAmount = 0;
  public int columnInsetNegXAmount = 0;
  public int columnInsetZAmount = 0;
  public int columnInsetNegZAmount = 0;

  public CharacterData placementCharacterData;
  public int selectedTeamIdx = 0;
  public List<CharacterTeamData> loadedTeams = new List<CharacterTeamData>();

  public bool showTerrainNodes = true;
  public bool showEmptyNodes = true;
  public bool showSurfaceNodes = true;
  public bool showAboveSurfaceNodes = true;
}