using UnityEngine;

public class TGTWSettings : ScriptableObject {
  public static string assetPath = "Assets/Editor/Settings/TGTWSettings.asset";

  public enum PaintType { IsoValues = 0 };
  public enum PaintMode { Projection_2D = 0, Freeform_3D = 1 };
  public enum BrushType { Sphere = 0, Cube = 1 };
  
  public float brushSize;
  public PaintType paintType;
  public PaintMode paintMode;
  public BrushType brushType;
}