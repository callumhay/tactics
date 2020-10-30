using System.Collections.Generic;
using System.Linq;
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
    public List<Vector3> normals = new List<Vector3>();
    public List<FaceEntry> faces = new List<FaceEntry>();
    public bool isRemoved = false;
  };

  private class NormalGroup {
    public List<int> normalIndices = new List<int>();
    public Vector3 avgNormal = new Vector3(0,0,0);
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
    var vertexMaps = new Dictionary<string, VertexEntry>[mesh.subMeshCount]; // Mappings for each submesh

    for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
      var vertexMap = new Dictionary<string, VertexEntry>();
      vertexMaps[subMeshIndex] = vertexMap;

      var triangles = mesh.GetTriangles(subMeshIndex);
      for (var i = 0; i < triangles.Length; i += 3) {
        var triIndices  = new int[3]{triangles[i], triangles[i+1], triangles[i+2]};
        var triVertices = new Vector3[3]{vertices[triIndices[0]], vertices[triIndices[1]], vertices[triIndices[2]]};
        var triNormal   = Vector3.Cross(triVertices[1] - triVertices[0], triVertices[2] - triVertices[0]).normalized;
        
        // Used later to rebuild the faces
        var triFace = new Face();
        triFace.indices = new int[3]{0,0,0};

        for (var j = 0; j < 3; j++) {
          var hash = makeVertexHash(triVertices[j]);
          VertexEntry vEntry;
          if (!vertexMap.TryGetValue(hash, out vEntry)) {
            vEntry = new VertexEntry{vertex = triVertices[j]};
            vertexMap.Add(hash, vEntry);
          }
          vEntry.normals.Add(triNormal);
          vEntry.faces.Add(new FaceEntry(){face = triFace, index=j});
        }
      }
    }

    var finalVertices = new List<Vector3>();
    var finalNormals  = new List<Vector3>();

    foreach (var vertexMap in vertexMaps) {
      foreach (var vertexEntry in vertexMap.Values) {
        ref readonly var vertex = ref vertexEntry.vertex;

        // Clean up all vertices outside of the min/max
        if ((vertex.x < minXZ.x || vertex.x > maxXZ.x) || (vertex.z < minXZ.y || vertex.z > maxXZ.y)) {
          vertexEntry.isRemoved = true;
          foreach (var faceEntry in vertexEntry.faces) { faceEntry.face.isRemoved = true; }
          continue;
        }

        // Put each normal into its own  group
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
            var iNorm = groupedNormals[i].avgNormal;
            float smallestAngle = float.MaxValue;
            mergeGroup2 = -1;

            for (int j = 0; j < groupedNormals.Count; j++) {
              if (i == j) { continue; }
              var jNorm = groupedNormals[j].avgNormal;
              var angle = Mathf.Rad2Deg*iNorm.angleTo(jNorm);
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
            g1.avgNormal += g2.avgNormal;
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
            finalNormals.Add(currNormalGrp.avgNormal);
          }
        }
      }
    }

    mesh.Clear();
    mesh.vertices = finalVertices.ToArray();
    mesh.normals  = finalNormals.ToArray();
    for (int i = 0; i < vertexMaps.GetLength(0); i++) {
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

}
