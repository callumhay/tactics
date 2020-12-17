using UnityEngine;

public class Bedrock {
  public static readonly string GAME_OBJ_NAME = "Bedrock";
  public static readonly float HEIGHT = 1.0f;

  private TerrainGrid terrain;
  public GameObject gameObj;

  public Bedrock(in TerrainGrid _terrain) {
    terrain = _terrain;

    var existingGameObj = GameObject.Find(GAME_OBJ_NAME);
    if (existingGameObj != null) {
      gameObj = existingGameObj;
    }
    else {
      gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
      gameObj.transform.position = new Vector3(0,0,0);
      gameObj.name = Bedrock.GAME_OBJ_NAME;
      gameObj.layer = LayerMask.NameToLayer(LayerHelper.TERRAIN_LAYER_NAME);
      var renderer = gameObj.GetComponent<Renderer>();
      renderer.sharedMaterial = Resources.Load<Material>("Materials/BedrockMat");
    }
    var collider = gameObj.GetComponent<Collider>();
    collider.material = Resources.Load<PhysicMaterial>("Materials/BedrockPhysMat");
    regenerateMesh();
  }

  public void regenerateMesh() {
    var unitsPerNode = TerrainGrid.unitsPerNode();
    var unitAdjust = unitsPerNode*2f*(1f-MarchingCubes.ISOVAL_CUTOFF);
    var scale = new Vector3(terrain.xSize*TerrainColumn.SIZE + unitAdjust, HEIGHT, terrain.zSize*TerrainColumn.SIZE + unitAdjust);
    gameObj.transform.localScale = scale;
    gameObj.transform.localPosition = 0.5f * (new Vector3(terrain.xSize, -HEIGHT, terrain.zSize));
    var renderer = gameObj.GetComponent<Renderer>();
  }
}
