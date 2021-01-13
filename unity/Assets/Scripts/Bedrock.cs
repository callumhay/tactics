using UnityEngine;

public class Bedrock : MonoBehaviour {
  public static readonly float HEIGHT = 1.0f;

  /// <summary>
  /// Call to ensure that the bedrock is the proper size and positioned appropriately for the given terrain.
  /// </summary>
  public void UpdateMesh(TerrainGrid terrain) {
    var unitsPerNode = TerrainGrid.UnitsPerNode();
    var unitAdjust = unitsPerNode*2f*(1f-MarchingCubes.ISOVAL_CUTOFF);
    var xUnitSize = terrain.XUnitSize();
    var zUnitSize = terrain.ZUnitSize();

    var scale = new Vector3(xUnitSize + unitAdjust, HEIGHT, zUnitSize + unitAdjust);
    transform.localScale = scale;
    transform.localPosition = 0.5f * (new Vector3(xUnitSize, -HEIGHT, zUnitSize));
  }
}
