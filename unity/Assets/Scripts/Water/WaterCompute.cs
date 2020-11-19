using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterCompute : MonoBehaviour {
  public static int waterResolution = 128;
  public static int numThreadsPerBlock = 8;
  public static int numThreadGroups = waterResolution / numThreadsPerBlock;

  public ComputeShader waterCS;
  private int waterCSKernel;

  public ComputeShader marchingCubesCS;
  private int mcCSKernel;
  
  public ComputeShader smoothNormalsCS;
  private int smoothNormalsCSKernel;
  //private ComputeBuffer verticesBuffer;
  //private ComputeBuffer normalsBuffer;
  //private ComputeBuffer smoothedNormalsBuffer;

  private ComputeBuffer isoValuesBuffer;
  private ComputeBuffer meshBuffer;
  private ComputeBuffer countBuffer;

  // CPU Mesh Rendering
  private MeshFilter meshFilter;
  private MeshRenderer meshRenderer;

  private int maxTris;
  private int maxVerts;

  private static float unitsPerNode = 0.25f;

  #pragma warning disable 649 // disable unassigned variable warning
  struct Vert {
    public Vector3 position;
    public Vector3 normal;
  }
  struct Triangle { 
    public Vector3 a; 
    public Vector3 b; 
    public Vector3 c;
  }

  private void createBuffers() {
    int pointsCount = waterResolution*waterResolution*waterResolution;
    int voxelCount = waterResolution-1;
    voxelCount = voxelCount*voxelCount*voxelCount;
    maxTris = voxelCount*5;
    maxVerts = maxTris*3;

    isoValuesBuffer = new ComputeBuffer(pointsCount, sizeof(float));
    countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
    meshBuffer = new ComputeBuffer(maxTris, sizeof(float)*9, ComputeBufferType.Append);
    meshBuffer.SetCounterValue(0);

    //verticesBuffer = new ComputeBuffer(maxVerts, sizeof(float)*3);
    //normalsBuffer  = new ComputeBuffer(maxVerts, sizeof(float)*3);
    //smoothedNormalsBuffer = new ComputeBuffer(maxVerts, sizeof(float)*3);
  }

  private void removeBuffers() {
    isoValuesBuffer?.Dispose();
    meshBuffer?.Dispose();
    countBuffer?.Dispose();
    //verticesBuffer?.Dispose();
    //normalsBuffer?.Dispose();
    //smoothedNormalsBuffer?.Dispose();
  }

  private void simulateLiquid() {
    waterCS.SetBuffer(waterCSKernel, "isoValues", isoValuesBuffer);
    waterCS.SetInt("resolutionSize", waterResolution);
    waterCS.SetFloat("time", Time.time);
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

    marchingCubesCS.SetBuffer(mcCSKernel, "meshData", meshBuffer);
    marchingCubesCS.SetBuffer(mcCSKernel, "isoValues", isoValuesBuffer);
    marchingCubesCS.SetInt("numNodesPerSide", waterResolution);
    marchingCubesCS.SetFloat("isoLevel", MarchingCubes.isoValCutoff);
    marchingCubesCS.SetFloat("unitsPerNode", unitsPerNode);
    marchingCubesCS.Dispatch(mcCSKernel, numThreadGroups, numThreadGroups, numThreadGroups);

    ComputeBuffer.CopyCount(meshBuffer, countBuffer, 0);
    int[] countArr = new int[1]{0};
    countBuffer.GetData(countArr);
    int triCount = countArr[0];

    // Turn the triangles into a mesh!
    var mesh = readMeshGPUToCPUTris(meshBuffer, triCount);
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
    generateMesh();
  }

  void Update() {
    //simulateLiquid();
    //generateMesh();
  }

  void OnDestroy() {
    removeBuffers();
  }

  private static Mesh readMeshGPUToCPUVerts(in ComputeBuffer vertsBuf, int vertCount) {
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
  private static Mesh readMeshGPUToCPUTris(in ComputeBuffer trisBuf, int triCount) {
    Triangle[] tris = new Triangle[triCount];
    trisBuf.GetData(tris, 0, 0, triCount);

    var mesh = new Mesh();
    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

    var vertCount = triCount*3;
    Vector3[] vertices = new Vector3[vertCount];
    int[] triangles = new int[vertCount];

    for (int i = 0; i < triCount; i++) {
      var vertIdx = i*3;
      vertices[vertIdx] = tris[i].a; vertices[vertIdx+1] = tris[i].b; vertices[vertIdx+2] = tris[i].c;
      triangles[vertIdx] = vertIdx; triangles[vertIdx+1] = vertIdx+1; triangles[vertIdx+2] = vertIdx+2;
    }

    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();

    return mesh;
  }

}
