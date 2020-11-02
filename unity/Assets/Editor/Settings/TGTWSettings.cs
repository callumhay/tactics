using UnityEngine;

public class TGTWSettings : ScriptableObject {
  public static string assetPath = "Assets/Editor/Settings/TGTWSettings.asset";

  public enum PaintType { IsoValues = 0 };
  public enum BrushType { Sphere = 0, Cube = 1 };

  public float brushSize;
  public PaintType paintType;
  public BrushType brushType;
}