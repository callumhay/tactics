using System;
using UnityEngine;

[Serializable]
public class TerrainGridNode {
  public Vector3 position;
  public Vector3Int gridIndex;
  public Vector3Int columnIndex;
  public float isoVal;

  private bool _isTraversalGrounded = false; // Used during terrain traversal to flag whether this is grounded or not
  public bool isTraversalGrounded {
    get { return _isTraversalGrounded; }
    set { _isTraversalGrounded = value || isDefinitelyGrounded(); }
  }

  public TerrainGridNode(in Vector3 gridSpacePos, in Vector3Int gridIdx, in Vector3Int colIdx, float iso=0.0f) {
    position = gridSpacePos;
    gridIndex = gridIdx;
    columnIndex = colIdx;
    isoVal = iso;
  }
  
  public bool isTerrain() { return isoVal > Mathf.Epsilon; }
  public bool isDefinitelyGrounded() { return gridIndex.y == 0 && isTerrain(); }

  public Color editorUnselectedColour(float alpha=0.0f) {
    return new Color(isoVal, isoVal, isoVal, Mathf.Clamp(alpha+isoVal, 0, 1));
  }
}
