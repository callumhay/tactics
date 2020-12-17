using UnityEngine;

public class TerrainLiquid {
  public static readonly string GAME_OBJ_NAME = "Liquid";

  public GameObject gameObj;
  public VolumeRaymarcher volRaymarcher; // Renders the liquid
  public WaterCompute waterCompute;      // Computes the physics of the liquid

  public TerrainLiquid() {
    var existingGameObj = GameObject.Find(GAME_OBJ_NAME);
    if (existingGameObj != null) {
      gameObj = existingGameObj;
      volRaymarcher = gameObj.GetComponent<VolumeRaymarcher>();
      waterCompute  = gameObj.GetComponent<WaterCompute>();
    }
    else {
      gameObj = new GameObject(GAME_OBJ_NAME);
      gameObj.layer = LayerMask.NameToLayer(LayerHelper.WATER_LAYER_NAME);
      volRaymarcher = gameObj.AddComponent<VolumeRaymarcher>();
      waterCompute  = gameObj.AddComponent<WaterCompute>();
    }
  }

  public void initAll(in TerrainGridNode[,,] nodes) {
    volRaymarcher.initAll();
    waterCompute.initAll(volRaymarcher);
    waterCompute.writeUpdateNodesToLiquid(nodes);
  }

}
