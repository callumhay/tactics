using UnityEngine;

public class TerrainGridNode {
  public Vector3 position;
  public Vector3Int terrainColumnIndex;
  public float isoVal = 0.0f;

  public TerrainGridNode(in Vector3 gridSpacePos, in Vector3Int tcIdx, float iso) {
    position = gridSpacePos;
    terrainColumnIndex = tcIdx;
    isoVal = iso;
  }
  public Color editorUnselectedColour(float alpha=0.0f) {
    return new Color(isoVal, isoVal, isoVal, Mathf.Clamp(alpha+isoVal, 0, 1));
  }
}
