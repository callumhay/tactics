using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class DebugNormals : MonoBehaviour {
#if UNITY_EDITOR

  [Range(0.1f,1.0f)]
  public float normalLineLength = 0.25f;
  public Color normalColour = Color.magenta;
  [Range(0.01f, 0.5f)]
  public float vertexRadius = 0.1f;
  public Color vertexColour = Color.yellow;

  private List<Vector3> triCenters;
  private List<Vector3> triNormals;

  private void OnEnable() {
    triCenters = new List<Vector3>();
    triNormals = new List<Vector3>();

    //var mesh = GetComponent<MeshRenderer>();
    


  }

  private void OnDrawGizmos() {
    if (triCenters == null || triNormals == null) { return; }    
    for (int i = 0; i < triCenters.Count; i++) {
      Gizmos.color = vertexColour;
      Gizmos.DrawSphere(triCenters[i], vertexRadius);
      Gizmos.color = normalColour;
      Gizmos.DrawLine(triCenters[i], triCenters[i] + triNormals[i] * normalLineLength);
    }
  }

#endif
}
