/*
using System.Collections.Generic;
using UnityEngine;

public class MeshFracturer : MonoBehaviour {
  
  [Range(1,5)]
  public int cutLevels = 3;       // How many levels of recursion happen (tree depth)
  [Range(0.01f,10f)]
  public float breakVelocityMultiplier = 2.0f;

  public bool enableCollisionFracturing = true;

  // Internal state variables used when calculating a fractured mesh
  private bool edgeSet = false;
  private Vector3 edgeVertex = Vector3.zero;
  private Vector2 edgeUV = Vector2.zero;
  private Plane edgePlane = new Plane();

  private void OnCollisionEnter(Collision collision) {
    if (!enableCollisionFracturing) { return; }
    enableCollisionFracturing = false;

    var contacts = new List<ContactPoint>();
    var numContacts = collision.GetContacts(contacts);

    var fractureInfo = new FractureCollisionInfo();
    fractureInfo.impulse = collision.impulse;
    fractureInfo.relativeVelocity = collision.relativeVelocity;

    for (int i = 0; i < numContacts; i++) {
      fractureInfo.avgPoint += contacts[i].point;
      fractureInfo.avgNormal += contacts[i].normal;
    }

    if (numContacts > 0) {
      fractureInfo.avgPoint /= numContacts;
      fractureInfo.avgNormal.Normalize();
      fractureInfo.avgPoint = transform.InverseTransformPoint(fractureInfo.avgPoint);
      fractureInfo.avgNormal = transform.InverseTransformDirection(fractureInfo.avgNormal);
      fractureMesh(fractureInfo);
    }
    else {
      enableCollisionFracturing = true;
    }
  }

  private void fractureMesh(FractureCollisionInfo fractureInfo) {
    var originalMesh = GetComponent<MeshFilter>().mesh;
    originalMesh.RecalculateBounds();

    void randomizePlane(in Bounds partBounds, ref Plane prevFracturePlane) {
      var perpVec = prevFracturePlane.normal.perpendicularTo();
      var nextNormal = Vector3.Cross(prevFracturePlane.normal, prevFracturePlane.normal.perpendicularTo());
      if (nextNormal.sqrMagnitude < 1e-9f) {
        nextNormal = UnityEngine.Random.onUnitSphere;
      }
      else {
        nextNormal = Vector3.Slerp(nextNormal, perpVec, Random.Range(0.0f,1.0f));
      }
      var nextPoint = partBounds.center + new Vector3(
        UnityEngine.Random.Range(0, 0.75f*partBounds.extents.x),
        UnityEngine.Random.Range(0, 0.75f*partBounds.extents.y),
        UnityEngine.Random.Range(0, 0.75f*partBounds.extents.z)
      );
      prevFracturePlane.SetNormalAndPosition(nextNormal, nextPoint);
    }

    // Start by fracturing the root at the given points (counts as the first cut level)
    var rootPart = new FracturedPartMesh(originalMesh);
    var cutPlane = fractureInfo.calcFracturePlane(originalMesh.bounds);
    var parts = new List<FracturedPartMesh>();
    var subParts = new List<FracturedPartMesh>();

    parts.Add(generateFracturedMesh(rootPart, cutPlane, true));
    parts.Add(generateFracturedMesh(rootPart, cutPlane, false));

    for (int i = 1; i < cutLevels; i++) {
      for (int j = 0; j < parts.Count; j++) {
        var currPart = parts[j];
        var bounds = currPart.Bounds;
        var nextPlane = currPart.fracturePlane;

        randomizePlane(bounds, ref nextPlane);
        subParts.Add(generateFracturedMesh(currPart, nextPlane, true));
        subParts.Add(generateFracturedMesh(currPart, nextPlane, false));
      }
      // Swap the two lists
      var temp = parts;
      parts = subParts;
      subParts = temp;
      subParts.Clear();
    }

    var velAmt = (fractureInfo.relativeVelocity.magnitude / Mathf.Max(1,parts.Count));
    for (var i = 0; i < parts.Count; i++) {
      var currPart = parts[i];
      var debris = currPart.MakeTerrainDebris(this);
      if (debris != null) {
        debris.gameObj.GetComponent<Rigidbody>().AddForceAtPosition(
          (currPart.Bounds.center-transform.position).normalized * velAmt * breakVelocityMultiplier, transform.position, ForceMode.VelocityChange
        );
      }
    }

    Destroy(gameObject);
  }

  private FracturedPartMesh generateFracturedMesh(in FracturedPartMesh original, in Plane plane, bool left) {
    var partMesh = new FracturedPartMesh(plane);
    var hasUVs = original.UV.GetLength(0) > 0;
    var ray1 = new Ray();
    var ray2 = new Ray();

    for (var i = 0; i < original.Triangles.Length; i++) {
      var triangles = original.Triangles[i];
      edgeSet = false;

      for (var j = 0; j < triangles.Length; j = j + 3) {
        var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
        var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
        var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

        var sideCount = (sideA ? 1 : 0) + (sideB ? 1 : 0) + (sideC ? 1 : 0);
        if (sideCount == 0) { continue; }
        if (sideCount == 3) {
          var vert1 = original.Vertices[triangles[j]];
          var vert2 = original.Vertices[triangles[j + 1]];
          var vert3 = original.Vertices[triangles[j + 2]];
          var norm1 = original.Normals[triangles[j]];
          var norm2 = original.Normals[triangles[j + 1]];
          var norm3 = original.Normals[triangles[j + 2]];
          if (hasUVs) {
            partMesh.AddTriangle(i, vert1, vert2, vert3, norm1, norm2, norm3,
              original.UV[triangles[j]], original.UV[triangles[j + 1]], original.UV[triangles[j + 2]]);
          }
          else {
            partMesh.AddTriangle(i, vert1, vert2, vert3, norm1, norm2, norm3);
          }
          continue;
        }

        // Cut points
        var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

        ray1.origin = original.Vertices[triangles[j + singleIndex]];
        var dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] - original.Vertices[triangles[j + singleIndex]];
        ray1.direction = dir1;
        plane.Raycast(ray1, out var enter1);
        var lerp1 = enter1 / dir1.magnitude;

        ray2.origin = original.Vertices[triangles[j + singleIndex]];
        var dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] - original.Vertices[triangles[j + singleIndex]];
        ray2.direction = dir2;
        plane.Raycast(ray2, out var enter2);
        var lerp2 = enter2 / dir2.magnitude;

        // The first vertex is the anchor
        var edgeNorm  = left ? plane.normal * -1f : plane.normal;
        var edgeVert1 = ray1.origin + ray1.direction.normalized * enter1;
        var edgeVert2 = ray2.origin + ray2.direction.normalized * enter2;
        if (hasUVs) {
          AddEdge(i, partMesh, edgeNorm, edgeVert1, edgeVert2,
            Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
            Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
        }
        else {
          AddEdge(i, partMesh, edgeNorm, edgeVert1, edgeVert2);
        }

        switch (sideCount) {
          case 1: {
            var vert1 = original.Vertices[triangles[j + singleIndex]];
            var norm1 = original.Normals[triangles[j + singleIndex]];
            var norm2 = Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1);
            var norm3 = Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2);
            if (hasUVs) {
              partMesh.AddTriangle(i, vert1, edgeVert1, edgeVert2, norm1, norm2, norm3, original.UV[triangles[j + singleIndex]],
                Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
            }
            else {
              partMesh.AddTriangle(i, vert1, edgeVert1, edgeVert2, norm1, norm2, norm3);
            }
            break;
          }
          case 2: {
            var vertA2 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]];
            var vertA3 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]];
            var normA1 = Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1);
            var normA2 = original.Normals[triangles[j + ((singleIndex + 1) % 3)]];
            var normA3 = original.Normals[triangles[j + ((singleIndex + 2) % 3)]];
            var normB3 = Vector3.Lerp(original.Normals[triangles[j + singleIndex]], original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2);

            if (hasUVs) {
              var uv1 = Vector2.Lerp(original.UV[triangles[j + singleIndex]], original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1);
              var uvB2 = original.UV[triangles[j + ((singleIndex + 2) % 3)]];
              partMesh.AddTriangle(i, edgeVert1, vertA2, vertA3, normA1, normA2, normA3,
                uv1, original.UV[triangles[j + ((singleIndex + 1) % 3)]], uvB2);
              partMesh.AddTriangle(i, edgeVert1, vertA3, edgeVert2, normA1, normA3, normB3,
                uv1, uvB2, Vector2.Lerp(original.UV[triangles[j + singleIndex]], uvB2, lerp2));
            }
            else {
              partMesh.AddTriangle(i, edgeVert1, vertA2, vertA3, normA1, normA2, normA3);
              partMesh.AddTriangle(i, edgeVert1, vertA3, edgeVert2, normA1, normA3, normB3);
            }
            break;
          }
          default:
            break;
        }
      }
    }
    partMesh.FillArrays();
    return partMesh;
  }

  private void AddEdge(int subMesh, FracturedPartMesh partMesh, 
    Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2 uv1, Vector2 uv2) {
      
    if (!edgeSet) {
      edgeSet = true; edgeVertex = vertex1; edgeUV = uv1;
    }
    else {
      edgePlane.Set3Points(edgeVertex, vertex1, vertex2);
      partMesh.AddTriangle(subMesh, edgeVertex, 
        edgePlane.GetSide(edgeVertex + normal) ? vertex1 : vertex2,
        edgePlane.GetSide(edgeVertex + normal) ? vertex2 : vertex1,
        normal, normal, normal, edgeUV, uv1, uv2);
    }
  }
  private void AddEdge(int subMesh, FracturedPartMesh partMesh, Vector3 normal, Vector3 vertex1, Vector3 vertex2) {
    if (!edgeSet) {
      edgeSet = true; edgeVertex = vertex1;
    }
    else {
      edgePlane.Set3Points(edgeVertex, vertex1, vertex2);
      partMesh.AddTriangle(subMesh, edgeVertex, 
        edgePlane.GetSide(edgeVertex + normal) ? vertex1 : vertex2,
        edgePlane.GetSide(edgeVertex + normal) ? vertex2 : vertex1,
        normal, normal, normal);
    }
  }

  private class FractureCollisionInfo {
    public Vector3 avgPoint = new Vector3(0,0,0);
    public Vector3 avgNormal = new Vector3(0,0,0);
    public Vector3 relativeVelocity;
    public Vector3 impulse;

    public Plane calcFracturePlane(in Bounds meshBounds) {
      // Attempt to find another vector to cross with the normal vector of the collision
      // to find an appropriate fracturing plane
      var otherVec = impulse.normalized;
      var fracNormal = Vector3.Cross(avgNormal, otherVec);
      if (fracNormal.sqrMagnitude < 1e-6f) {
        otherVec = (meshBounds.center - avgPoint);
        if (otherVec.sqrMagnitude < 1e-6f) {
          otherVec = avgNormal.perpendicularTo();
        }
        otherVec.Normalize();
        fracNormal = Vector3.Cross(avgNormal, otherVec);
        if (fracNormal.sqrMagnitude < 1e-6f) {
          // We need to find a better vector to cross with the normal, try each of the corners
          // of the bounding box...
          var foundNormal = false;
          for (int i = 0; i < MarchingCubes.corners.GetLength(0); i++) {
            var currCorner = meshBounds.min + Vector3.Scale(meshBounds.size, MarchingCubes.corners[i]);
            otherVec = currCorner-avgPoint;
            if (otherVec.sqrMagnitude < 1e-6f) { continue; }
            otherVec.Normalize();
            fracNormal = Vector3.Cross(avgNormal, otherVec);
            if (fracNormal.sqrMagnitude < 1e-6f) { continue; }
            foundNormal = true;
            break;
          }
          while (!foundNormal) {
            Debug.Log("Couldn't find any suitable plane normals for fracture, randomly searching...");
            // ... use random vectors until we find something that works
            fracNormal = Vector3.Cross(avgNormal, UnityEngine.Random.onUnitSphere);
            if (fracNormal.sqrMagnitude > 1e-6f) { foundNormal = true; }
          }
        }
      }

      fracNormal.Normalize();
      var ptToCenterVec = (meshBounds.center - avgPoint);
      var ptToCenterMag = Mathf.Max(1e-6f, ptToCenterVec.magnitude);
      var nPtToCenterVec = ptToCenterVec/ptToCenterMag;
      var pointOfFracture = avgPoint + UnityEngine.Random.Range(0, ptToCenterMag) * nPtToCenterVec;
      return new Plane(fracNormal, pointOfFracture);
    }

  }

  private class FracturedPartMesh {

    private List<Vector3> _Verticies = new List<Vector3>();
    private List<Vector3> _Normals = new List<Vector3>();
    private List<List<int>> _Triangles = new List<List<int>>();
    private List<Vector2> _UVs = new List<Vector2>();

    public Vector3[] Vertices;
    public Vector3[] Normals;
    public int[][] Triangles;
    public Vector2[] UV;

    public Bounds Bounds;
    public Plane fracturePlane;

    public FracturedPartMesh(in Plane _plane) {
      Bounds = new Bounds();
      fracturePlane = _plane;
    }
    public FracturedPartMesh(in Mesh mesh) {
      UV = mesh.uv;
      Vertices = mesh.vertices;
      Normals = mesh.normals;
      Triangles = new int[mesh.subMeshCount][];
      for (int i = 0; i < mesh.subMeshCount; i++) {
        Triangles[i] = mesh.GetTriangles(i);
      }
      Bounds = mesh.bounds;
    }

    public void AddTriangle(int submesh, Vector3 vert1, Vector3 vert2, Vector3 vert3, 
      Vector3 normal1, Vector3 normal2, Vector3 normal3, Vector2 uv1, Vector2 uv2, Vector2 uv3) {
      AddTriangle(submesh, vert1, vert2, vert3, normal1, normal2, normal3);
      _UVs.Add(uv1); _UVs.Add(uv2); _UVs.Add(uv3);
    }
    public void AddTriangle(int submesh, Vector3 vert1, Vector3 vert2, Vector3 vert3, 
      Vector3 normal1, Vector3 normal2, Vector3 normal3) {

      if (_Triangles.Count - 1 < submesh) { _Triangles.Add(new List<int>()); }
      _Triangles[submesh].Add(_Verticies.Count); _Verticies.Add(vert1);
      _Triangles[submesh].Add(_Verticies.Count); _Verticies.Add(vert2);
      _Triangles[submesh].Add(_Verticies.Count); _Verticies.Add(vert3);
      _Normals.Add(normal1); _Normals.Add(normal2); _Normals.Add(normal3);
      Bounds.min = Vector3.Min(Bounds.min, vert1);
      Bounds.min = Vector3.Min(Bounds.min, vert2);
      Bounds.min = Vector3.Min(Bounds.min, vert3);
      Bounds.max = Vector3.Max(Bounds.max, vert1);
      Bounds.max = Vector3.Max(Bounds.max, vert2);
      Bounds.max = Vector3.Max(Bounds.max, vert3);
    }

    public void FillArrays() {
      Vertices = _Verticies.ToArray();
      Normals = _Normals.ToArray();
      UV = _UVs.ToArray();
      Triangles = new int[_Triangles.Count][];
      for (var i = 0; i < _Triangles.Count; i++) {
        Triangles[i] = _Triangles[i].ToArray();
      }
    }

    public Mesh MakeMesh(in Mesh originalMesh) {
      var mesh = new Mesh();
      mesh.name = originalMesh.name;
      mesh.vertices = Vertices;
      mesh.normals = Normals;
      mesh.uv = UV;
      for (var i = 0; i < Triangles.Length; i++) { mesh.SetTriangles(Triangles[i], i, true); }
      mesh.RecalculateBounds();
      return mesh;
    }

    public TerrainDebris MakeTerrainDebris(MeshFracturer original) {
      if (Vertices.GetLength(0) == 0 || Triangles.GetLength(0) == 0) { return null; }

      var terrainDebris = new TerrainDebris(original.transform.position);
      var gObj = terrainDebris.gameObj;
      gObj.transform.rotation = original.transform.rotation;
      gObj.transform.localScale = original.transform.localScale;
      gObj.transform.SetParent(original.transform.parent);

      var originalMesh = original.GetComponent<MeshFilter>().mesh;
      var mesh = MakeMesh(originalMesh);
      terrainDebris.setMesh(mesh);

      // Transfer velocity and physical properties to the new object
      var originalRB = original.GetComponent<Rigidbody>();
      var rigidbody = gObj.GetComponent<Rigidbody>();
      rigidbody.velocity = originalRB.velocity;
      rigidbody.angularVelocity = originalRB.angularVelocity;
      
      var meshFracturer = gObj.GetComponent<MeshFracturer>();
      meshFracturer.cutLevels = original.cutLevels;
      meshFracturer.enableCollisionFracturing = false;

      return terrainDebris;
    }

  } // PartMesh

}
*/
