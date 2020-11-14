using UnityEngine;

public class TGTWSettings : ScriptableObject {
  public static string assetPath = "Assets/Editor/Settings/TGTWSettings.asset";

  public enum PaintType { IsoValues = 0, MaterialsOnly = 1 };
  public enum PaintMode { Projection = 0, Floating = 1 };
  public enum BrushType { Sphere = 0, Cube = 1 };
  
  public float brushSize = 1.0f;
  public float matPaintIntensity = 0.25f;
  public PaintType paintType = PaintType.IsoValues;
  public PaintMode paintMode = PaintMode.Projection;
  public BrushType brushType = BrushType.Cube;
  public bool gridSnaping = false;
  public bool showGridOverlay = false;
  public Material paintMaterial;
}