using System.Collections;
using System.Collections.Generic;
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
      gameObj.name = Bedrock.gameObjectName;
      var renderer = gameObj.GetComponent<Renderer>();
      renderer.sharedMaterial = Resources.Load<Material>("Materials/BedrockMat");
    }
    regenerateMesh();
  }

  public void regenerateMesh() {
    var unitsPerNode = terrain.unitsPerNode();
    var halfUnitsPerNode = 0.5f * unitsPerNode;
    var scale = new Vector3(terrain.xSize*TerrainColumn.size + halfUnitsPerNode, height, terrain.zSize*TerrainColumn.size + halfUnitsPerNode);
    gameObj.transform.localScale = scale;
    gameObj.transform.position = 0.5f * (new Vector3(terrain.xSize, -(height + unitsPerNode + 1e-6f), terrain.zSize));
    var renderer = gameObj.GetComponent<Renderer>();
  }
}
