using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DebugDomeMesh : MonoBehaviour {

  public float domeHeight = 10.0f;
  public float domeRadius = 20.0f;
  [Range(2,100)] public int longitudeSlices = 5;
  [Range(2,100)] public int latitudeSlices = 10;


  private MeshRenderer meshRenderer;
  private MeshFilter meshFilter;

  private void rebuildMesh() {
    var tris = new List<int>();
    var verts = new List<Vector3>();
    MeshHelper.BuildDomeData(domeHeight, domeRadius, longitudeSlices, latitudeSlices, tris, verts);

    var mesh = new Mesh();
    mesh.SetVertices(verts);
    mesh.SetTriangles(tris, 0);
    mesh.Optimize();
    mesh.RecalculateBounds();

    meshFilter.sharedMesh = mesh;
  }

  void Start() {
    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) { meshFilter = gameObject.AddComponent<MeshFilter>(); }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) { meshRenderer = gameObject.AddComponent<MeshRenderer>(); }
    
    rebuildMesh();
  }

  private void OnValidate() {
    Invoke("rebuildMesh", 0);
  }

}
