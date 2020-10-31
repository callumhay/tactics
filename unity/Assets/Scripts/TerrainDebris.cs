using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainDebris {

  
  
  public TerrainDebris(in Vector3 pos) {

  }

  // Takes a 3D array of localspace nodes and generates the mesh for this debris
  public void regenerateMesh(in CubeCorner[,,] lsNodes) {
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
    
    // TODO
  }


}
