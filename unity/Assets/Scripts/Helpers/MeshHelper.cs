﻿using System;
using System.Linq;
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

  public static void Submeshify(ref Mesh mesh, ref MeshRenderer meshRenderer, 
    in List<int> triangles, in List<Material> materials, in Material defaultMat
  ) {

    Debug.Assert(triangles.Count == materials.Count);

    // Build a dictionary of all the unique materials with indices
    var matDict = new Dictionary<Material, List<int>>();
    for (int i = 0; i < materials.Count; i++) {
      var currMat = materials[i] == null ? defaultMat : materials[i];
      if (!matDict.ContainsKey(currMat)) {
        matDict[currMat] = new List<int>();
      }
    }

    // For cases where there are multiple materials on a single triangle we
    // need to build materials to support blending them
    var multiMatDict = new Dictionary<HashSet<Material>, Material>(HashSet<Material>.CreateSetComparer());
    var matSet = new HashSet<Material>();

    var perUVAmt = 3.0f;
    var uvs = new Vector3[mesh.vertices.Length];
    for (int i = 0; i < uvs.Length; i++) { uvs[i] = new Vector3(perUVAmt,perUVAmt,perUVAmt); }

    for (int i = 0; i < triangles.Count; i += 3) {
      var t0 = triangles[i]; var t1 = triangles[i+1]; var t2 = triangles[i+2];
      var m0 = materials[i]; var m1 = materials[i+1]; var m2 = materials[i+2];

      m0 = m0 == null ? defaultMat : m0;
      m1 = m1 == null ? defaultMat : m1;
      m2 = m2 == null ? defaultMat : m2;

      // Do the materials all match?
      List<int> submeshList = null;
      if (m0 == m1 && m0 == m2) {
        submeshList = matDict[m0];
      }
      else {
        // Materials don't match, adjust the UVs for material blending and introduce
        // a multi-material for rendering
        matSet.Clear(); matSet.Add(m0); matSet.Add(m1); matSet.Add(m2);
        Material multiMat = null;
        
        if (!multiMatDict.TryGetValue(matSet, out multiMat)) {
          multiMat = new Material(Resources.Load<Material>("Materials/Triplanar3BlendMat"));

          var m0GndTex   = m0.GetTexture("GroundTex");
          var m0GndNTex  = m0.GetTexture("GroundNormalTex");
          var m0WallTex  = m0.GetTexture("WallTex");
          var m0WallNTex = m0.GetTexture("WallNormalTex");

          var m1GndTex   = m1.GetTexture("GroundTex");
          var m1GndNTex  = m1.GetTexture("GroundNormalTex");
          var m1WallTex  = m1.GetTexture("WallTex");
          var m1WallNTex = m1.GetTexture("WallNormalTex");

          var m2GndTex   = m2.GetTexture("GroundTex");
          var m2GndNTex  = m2.GetTexture("GroundNormalTex");
          var m2WallTex  = m2.GetTexture("WallTex");
          var m2WallNTex = m2.GetTexture("WallNormalTex");

          multiMat.SetTexture("GroundTex1", m0GndTex);
          multiMat.SetTexture("GroundNormalTex1", m0GndNTex);
          multiMat.SetTexture("WallTex1", m0WallTex);
          multiMat.SetTexture("WallNormalTex1", m0WallNTex);

          multiMat.SetTexture("GroundTex2", m1GndTex);
          multiMat.SetTexture("GroundNormalTex2", m1GndNTex);
          multiMat.SetTexture("WallTex2", m1WallTex);
          multiMat.SetTexture("WallNormalTex2", m1WallNTex);

          multiMat.SetTexture("GroundTex3", m2GndTex);
          multiMat.SetTexture("GroundNormalTex3", m2GndNTex);
          multiMat.SetTexture("WallTex3", m2WallTex);
          multiMat.SetTexture("WallNormalTex3", m2WallNTex);

          multiMat.SetFloat("TextureScale", (m0.GetFloat("TextureScale") + m1.GetFloat("TextureScale") + m2.GetFloat("TextureScale"))/3.0f);
          multiMat.SetVector("TextureOffset", (m0.GetVector("TextureOffset") + m1.GetVector("TextureOffset") + m2.GetVector("TextureOffset"))/3.0f);

          multiMatDict[matSet] = multiMat;
          submeshList = new List<int>();
          matDict[multiMat] = submeshList;
          uvs[t0].Set(perUVAmt,0,0); uvs[t1].Set(0,perUVAmt,0); uvs[t2].Set(0,0,perUVAmt);
        }
        else {
          submeshList = matDict[multiMat];

          // Match up the different materials with the portions of the multi-material
          var gnd1Tex = multiMat.GetTexture("GroundTex1");
          var gnd2Tex = multiMat.GetTexture("GroundTex2");
          var gnd3Tex = multiMat.GetTexture("GroundTex3");

          var m0GndTex = m0.GetTexture("GroundTex");
          var m1GndTex = m1.GetTexture("GroundTex");
          var m2GndTex = m2.GetTexture("GroundTex");
          
          if (m0GndTex.GetNativeTexturePtr() == gnd1Tex.GetNativeTexturePtr()) {
            uvs[t0].Set(perUVAmt,0,0);
            if (m1GndTex.GetNativeTexturePtr() == gnd2Tex.GetNativeTexturePtr()) { uvs[t1].Set(0,perUVAmt,0); uvs[t2].Set(0,0,perUVAmt); }
            else { uvs[t1].Set(0,0,perUVAmt); uvs[t2].Set(0,perUVAmt,0); }
          }
          else if (m0GndTex.GetNativeTexturePtr() == gnd2Tex.GetNativeTexturePtr()) {
            uvs[t0].Set(0,perUVAmt,0);
            if (m1GndTex.GetNativeTexturePtr() == gnd1Tex.GetNativeTexturePtr()) { uvs[t1].Set(perUVAmt,0,0); uvs[t2].Set(0,0,perUVAmt); }
            else { uvs[t1].Set(0,0,perUVAmt); uvs[t2].Set(perUVAmt,0,0); }
          }
          else {
            // m0GndTex == gnd3Tex
            uvs[t0].Set(0,0,perUVAmt);
            if (m1GndTex.GetNativeTexturePtr() == gnd1Tex.GetNativeTexturePtr()) { uvs[t1].Set(perUVAmt,0,0); uvs[t2].Set(0,perUVAmt,0); }
            else { uvs[t1].Set(0,perUVAmt,0); uvs[t2].Set(perUVAmt,0,0); }
          }
        }
      }
      submeshList.Add(t0); submeshList.Add(t1); submeshList.Add(t2);
    }

    int count = 0;
    var submeshMats = new Material[matDict.Count];
    var submeshTris = new int[matDict.Count][];
    foreach (var matEntry in matDict) {
      submeshMats[count] = matEntry.Key;
      submeshTris[count] = matEntry.Value.ToArray();
      count++;
    }
    mesh.SetUVs(0, uvs);
    meshRenderer.sharedMaterials = submeshMats;
    mesh.subMeshCount = submeshMats.Length;

    for (int i = 0; i < submeshTris.GetLength(0); i++) {
      mesh.SetTriangles(submeshTris[i], i);
    }
  }
  
  public static void Submeshify(
    ref Mesh mesh, ref MeshRenderer meshRenderer, ref List<Tuple<Material[], float[]>> materials,
    in List<int> triangles, in Material defaultMat
  ) {

    Debug.Assert(triangles.Count == materials.Count);

    // Build a dictionary of all the unique materials with indices, also do some data massaging
    // to the materials tuples to ensure we only have valid, existant material contributions.
    var matDict = new Dictionary<Material, List<int>>();
    for (int i = 0; i < materials.Count; i++) {
      var currMatList = materials[i].Item1;
      var currContribList = materials[i].Item2;

      bool allMaterialsEmpty = true;
      for (int j = 0; j < currMatList.Length; j++) {
        if (currMatList[j] != null && currContribList[j] != 0) { allMaterialsEmpty = false; break; }
      }

      if (allMaterialsEmpty) {
        // Special case where the material is null / non-existant
        // In this case we set it to be full contribution of the default material
        var currMat = defaultMat;
        currMatList = new Material[1]; currMatList[0] = defaultMat;
        currContribList = new float[1]; currContribList[0] = 1f;
        if (!matDict.ContainsKey(currMat)) {
          matDict[currMat] = new List<int>();
        }
        materials[i] = new Tuple<Material[], float[]>(currMatList, currContribList);
      }
      else {
        // There's at least one material that's not null in the current list
        var nonNullMatList = new List<Material>();
        var nonNullContribList = new List<float>();
        for (int j = 0; j < currMatList.Length; j++) {
          var currMat = currMatList[j];
          var currContrib = currContribList[j];
          if (currMat == null || currContrib <= 0) { continue; }
          if (currMat == null && currContrib > 0) { currMat = defaultMat; }
          nonNullMatList.Add(currMat);
          nonNullContribList.Add(currContrib);
          if (!matDict.ContainsKey(currMat)) {
            matDict[currMat] = new List<int>();
          }
        }
        // Make sure that materials only contains items that are contributing to the appearance of each vertex
        if (nonNullMatList.Count != currMatList.Length) {
          materials[i] = new Tuple<Material[], float[]>(nonNullMatList.ToArray(), nonNullContribList.ToArray());
        }
      }
    }

    // For cases where there are multiple materials on a single triangle we
    // need to build new materials to support blending them
    var multiMatDict = new Dictionary<HashSet<Material>, Material>(HashSet<Material>.CreateSetComparer());

    var m0Dict = new Dictionary<Material, float>();
    var m1Dict = new Dictionary<Material, float>();
    var m2Dict = new Dictionary<Material, float>();
    var allMatSet = new HashSet<Material>();

    var uv0s = new Vector3[mesh.vertices.Length];
    for (int i = 0; i < uv0s.Length; i++) { uv0s[i] = new Vector3(1,1,1); }

    List<int> threeTexMaterial(ref Vector3[] uvs, int t0, int t1, int t2) {
      Debug.Assert(allMatSet.Count <= 3, "You shouldn't be calling this function with more than 3 materials.");
      Material multiMat = null;
      List<int> submeshList = null;

      if (!multiMatDict.TryGetValue(allMatSet, out multiMat)) {
        multiMat = new Material(Resources.Load<Material>("Materials/Triplanar3BlendMat"));
        
        float avgMetallic = 0;
        float avgSmoothness = 0;
        float avgScale = 0;
        Vector4 avgOffset = new Vector4(0,0,0,0);

        int matNum = 1;
        foreach (var mat in allMatSet) {
          avgMetallic   += mat.GetFloat("Metallic");
          avgSmoothness += mat.GetFloat("Smoothness");
          avgScale += mat.GetFloat("TextureScale");
          avgOffset += mat.GetVector("TextureOffset");

          var gndTex   = mat.GetTexture("GroundTex");
          var gndNTex  = mat.GetTexture("GroundNormalTex");
          var wallTex  = mat.GetTexture("WallTex");
          var wallNTex = mat.GetTexture("WallNormalTex");

          multiMat.SetTexture("GroundTex"+matNum, gndTex);
          multiMat.SetTexture("GroundNormalTex"+matNum, gndNTex);
          multiMat.SetTexture("WallTex"+matNum, wallTex);
          multiMat.SetTexture("WallNormalTex"+matNum, wallNTex);
          matNum++;
        }

        avgMetallic /= allMatSet.Count;
        avgSmoothness /= allMatSet.Count;
        avgScale /= allMatSet.Count;
        avgOffset /= allMatSet.Count;
        
        multiMat.SetFloat("Metallic", avgMetallic);
        multiMat.SetFloat("Smoothness", avgSmoothness);
        multiMat.SetFloat("TextureScale", avgScale);
        multiMat.SetVector("TextureOffset", avgOffset);

        multiMatDict[allMatSet] = multiMat;
        submeshList = new List<int>();
        matDict[multiMat] = submeshList;
      }
      else { submeshList = matDict[multiMat]; }

      // Match up the different materials with the portions of the multi-material
      var gnd1TexPtr = multiMat.GetTexture("GroundTex1").GetNativeTexturePtr();
      var gnd2TexPtr = multiMat.GetTexture("GroundTex2").GetNativeTexturePtr();
      var gnd3TexPtr = multiMat.GetTexture("GroundTex3").GetNativeTexturePtr();

      void setUVs(ref Vector3[] uvArr, in Dictionary<Material, float> matLookup, int tIdx) {
        uvArr[tIdx].Set(0,0,0);
        foreach (var entry in matLookup) {
          var mat = entry.Key;
          var contrib = entry.Value;
          var currGndTexPtr = mat.GetTexture("GroundTex").GetNativeTexturePtr();
          if (currGndTexPtr == gnd1TexPtr) { uvArr[tIdx].x = contrib; }
          else if (currGndTexPtr == gnd2TexPtr) { uvArr[tIdx].y = contrib; }
          else { uvArr[tIdx].z = contrib; }
        }
      }
      setUVs(ref uv0s, m0Dict, t0);
      setUVs(ref uv0s, m1Dict, t1);
      setUVs(ref uv0s, m2Dict, t2);

      return submeshList;
    }

    for (int i = 0; i < triangles.Count; i += 3) {
      var i1 = i+1; var i2 = i+2;
      var t0 = triangles[i]; var t1 = triangles[i1]; var t2 = triangles[i2];
      var m0List = materials[i].Item1; var m1List = materials[i1].Item1; var m2List = materials[i2].Item1;
      var c0List = materials[i].Item2; var c1List = materials[i1].Item2; var c2List = materials[i2].Item2;

      m0Dict.Clear(); for (int j = 0; j < m0List.Length; j++) { m0Dict[m0List[j]] = c0List[j]; }
      m1Dict.Clear(); for (int j = 0; j < m1List.Length; j++) { m1Dict[m1List[j]] = c1List[j]; }
      m2Dict.Clear(); for (int j = 0; j < m2List.Length; j++) { m2Dict[m2List[j]] = c2List[j]; }
      
      List<int> submeshList = null;
      if (m0Dict.Count == 1 && m1Dict.Count == 1 && m2Dict.Count == 1) {
        var m0 = m0Dict.First().Key; var m1 = m1Dict.First().Key; var m2 = m2Dict.First().Key;
        // Easy case: all vertices have a single material and each one is the same
        if (m0 == m1 && m0 == m2) { submeshList = matDict[m0]; }
        else {
          // Materials don't match but we're only dealing with a single material per vertex, 
          // adjust the UVs for material blending and introduce a multi-material for rendering
          allMatSet.Clear();
          allMatSet.Add(m0); allMatSet.Add(m1); allMatSet.Add(m2);
          submeshList = threeTexMaterial(ref uv0s, t0, t1, t2);
        }
      }
      else {
        // Trickier - we have some (or all) vertices that have multiple materials applied to them
        allMatSet.Clear();
        allMatSet.UnionWith(m0Dict.Keys); allMatSet.UnionWith(m1Dict.Keys); allMatSet.UnionWith(m2Dict.Keys);
        switch (allMatSet.Count) {
          case 2: case 3: {
            // We can still just use a 3-texture multi-material for this
            submeshList = threeTexMaterial(ref uv0s, t0, t1, t2);
            break;
          }
          case 4: case 5: case 6: {
            // Need our super shader material for all these combinations
            Debug.Assert(false, "No shader for this many materials - yet.");
            //submeshList = manyTexMaterial(ref uv0s, ref uv1s, t0, t1, t2);
            break;
          }
          default:
            Debug.Assert(false, "Too few or too many materials found on triangle: " + allMatSet.Count);
            break;
        }
      }
      submeshList.Add(t0); submeshList.Add(t1); submeshList.Add(t2);
    }

    // Remove any empty material lists
    var emptyMats = new List<Material>();
    foreach (var matEntry in matDict) {
      if (matEntry.Value.Count == 0) { emptyMats.Add(matEntry.Key); }
    }
    foreach (var emptyMat in emptyMats) {
      matDict.Remove(emptyMat);
    }

    // Build the mesh information and set it on the given mesh
    int count = 0;
    var submeshMats = new Material[matDict.Count];
    var submeshTris = new int[matDict.Count][];
    foreach (var matEntry in matDict) {
      submeshMats[count] = matEntry.Key;
      submeshTris[count] = matEntry.Value.ToArray();
      count++;
    }
    mesh.SetUVs(0, uv0s);
    meshRenderer.sharedMaterials = submeshMats;
    mesh.subMeshCount = submeshMats.Length;

    for (int i = 0; i < submeshTris.GetLength(0); i++) {
      mesh.SetTriangles(submeshTris[i], i);
    }
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
