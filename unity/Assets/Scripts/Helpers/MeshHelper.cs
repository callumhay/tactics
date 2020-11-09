using System;
using System.Collections.Generic;
using UnityEngine;

public static class MeshHelper {
  public static void BuildCubeData(in Vector3 pos, in Vector3 size, out int[] tris, out Vector3[] vertices) {
    tris = new int[3*12]{
      0, 2, 1, //face front
      0, 3, 2,
      4, 5, 6, //face top
      4, 6, 7,
      8, 9, 10, //face right
      8, 10, 11,
      12, 13, 14, //face left
      12, 14, 15,
      16, 17, 18, //face back
      16, 18, 19,
      20, 21, 22, //face bottom
      20, 23, 21
    };
    var xSize = size.x; var ySize = size.y; var zSize = size.z;
    vertices = new Vector3[24] {
      new Vector3 (0, 0, 0),
      new Vector3 (xSize, 0, 0),
      new Vector3 (xSize, ySize, 0),
      new Vector3 (0, ySize, 0),

      new Vector3 (xSize, ySize, 0),
      new Vector3 (0, ySize, 0),
      new Vector3 (0, ySize, zSize),
      new Vector3 (xSize, ySize, zSize),
      
      new Vector3 (xSize, 0, 0),
      new Vector3 (xSize, ySize, 0),
      new Vector3 (xSize, ySize, zSize),
      new Vector3 (xSize, 0, zSize),

      new Vector3 (0, 0, 0),
      new Vector3 (0, 0, zSize),
      new Vector3 (0, ySize, zSize),
      new Vector3 (0, ySize, 0),

      new Vector3 (xSize, ySize, zSize),
      new Vector3 (0, ySize, zSize),
      new Vector3 (0, 0, zSize),
      new Vector3 (xSize, 0, zSize),

      new Vector3 (0, 0, 0),
      new Vector3 (xSize, 0, zSize),
      new Vector3 (0, 0, zSize),
      new Vector3 (xSize, 0, 0),
    };
  }

  private class Face {
    public int[] indices;
    public bool isRemoved = false;
  }

  private class FaceEntry {
    public Face face;
    public int index;
  }

  private class VertexEntry {
    public Vector3 vertex;
    public Vector3 uv;
    public List<Vector3> normals = new List<Vector3>();
    public List<FaceEntry> faces = new List<FaceEntry>();
    public bool isRemoved = false;

    // Vertex/normal indices in the final mesh list
    public List<int> smoothedNormalIndices = new List<int>(); 
  };

  private class NormalGroup {
    public List<int> normalIndices = new List<int>();
    public Vector3 avgNormal = new Vector3(0,0,0);
  }

  public static float defaultSmoothingAngle = 62.5f;
  public static float defaultTolerance = 1e-4f;

  public static void RecalculateNormals(this Mesh mesh, float smoothingAngle, float tolerance) {
    MeshHelper.RecalculateNormals(mesh, smoothingAngle, tolerance, 
      new Vector2(float.MinValue, float.MinValue),
      new Vector2(float.MaxValue, float.MaxValue)
    );
  }

  public static void RecalculateNormals(
    this Mesh mesh, float smoothingAngle, float tolerance, in Vector2 minXZ, in Vector2 maxXZ
  ) {

    var decimalShift = Mathf.Log10(1.0f / tolerance);
    var shiftMultiplier = Mathf.Pow(10, decimalShift);

    string makeValueHash(float value) {
      return (value * shiftMultiplier).ToString();
    }
    string makeVertexHash(in Vector3 vertex) {
      return makeValueHash(vertex.x) + "," + makeValueHash(vertex.y) + "," + makeValueHash(vertex.z);
    }

    var vertices = mesh.vertices;
    var uvs = new List<Vector3>();
    mesh.GetUVs(0, uvs);
    while (uvs.Count < vertices.Length) {
      uvs.Add(new Vector3(0,0,0));
    }

    var vertexMaps = new Dictionary<string, VertexEntry>[mesh.subMeshCount]; // Mappings for each submesh
    for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
      var vertexMap = new Dictionary<string, VertexEntry>();
      vertexMaps[subMeshIndex] = vertexMap;

      var triangles = mesh.GetTriangles(subMeshIndex);
      for (var i = 0; i < triangles.Length; i += 3) {
        var triIndices  = new int[3]{triangles[i], triangles[i+1], triangles[i+2]};
        var triVertices = new Vector3[3]{vertices[triIndices[0]], vertices[triIndices[1]], vertices[triIndices[2]]};
        var triUVs      = new Vector3[3]{uvs[triIndices[0]], uvs[triIndices[1]], uvs[triIndices[2]]};
        var triNormal   = Vector3.Cross(triVertices[1] - triVertices[0], triVertices[2] - triVertices[0]).normalized;
        
        // Used later to rebuild the faces
        var triFace = new Face();
        triFace.indices = new int[3]{0,0,0};

        for (var j = 0; j < 3; j++) {
          var hash = makeVertexHash(triVertices[j]);
          VertexEntry vEntry;
          if (!vertexMap.TryGetValue(hash, out vEntry)) {
            vEntry = new VertexEntry{vertex = triVertices[j], uv = triUVs[j]};
            vertexMap.Add(hash, vEntry);
          }
          vEntry.normals.Add(triNormal);
          vEntry.faces.Add(new FaceEntry(){face = triFace, index=j});
        }
      }
    }

    var finalVertices = new List<Vector3>();
    var finalNormals  = new List<Vector3>();
    var finalUVs = new List<Vector3>();

    foreach (var vertexMap in vertexMaps) {
      foreach (var vertexEntry in vertexMap.Values) {
        var vertex = vertexEntry.vertex;

        // Clean up all vertices outside of the min/max
        if ((vertex.x < minXZ.x || vertex.x > maxXZ.x) || (vertex.z < minXZ.y || vertex.z > maxXZ.y)) {
          vertexEntry.isRemoved = true;
          foreach (var faceEntry in vertexEntry.faces) { faceEntry.face.isRemoved = true; }
          continue;
        }
    
        var uv = vertexEntry.uv;

        // Put each normal into its own group
        var groupedNormals = new List<NormalGroup>();
        for (int i = 0; i < vertexEntry.normals.Count; i++) {
          var grp = new NormalGroup();
          grp.normalIndices.Add(i);
          grp.avgNormal = vertexEntry.normals[i];
          groupedNormals.Add(grp);
        }

        // Consolidate all the grouped normals based on their comparative angles
        while (true) {
          // Compare each group's normal with each other to see if they can be combined
          int mergeGroup1 = -1; int mergeGroup2 = -1;
          for (int i = 0; i < groupedNormals.Count; i++) {
            var normal1 = groupedNormals[i].avgNormal;
            float smallestAngle = float.MaxValue;
            mergeGroup2 = -1;

            for (int j = 0; j < groupedNormals.Count; j++) {
              if (i == j) { continue; }
              var normal2 = groupedNormals[j].avgNormal;
              var angle = Vector3.Angle(normal1, normal2);
              if (angle <= smoothingAngle && angle < smallestAngle) {
                smallestAngle = angle;
                mergeGroup2 = j;
              }
            }
            if (mergeGroup2 != -1) {
              mergeGroup1 = i;
              break;
            }
          }

          if (mergeGroup1 != -1) {
            // Join the two groups together and continue
            var g1 = groupedNormals[mergeGroup1];
            var g2 = groupedNormals[mergeGroup2];
            groupedNormals.RemoveAt(mergeGroup2);
            for (int i = 0; i < g2.normalIndices.Count; i++) {
              g1.normalIndices.Add(g2.normalIndices[i]);
            }

            // Update the average normal based on all the collected indices
            g1.avgNormal.Set(0,0,0);
            foreach (var nIdx in g1.normalIndices) { g1.avgNormal += vertexEntry.normals[nIdx]; }
            g1.avgNormal.Normalize();

          } 
          else {
            break;
          }
        }

        // Go through each of the grouped normals, average each group and insert 
        // them as duplicates of the vertices
        for (int i = 0; i < groupedNormals.Count; i++) {
          var currNormalGrp = groupedNormals[i];
          for (int j = 0; j < currNormalGrp.normalIndices.Count; j++) {

            var currIndex = currNormalGrp.normalIndices[j];
            var faceEntry = vertexEntry.faces[currIndex];
            faceEntry.face.indices[faceEntry.index] = finalVertices.Count;
            finalVertices.Add(vertex);
            finalUVs.Add(uv);
            finalNormals.Add(currNormalGrp.avgNormal);
            vertexEntry.smoothedNormalIndices.Add(finalNormals.Count-1);
          }
        }
      }
    }

    // Final pass is to resmooth any shared normals between mesh subgroups,
    // these have all been stored in each VertexEntry (smoothedNormals)
    var changedNormals = new HashSet<int>();
    foreach (var vertexMap in vertexMaps) {
      foreach (var vertexEntry in vertexMap.Values) {
        var smoothedNormalIndices = vertexEntry.smoothedNormalIndices;
        for (int i = 0; i < smoothedNormalIndices.Count; i++) {
          var nIdx1 = smoothedNormalIndices[i];
          var normal1 = finalNormals[nIdx1];
          for (int j = i+1; j < smoothedNormalIndices.Count; j++) {
            var nIdx2 = smoothedNormalIndices[j];
            var normal2 = finalNormals[nIdx2];
            if (Vector3.Angle(normal1, normal2) <= smoothingAngle) {
              finalNormals[nIdx1] += normal2;
              finalNormals[nIdx2] += normal1;
              changedNormals.Add(nIdx1);
              changedNormals.Add(nIdx2);
            }
          }
        }
      }
    }
    foreach (var nIdx in changedNormals) {
      finalNormals[nIdx].Normalize();
    }

    mesh.Clear();
    mesh.vertices = finalVertices.ToArray();
    mesh.normals  = finalNormals.ToArray();
    mesh.SetUVs(0, finalUVs);
    mesh.subMeshCount = vertexMaps.Length;
    
    for (int i = 0; i < vertexMaps.Length; i++) {
      var vertexMap = vertexMaps[i];
      var subMeshTris = new List<int>();
      foreach (var vertexEntry in vertexMap.Values) {
        if (vertexEntry.isRemoved) { continue; }
        foreach (var faceEntry in vertexEntry.faces) {
          if (faceEntry.face.isRemoved) { continue; }
          subMeshTris.Add(faceEntry.face.indices[0]);
          subMeshTris.Add(faceEntry.face.indices[1]);
          subMeshTris.Add(faceEntry.face.indices[2]);
        }
      }
      mesh.SetTriangles(subMeshTris.ToArray(), i);
    }
  }

  /*
  public static Material[] Submeshify(this Mesh mesh, in List<Material> materials, in Material defaultMat) {
    Debug.Assert(mesh.triangles.Length == materials.Count);

    // Find the first non-null material - this will be our fallback material
    Material nonNullMat = null;
    for (int i = 0; i < materials.Count; i++) {
      if (materials[i] != null) {
        nonNullMat = materials[i];
        break;
      }
    }

    // Build a dictionary of all the unique materials with indices
    var matDict = new Dictionary<Material, List<int>>();
    if (nonNullMat == null) {
      matDict[defaultMat] = new List<int>();
    }
    else {
      for (int i = 0; i < materials.Count; i++) {
        var currMat = materials[i] == null ? defaultMat : materials[i];
        if (!matDict.ContainsKey(currMat)) {
          matDict[currMat] = new List<int>();
        }
      }
    }

    var multiMatDict = new Dictionary<HashSet<Material>, Material>(HashSet<Material>.CreateSetComparer());
    var matSet = new HashSet<Material>();

    var triangles = mesh.triangles;
    for (int i = 0; i < triangles.Length; i += 3) {
      var t0 = triangles[i]; var t1 = triangles[i+1]; var t2 = triangles[i+2];
      var m0 = materials[i]; var m1 = materials[i+1]; var m2 = materials[i+2];

      m0 = m0 == null ? defaultMat : m0;
      m1 = m1 == null ? defaultMat : m1;
      m2 = m2 == null ? defaultMat : m2;

      // Do the materials all match?
      if (m0 == m1 && m0 == m2) {
        var submeshList = matDict[m0];
        submeshList.Add(t0); submeshList.Add(t1); submeshList.Add(t2);
      }
      else {
        // Materials don't match, adjust the UVs for material blending
        matSet.Clear();
        matSet.Add(m0); matSet.Add(m1); matSet.Add(m2);
        Material multiMat = null;
        if (!multiMatDict.TryGetValue(matSet, out multiMat)) {
          multiMat = Resources.Load<Material>("Materials/TriplanarMultiBlendMat");

          m0.GetTexture("")

          //multiMat.SetTexture("GroundTex1", )
          //multiMat.SetTexture("GroundNormalTex1", )
          //multiMat.SetTexture("WallTex1", )
          //multiMat.SetTexture("WallNormalTex1", )

          //multiMat.SetTexture("GroundTex2", )
          //multiMat.SetTexture("GroundNormalTex2", )
          //multiMat.SetTexture("WallTex2", )
          //multiMat.SetTexture("WallNormalTex2", )

          //multiMat.SetTexture("GroundTex3", )
          //multiMat.SetTexture("GroundNormalTex3", )
          //multiMat.SetTexture("WallTex3", )
          //multiMat.SetTexture("WallNormalTex3", )

          multiMatDict[matSet] = multiMat;
        }
        else {
          // Match up the different materials with the portions of the multi-material
          // TODO
        }

      }
    }
  }
  */
  
  public static (int[][], Material[]) Submeshify(in List<int> triangles, in List<Material> materials, in Material defaultMat) {
    Debug.Assert(triangles.Count == materials.Count);

    // Find the first non-null material - this will be our fallback material
    Material nonNullMat = null;
    for (int i = 0; i < materials.Count; i++) {
      if (materials[i] != null) {
        nonNullMat = materials[i];
        break;
      }
    }

    // Build a dictionary of all the unique materials with indices
    var matDict = new Dictionary<Material, List<int>>();
    if (nonNullMat == null) {
      matDict[defaultMat] = new List<int>();
      nonNullMat = defaultMat;
    }
    else {
      for (int i = 0; i < materials.Count; i++) {
        var currMat = materials[i] == null ? defaultMat : materials[i];
        if (!matDict.ContainsKey(currMat)) {
          matDict[currMat] = new List<int>();
        }
      }
    }

    for (int i = 0; i < triangles.Count; i += 3) {
      var t0 = triangles[i]; var t1 = triangles[i+1]; var t2 = triangles[i+2];
      var m0 = materials[i]; var m1 = materials[i+1]; var m2 = materials[i+2];


      m0 = m0 == null ? nonNullMat : m0;
      m1 = m1 == null ? nonNullMat : m1;
      m2 = m2 == null ? nonNullMat : m2;

      // Do the materials all match?
      if (m0 == m1 && m0 == m2) {
        var submeshList = matDict[m0];
        submeshList.Add(t0); submeshList.Add(t1); submeshList.Add(t2);
      }
      else {
        // ... If not then we need blend multiple triangles together
        if (m0 == m1) {
          var m0m1SubmeshList = matDict[m0];
          m0m1SubmeshList.Add(t0); m0m1SubmeshList.Add(t1); m0m1SubmeshList.Add(t2); 
          //var m2SubmeshList = matDict[m2];
          //m2SubmeshList.Add(t0); m2SubmeshList.Add(t1); m2SubmeshList.Add(t2);
        }
        else if (m0 == m2) {
          var m0m2SubmeshList = matDict[m0];
          m0m2SubmeshList.Add(t0); m0m2SubmeshList.Add(t1); m0m2SubmeshList.Add(t2);
          //var m1SubmeshList = matDict[m1];
          //m1SubmeshList.Add(t0); m1SubmeshList.Add(t1); m1SubmeshList.Add(t2);
        }
        else if (m1 == m2) {
          var m1m2SubmeshList = matDict[m1];
          m1m2SubmeshList.Add(t0); m1m2SubmeshList.Add(t1); m1m2SubmeshList.Add(t2);
          //var m0SubmeshList = matDict[m0];
          //m0SubmeshList.Add(t0); m0SubmeshList.Add(t1); m0SubmeshList.Add(t2);
        }
        else {
          // All materials are different
          var m0SubmeshList = matDict[m0];
          m0SubmeshList.Add(t0); m0SubmeshList.Add(t1); m0SubmeshList.Add(t2);
          //var m1SubmeshList = matDict[m1];
          //m1SubmeshList.Add(t0); m1SubmeshList.Add(t1); m1SubmeshList.Add(t2);
          //var m2SubmeshList = matDict[m2];
          //m2SubmeshList.Add(t0); m2SubmeshList.Add(t1); m2SubmeshList.Add(t2);
        }
      }

    }

    // Convert everything into arrays
    var submeshTris = new int[matDict.Count][];
    var submeshMats = new Material[matDict.Count];
    int count = 0;
    foreach (var matEntry in matDict) {
      submeshMats[count] = matEntry.Key;
      submeshTris[count] = matEntry.Value.ToArray();
      count++;
    }

    return (submeshTris, submeshMats);
  }


  public static float SignedVolumeOfTriangle(in Vector3 p1, in Vector3 p2, in Vector3 p3) {
    return Vector3.Dot(p1, Vector3.Cross(p2,p3)) / 6.0f;
  }

  public static float CalculateVolume(this Mesh mesh) {
    float volume = 0.0f;
    var vertices = mesh.vertices;
    var triangles = mesh.triangles;
    for (int i = 0; i < mesh.triangles.Length; i += 3) {
      volume += MeshHelper.SignedVolumeOfTriangle(
        vertices[triangles[i + 0]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]
      );
    }
    return Mathf.Abs(volume);
  }

}
