using System;
using System.Collections.Generic;
using UnityEngine;


// The contribution of a material is dependant on how many contributors there are and what their
// contribution values are. The overall contribution is a weighted average of each contribution
// divided by the sum of all contributions. The materials are then blended together based
// on those weights.

[Serializable]
public class NodeMaterialContrib {
  public Material material;
  public float contribution = 0f; // Quantity in [0,1] for the contribution of the material

  public NodeMaterialContrib(Material mat, float contrib) {
    material = mat;
    contribution = contrib;
  }
}

[Serializable]
public class TerrainGridNode {

  public static int maxMaterialsPerNode = 2;

  [NonSerialized]
  public Vector3 position;
  [NonSerialized]
  public Vector3Int gridIndex;
  [NonSerialized]
  public List<Vector3Int> columnIndices = new List<Vector3Int>();

  public float isoVal = 0f;
  public List<NodeMaterialContrib> materials = new List<NodeMaterialContrib>(); // Currently we only allow a maximum of 2 materials on a node

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
