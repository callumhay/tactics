using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TerrainDebris {
  // TODO: Remove this and use materials to define the density!
  private static float density = 1000.0f; // kg/m^3

  public GameObject gameObj;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private MeshCollider meshCollider;
  private Rigidbody rigidbody;
  private DebrisCollisionMonitor collisionMonitor;
  private GameEventListener onSleepEventListener;
  private GameEventListener onFellOffEventListener;

  private TerrainGrid terrain;
  
  public TerrainDebris(in TerrainGrid _terrain, in Vector3 pos) {
    terrain = _terrain;

    gameObj = new GameObject("Debris");
    gameObj.transform.position = pos;
    gameObj.SetActive(false); // Needed in order to properly register events
    
    meshFilter = gameObj.AddComponent<MeshFilter>();
    meshRenderer = gameObj.AddComponent<MeshRenderer>();
    meshRenderer.sharedMaterial = Resources.Load<Material>("Materials/TerrainMat");
    meshCollider = gameObj.AddComponent<MeshCollider>();
    meshCollider.convex = true;
    rigidbody = gameObj.AddComponent<Rigidbody>();

    // Monitor collisions with game events and listeners
    onSleepEventListener = gameObj.AddComponent<GameEventListener>();
    onSleepEventListener.unityEvent = new UnityEvent();
    onSleepEventListener.unityEvent.AddListener(onDebrisSleep);
    onSleepEventListener.gameEvent = Resources.Load<GameEvent>("Events/DebrisSleepEvent");

    onFellOffEventListener = gameObj.AddComponent<GameEventListener>();
    onFellOffEventListener.unityEvent = new UnityEvent();
    onFellOffEventListener.unityEvent.AddListener(onDebrisFellOff);
    onFellOffEventListener.gameEvent = Resources.Load<GameEvent>("Events/DebrisFellOffTerrainEvent");

    collisionMonitor = gameObj.AddComponent<DebrisCollisionMonitor>();
    collisionMonitor.onSleepEvent = onSleepEventListener.gameEvent;
    collisionMonitor.onFellOffEvent = onFellOffEventListener.gameEvent;

    gameObj.SetActive(true);
  }

  // Takes a 3D array of localspace nodes and generates the mesh for this debris
  public void build(in CubeCorner[,,] lsNodes) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();

    var corners = new CubeCorner[CubeCorner.numCorners];
    for (int i = 0; i < CubeCorner.numCorners; i++) { corners[i] = new CubeCorner(); }

    for (int x = 0; x < lsNodes.GetLength(0)-1; x++) {
      for (int y = 0; y < lsNodes.GetLength(1)-1; y++) {
        for (int z = 0; z < lsNodes.GetLength(2)-1; z++) {
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

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();
    mesh.triangles = triangles.ToArray();
    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance);
    mesh.RecalculateBounds();
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    meshRenderer.material.SetInt("IsTerrain", 0);

    // TODO: Calculate the mass and drag based on the density of the material and the volume of the mesh
    rigidbody.mass = Mathf.Max(1.0f, TerrainDebris.density * mesh.CalculateVolume());
    rigidbody.drag = 0.01f * Mathf.Max(0.1f, (mesh.bounds.size.x * mesh.bounds.size.z));
  }

  private void onDebrisSleep() {
    Debug.Log("Sleeping.");
    terrain.mergeDebrisIntoTerrain(this);
    GameObject.Destroy(gameObj);
  }
  private void onDebrisFellOff() {
    Debug.Log("Fell off the terrain.");
    terrain.removeDebris(this);
    GameObject.Destroy(gameObj);
  }
}
