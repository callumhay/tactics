using System.Linq;
using UnityEngine;


public static class TextureHelper {

  public static Texture3D BuildBlackTexture3D(int resSize, TextureFormat format) {
    var blackTex3d = new Texture3D(resSize,resSize,resSize,format,false);
    var blackPixels = Enumerable.Repeat<Color>(Color.black, resSize*resSize*resSize).ToArray<Color>();
    blackTex3d.SetPixels(blackPixels);
    return blackTex3d;
  }

  public static RenderTexture Init3DRenderTexture(int resSize, RenderTextureFormat format) {
    var result = new RenderTexture(resSize, resSize, 0, format);
    result.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
    result.volumeDepth = resSize;
    result.enableRandomWrite = true;
    result.Create();
    ClearRT(result, new Color(0,0,0,0));
    return result;
  }

  private static void ClearRT(RenderTexture rt, Color c) {
    var tempRT = RenderTexture.active;
    RenderTexture.active = rt;
    GL.Clear(true, true, c);
    RenderTexture.active = tempRT;
  }

  public static Texture2D buildJitterTexture2D(int size, bool useDither) {
    var tex = new Texture2D(size, size, TextureFormat.R8, false);
    var numTexels = size*size;
    byte[] texData = new byte[numTexels];
    Random.InitState(Mathf.CeilToInt(Time.time));
    if (useDither) {
      float[,] ditherPattern = new float[4,4]{
        { 0.0f, 0.5f, 0.125f, 0.625f },
        { 0.75f, 0.22f, 0.875f, 0.375f },
        { 0.1875f, 0.6875f, 0.0625f, 0.5625f },
        { 0.9375f, 0.4375f, 0.8125f, 0.3125f }
      };
      for (int i = 0; i < size; i++) {
        for (int j = 0; j < size; j++) {
          float dither = ditherPattern[i%4,j%4] * Random.Range(0.5f,1.0f);
          texData[i*size + j] = (byte)(dither*256);
        }
      }
    }
    else {
      for (int i = 0; i < numTexels; i++) {
        texData[i] = (byte)(((float)Random.Range(0,int.MaxValue)/(float)int.MaxValue)*256);
      }
    }
    tex.LoadRawTextureData(texData);
    tex.Apply();
    return tex;
  }
}
