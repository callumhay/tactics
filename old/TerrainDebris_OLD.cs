using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// TODO:
// - Make a prefab for debris, add all the components to it including the events
// - Make this class a Monobehaviour that is on the prefab, remove unnecessary code
// - Instantiate the prefab (using "instantiate") inside the TerrainGrid where this class is created
// - Destroy the GameObject inside the TerrainGrid (it should be created and destroyed in the same class!)
public class TerrainDebris_OLD {
  // TODO: Remove this and use materials to define the density!
  private static float density = 1000.0f; // kg/m^3
  
  public static string gameObjName = "Debris";

  public GameObject gameObj;
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;
  private MeshCollider meshCollider;
  private Rigidbody rigidbody;
  private DebrisCollisionMonitor collisionMonitor;
  private GameEventListener felloffListener;

  public TerrainDebris_OLD(in Vector3 pos) {
    gameObj = new GameObject(gameObjName);
    gameObj.transform.position = pos;
    initComponents();
  }

  private void initComponents() {
    gameObj.SetActive(false);

    meshFilter = gameObj.GetComponent<MeshFilter>(); 
    if (meshFilter == null) { meshFilter = gameObj.AddComponent<MeshFilter>(); }
    meshRenderer = gameObj.GetComponent<MeshRenderer>(); 
    if (meshRenderer == null) { meshRenderer = gameObj.AddComponent<MeshRenderer>(); }

    meshCollider = gameObj.GetComponent<MeshCollider>();
    if (meshCollider == null) { meshCollider = gameObj.AddComponent<MeshCollider>(); }
    meshCollider.convex = true;
    rigidbody = gameObj.GetComponent<Rigidbody>();
    if (rigidbody == null) { rigidbody = gameObj.AddComponent<Rigidbody>(); }

    // Monitor collisions with game events and listeners
    //sleepListener = gameObj.GetComponent<GameEventListener>() ?? gameObj.AddComponent<GameEventListener>();
    //sleepListener.unityEvent = sleepListener.unityEvent ?? new UnityEvent<GameObject>();
    //sleepListener.unityEvent.AddListener(onDebrisSleep);
    //sleepListener.gameEvent = Resources.Load<GameEvent>(GameEvent.DEBRIS_SLEEP_EVENT);

    felloffListener = gameObj.GetComponent<GameEventListener>();
    if (felloffListener == null) { felloffListener =  gameObj.AddComponent<GameEventListener>(); }
    felloffListener.unityEvent = felloffListener.unityEvent ?? new UnityEvent<GameObject>();
    felloffListener.unityEvent.AddListener(onDebrisFellOff);
    felloffListener.gameEvent = Resources.Load<GameEvent>(GameEvent.DEBRIS_FELL_OFF_EVENT);

    collisionMonitor = gameObj.GetComponent<DebrisCollisionMonitor>();
    if (collisionMonitor == null) { collisionMonitor = gameObj.AddComponent<DebrisCollisionMonitor>(); }

    nodeMapper = gameObj.GetComponent<DebrisNodeMapper>();
    if (nodeMapper == null) { nodeMapper = gameObj.AddComponent<DebrisNodeMapper>(); }

    // No fracturing for now... need to implement fracturing using Voronoi splitting, right now it looks a bit strange
    //meshFracturer = gameObj.GetComponent<MeshFracturer>();
    //if (meshFracturer == null) { meshFracturer = gameObj.AddComponent<MeshFracturer>(); }

    gameObj.SetActive(true);
  }

  // Takes a 3D array of localspace nodes and generates the mesh for this debris
  public void build(CubeCorner[,,] lsNodes) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var materials = new List<Tuple<Material[],float[]>>();

    var corners = new CubeCorner[CubeCorner.NUM_CORNERS];
    for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) { corners[i] = new CubeCorner(); }

    for (int x = 0; x < lsNodes.GetLength(0)-1; x++) {
      for (int y = 0; y < lsNodes.GetLength(1)-1; y++) {
        for (int z = 0; z < lsNodes.GetLength(2)-1; z++) {
          ref readonly var node = ref lsNodes[x,y,z];
          for (int i = 0; i < CubeCorner.NUM_CORNERS; i++) {
            ref readonly var cornerInc = ref MarchingCubes.corners[i];
            var cornerNode = lsNodes[x+cornerInc.x, y+cornerInc.y, z+cornerInc.z];
            corners[i].position = cornerNode.position;
            corners[i].isoVal = cornerNode.isoVal;
            corners[i].materials = cornerNode.materials;
          }
          MarchingCubes.polygonize(corners, materials, triangles, vertices, false);
        }
      }
    }

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.vertices = vertices.ToArray();

    // Split the mesh triangles up into their respective material groups (i.e., submeshes)
    MeshHelper.Submeshify(ref mesh, ref meshRenderer, ref materials, triangles, MaterialHelper.defaultMaterial);

    mesh.RecalculateNormals(MeshHelper.defaultSmoothingAngle, MeshHelper.defaultTolerance);
    mesh.RecalculateBounds();
    setMesh(mesh);

    nodeMapper.originalCorners = lsNodes;
  }

  public void setMesh(in Mesh mesh) {
    meshFilter.sharedMesh = mesh;
    meshCollider.sharedMesh = mesh;

    for (int i = 0; i < meshRenderer.materials.Length; i++) {
      meshRenderer.materials[i].SetInt("IsTerrain", 0);
    }

    // TODO: Calculate the mass and drag based on the density of the material and the volume of the mesh
    rigidbody.SetDensity(TerrainDebris.density);
    rigidbody.mass = Mathf.Max(1.0f, TerrainDebris.density * mesh.CalculateVolume());
    rigidbody.drag = getDrag(mesh.bounds);
  }



  public static float getDrag(in Bounds bounds) {
    return 0.01f * Mathf.Max(0.1f, (bounds.size.x * bounds.size.z));
  }

  private void onDebrisFellOff(GameObject eventGO) {
    if (eventGO == gameObj) {
      //Debug.Log("onDebrisFellOff - destroying TerrainDebris GameObject (in TerrainDebris).");
      GameObject.Destroy(gameObj);
    }
  }
}
