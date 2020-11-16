using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterCompute : MonoBehaviour {
  public static int waterResolution = 64;
  public static int numThreadsPerBlock = 8;
  public static int numThreadGroups = waterResolution / numThreadsPerBlock;

  public Material structBufferMat;

  public ComputeShader waterCS;
  private int waterCSKernel;

  public ComputeShader marchingCubesCS;
  private int mcCSKernel;
  //private ComputeBuffer trianglesBuffer;
  //private ComputeBuffer trisCountBuffer;
  
  public ComputeShader smoothNormalsCS;
  private int smoothNormalsCSKernel;
  //private ComputeBuffer verticesBuffer;
  //private ComputeBuffer normalsBuffer;
  //private ComputeBuffer smoothedNormalsBuffer;

  private ComputeBuffer isoValuesBuffer;
  private ComputeBuffer meshBuffer;
  private ComputeBuffer vertCountBuffer;

  // CPU Mesh Rendering
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;


  #pragma warning disable 649 // disable unassigned variable warning
  struct Vert {
    public Vector3 position;
    public Vector3 normal;
  }
  /*struct Triangle { 
    public Vector3 a; 
    public Vector3 b; 
    public Vector3 c;
  }*/


  private void createBuffers() {
    int pointsCount = waterResolution*waterResolution*waterResolution;
    int voxelCount = waterResolution-1;
    voxelCount = voxelCount*voxelCount*voxelCount;
    int maxTris = voxelCount*5;
    int maxVerts = maxTris*3;

    isoValuesBuffer = new ComputeBuffer(pointsCount, sizeof(float));

    meshBuffer = new ComputeBuffer(maxVerts, sizeof(float)*6, ComputeBufferType.Append);
    meshBuffer.SetCounterValue(0);

    vertCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

    //trianglesBuffer = new ComputeBuffer(maxTris, sizeof(float)*9, ComputeBufferType.Append);
    //trisCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
    //trianglesBuffer.SetCounterValue(0);

    //verticesBuffer = new ComputeBuffer(maxVerts, sizeof(float)*3);
    //normalsBuffer  = new ComputeBuffer(maxVerts, sizeof(float)*3);
    //smoothedNormalsBuffer = new ComputeBuffer(maxVerts, sizeof(float)*3);
  }

  private void removeBuffers() {
    isoValuesBuffer?.Dispose();
    meshBuffer?.Dispose();
    vertCountBuffer?.Dispose();
    //trianglesBuffer?.Dispose();
    //trisCountBuffer?.Dispose();
    //verticesBuffer?.Dispose();
    //normalsBuffer?.Dispose();
    //smoothedNormalsBuffer?.Dispose();
  }

  private void simulateLiquid() {
    waterCS.SetBuffer(waterCSKernel, "isoValues", isoValuesBuffer);
    waterCS.SetInt("resolutionSize", waterResolution);
    waterCS.SetFloat("time", Time.timeSinceLevelLoad);
    //result = new RenderTexture(waterResolution, waterResolution, 0, RenderTextureFormat.ARGBFloat);
    //result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
    //result.volumeDepth = waterResolution;
    //result.enableRandomWrite = true;
    //result.Create();
    //waterCS.SetTexture(waterCSKernel, "Result", result);
    waterCS.Dispatch(waterCSKernel, numThreadGroups, numThreadGroups, numThreadGroups);
  }
  private void generateMesh() {
    meshBuffer.SetCounterValue(0);

    marchingCubesCS.SetBuffer(mcCSKernel, "vertices", meshBuffer);
    marchingCubesCS.SetBuffer(mcCSKernel, "isoValues", isoValuesBuffer);
    marchingCubesCS.SetInt("numNodesPerSide", waterResolution);
    marchingCubesCS.SetFloat("isoLevel", MarchingCubes.isoValCutoff);
    marchingCubesCS.SetFloat("unitsPerNode", 0.25f);
    marchingCubesCS.Dispatch(mcCSKernel, numThreadGroups, numThreadGroups, numThreadGroups);

    // Get the number of vertices
    ComputeBuffer.CopyCount(meshBuffer, vertCountBuffer, 0);
    int[] countArr = new int[1]{0};
    vertCountBuffer.GetData(countArr);
    int vertCount = countArr[0];

    // Turn the triangles into a mesh!
    var mesh = readMeshGPUToCPU(meshBuffer, vertCount);
    /*
    // Perform smooth shading on the mesh...
    {
      verticesBuffer.SetData(mesh.vertices);
      normalsBuffer.SetData(mesh.normals);

      smoothNormalsCS.SetInt("numVertices", numVertices);
      smoothNormalsCS.SetFloat("smoothingAngleRads", Mathf.Deg2Rad * MeshHelper.defaultSmoothingAngle);

      int threadGroups = Mathf.CeilToInt(numVertices / 8.0f); 
      smoothNormalsCS.Dispatch(smoothNormalsCSKernel, threadGroups, 1, 1);

      Vector3[] normals = new Vector3[numVertices];
      smoothedNormalsBuffer.GetData(normals);
      mesh.SetNormals(normals);
    }
    */
    meshFilter.sharedMesh = mesh;
    
  }

  void Start() {
    if (waterCS == null || marchingCubesCS == null || smoothNormalsCS == null) { 
      Debug.LogWarning("One or multiple compute shaders are not set.");
    }
   
    meshFilter = GetComponent<MeshFilter>();
    if (!meshFilter) {
      meshFilter = gameObject.AddComponent<MeshFilter>();
    }
    meshRenderer = GetComponent<MeshRenderer>();
    if (!meshRenderer) {
      meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }
    if (meshRenderer.sharedMaterial == null) {
      meshRenderer.sharedMaterial = MaterialHelper.defaultMaterial;
    }
  

    createBuffers();

    // Get ids for all the compute shader kernels
    waterCSKernel = waterCS.FindKernel("CSTest");
    mcCSKernel = marchingCubesCS.FindKernel("CSMarchingCubes");
    smoothNormalsCSKernel = smoothNormalsCS.FindKernel("CSSmoothNormals");

    

    //smoothNormalsCS.SetBuffer(smoothNormalsCSKernel, "vertices", verticesBuffer);
    //smoothNormalsCS.SetBuffer(smoothNormalsCSKernel, "normals", normalsBuffer);
    //smoothNormalsCS.SetBuffer(smoothNormalsCSKernel, "smoothedNormals", smoothedNormalsBuffer);
    
    simulateLiquid();
  }

  void Update() {
    generateMesh();
  }
  void FixedUpdate() {
    simulateLiquid();
  }
  /*
  void OnRenderObject() {
    structBufferMat.SetBuffer("_Buffer", meshBuffer);
    structBufferMat.SetPass(0);

    // Get the number of vertices
    ComputeBuffer.CopyCount(meshBuffer, vertCountBuffer, 0);
    int[] countArr = new int[1]{0};
    vertCountBuffer.GetData(countArr);
    int vertCount = countArr[0];

    Graphics.DrawProceduralNow(MeshTopology.Triangles, vertCount);
  }
  */

  void OnDestroy() {
    removeBuffers();
  }


  private static Mesh readMeshGPUToCPU(in ComputeBuffer vertsBuf, int vertCount) {
    Vert[] verts = new Vert[vertCount];
    vertsBuf.GetData(verts, 0, 0, vertCount);

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    Vector3[] vertices = new Vector3[vertCount];
    int[] triangles = new int[vertCount];

    for (int i = 0; i < vertCount; i++) {
      vertices[i] = verts[i].position;
      triangles[i] = i;
    }

    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();

    return mesh;
  }


}
