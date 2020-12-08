using System.Collections.Generic;

public class TerrainDebrisDiff {
  public HashSet<TerrainGridNode> prevDebrisNodes;
  public HashSet<TerrainGridNode> currDebrisNodes;

  public TerrainDebrisDiff(HashSet<TerrainGridNode> prev, HashSet<TerrainGridNode> curr) {
    prevDebrisNodes = prev;
    currDebrisNodes = curr;
  }

}
