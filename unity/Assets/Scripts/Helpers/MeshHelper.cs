using UnityEngine;

public static class MeshHelper {
  public static void BuildCubeData(Vector3 pos, Vector3 size, out int[] tris, out Vector3[] vertices) {
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
}
