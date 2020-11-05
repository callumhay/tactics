using UnityEngine;

public class Bedrock {
  public static readonly string gameObjectName = "Bedrock";
  public static readonly float height = 1.0f;

  private TerrainGrid terrain;
  public GameObject gameObj;

  public Bedrock(in TerrainGrid _terrain) {
    terrain = _terrain;

    var existingGameObj = GameObject.Find(Bedrock.gameObjectName);
    if (existingGameObj != null) {
      gameObj = existingGameObj;
    }
    else {
      gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
      gameObj.transform.position = new Vector3(0,0,0);
      gameObj.name = Bedrock.gameObjectName;
      var renderer = gameObj.GetComponent<Renderer>();
      renderer.sharedMaterial = Resources.Load<Material>("Materials/BedrockMat");
    }
    var collider = gameObj.GetComponent<Collider>();
    collider.material = Resources.Load<PhysicMaterial>("Materials/BedrockPhysMat");
    regenerateMesh();
  }

  public void regenerateMesh() {
    var unitsPerNode = terrain.unitsPerNode();
    var unitAdjust = unitsPerNode*(1-MarchingCubes.isoValCutoff);
    var scale = new Vector3(terrain.xSize*TerrainColumn.size + unitAdjust, height, terrain.zSize*TerrainColumn.size + unitAdjust);
    gameObj.transform.localScale = scale;
    gameObj.transform.localPosition = 0.5f * (new Vector3(terrain.xSize, -height, terrain.zSize));
    var renderer = gameObj.GetComponent<Renderer>();
  }
}
