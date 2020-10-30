using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TerrainGridNode {
  public Vector3 position;
  public Vector3Int gridIndex;
  public List<Vector3Int> columnIndices = new List<Vector3Int>();
  public float isoVal;

  private bool _isTraversalGrounded = false; // Used during terrain traversal to flag whether this is grounded or not
  public bool isTraversalGrounded {
    get { return _isTraversalGrounded; }
    set { _isTraversalGrounded = value || isDefinitelyGrounded(); }
  }

  public TerrainGridNode(in Vector3 gridSpacePos, in Vector3Int gridIdx, float iso=0.0f) {
    position = gridSpacePos;
    gridIndex = gridIdx;
    isoVal = iso;
  }
  
  public bool isTerrain() { return isoVal > Mathf.Epsilon; }
  public bool isDefinitelyGrounded() { return gridIndex.y == 0 && isTerrain(); }

  public Color editorUnselectedColour(float alpha=0.0f) {
    return new Color(isoVal, isoVal, isoVal, Mathf.Clamp(alpha+isoVal, 0, 1));
  }
}
