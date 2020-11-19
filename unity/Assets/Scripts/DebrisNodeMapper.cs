using UnityEngine;

public class DebrisNodeMapper : MonoBehaviour {

    // Original nodes/corners used to build the debris in local space
    private CubeCorner[,,] _originalCorners;
    public CubeCorner[,,] originalCorners { 
      set { _originalCorners = value; } 
    }

    public CubeCorner mapFromWorldspace(in Vector3 wsPos) {
      Vector3 lsPos = transform.InverseTransformPoint(wsPos); // world space to local space

      // Local space to index space and clamp the index space position into the original grid size for the debris
      int x = Mathf.Clamp(Mathf.RoundToInt(lsPos.x/TerrainGrid.unitsPerNode() + _originalCorners.GetLength(0)/2.0f), 0, _originalCorners.GetLength(0)-1);
      int y = Mathf.Clamp(Mathf.RoundToInt(lsPos.y/TerrainGrid.unitsPerNode() + _originalCorners.GetLength(1)/2.0f), 0, _originalCorners.GetLength(1)-1);
      int z = Mathf.Clamp(Mathf.RoundToInt(lsPos.z/TerrainGrid.unitsPerNode() + _originalCorners.GetLength(2)/2.0f), 0, _originalCorners.GetLength(2)-1);

      return _originalCorners[x,y,z];
    }

}
