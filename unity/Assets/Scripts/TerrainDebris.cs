using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainDebris {
  private GameObject gameObj;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private MeshCollider meshCollider;
  private Rigidbody rigidbody;
  
  public TerrainDebris(in Vector3 pos) {
    gameObj = new GameObject("Debris");
    gameObj.transform.position = pos;
    meshFilter = gameObj.AddComponent<MeshFilter>();
    meshRenderer = gameObj.AddComponent<MeshRenderer>();
    meshCollider = gameObj.AddComponent<MeshCollider>();
    meshCollider.convex = true;
    rigidbody = gameObj.AddComponent<Rigidbody>();
  }

  // Takes a 3D array of localspace nodes and generates the mesh for this debris
  public void build(in CubeCorner[,,] lsNodes) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();

    var corners = new CubeCorner[CubeCorner.numCorners];
    for (int x = 0; x < lsNodes.GetLength(0); x++) {
      for (int y = 0; y < lsNodes.GetLength(1); y++) {
        for (int z = 0; z < lsNodes.GetLength(2); z++) {
          ref readonly var node = ref lsNodes[x,y,z];
          for (int i = 0; i < CubeCorner.numCorners; i++) {
            ref readonly var cornerInc = ref MarchingCubes.corners[i];
            var cornerNode = lsNodes[x+cornerInc.x, y+cornerInc.y, z+cornerInc.z];
            corners[i].position = cornerNode.position;
            corners[i].isoVal = cornerNode.isoVal;
          }
          MarchingCubes.polygonize(corners, ref triangles, ref vertices);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();
    mesh.triangles = triangles.ToArray();
    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    // TODO: Calculate the mass based on the density of the material and the volume of the mesh
    

  }

  public void destroy() {
    GameObject.Destroy(gameObj); // Cleans up the GameObject and all components



  }

}
