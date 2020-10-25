using UnityEngine;

public class TerrainGridNode {
  public Vector3 position = new Vector3();
  public float isoVal = 0.0f;

  public TerrainGridNode(in Vector3 pos, float iso) {
    position = pos;
    isoVal = iso;
  }
  public Color editorUnselectedColour(float alpha=0.0f) {
    return new Color(isoVal, isoVal, isoVal, Mathf.Clamp(alpha+isoVal, 0, 1));
  }
}
