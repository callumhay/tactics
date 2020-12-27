using UnityEngine;

public class TerrainLiquid {
  public static readonly string GAME_OBJ_NAME = "Liquid";

  public GameObject gameObj;
  public LiquidVolumeRaymarcher volRaymarcher; // Renders the liquid
  public LiquidCompute liquidCompute;      // Computes the physics of the liquid

  public TerrainLiquid() {
    var existingGameObj = GameObject.Find(GAME_OBJ_NAME);
    if (existingGameObj != null) {
      gameObj = existingGameObj;
      volRaymarcher = gameObj.GetComponent<LiquidVolumeRaymarcher>();
      liquidCompute  = gameObj.GetComponent<LiquidCompute>();
    }
    else {
      gameObj = new GameObject(GAME_OBJ_NAME);
      gameObj.layer = LayerMask.NameToLayer(LayerHelper.WATER_LAYER_NAME);
      volRaymarcher = gameObj.AddComponent<LiquidVolumeRaymarcher>();
      liquidCompute  = gameObj.AddComponent<LiquidCompute>();
    }
  }

  public void initAll(in TerrainGridNode[,,] nodes) {
    //volRaymarcher.initAll();
    liquidCompute.initAll(volRaymarcher);
    liquidCompute.writeUpdateNodesToLiquid(nodes);
  }

}
