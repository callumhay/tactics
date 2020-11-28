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
  public static readonly int maxMaterialsPerNode = 2;

  [NonSerialized]
  public Vector3 position;
  [NonSerialized]
  public Vector3Int gridIndex;
  [NonSerialized]
  public List<Vector3Int> columnIndices = new List<Vector3Int>();

  public float isoVal = 0f;    // How much terrain is contained in this node (via Marching Cubes)
  public float liquidVol = 0f; // How much liquid is contained in this node (Liters)
  public List<NodeMaterialContrib> materials = new List<NodeMaterialContrib>(); // Currently we only allow a maximum of 2 materials on a node

  private bool _isTraversalGrounded = false; // Used during terrain traversal to flag whether this is grounded or not
  public bool isTraversalGrounded {
    get { return _isTraversalGrounded; }
    set { _isTraversalGrounded = value || isDefinitelyGrounded(); }
  }

  public TerrainGridNode(in Vector3 gridSpacePos, in Vector3Int gridIdx, float _isoVal=0f, float _liquidVol=0f) {
    position = gridSpacePos;
    gridIndex = gridIdx;
    isoVal = _isoVal;
    liquidVol = _liquidVol;

    Debug.Assert(isoVal > 0 && liquidVol == 0 || isoVal == 0 && liquidVol > 0 || isoVal == 0 && liquidVol == 0, 
      "You can't have a node that's both land and liquid!");
  }
  
  public bool isTerrain() { return isoVal > Mathf.Epsilon; }
  public bool isDefinitelyGrounded() { return gridIndex.y == 0 && isTerrain(); }

  public Color editorUnselectedColour(float alpha=0.0f) {
    return new Color(isoVal, isoVal, isoVal, Mathf.Clamp(alpha+isoVal, 0, 1));
  }
}
