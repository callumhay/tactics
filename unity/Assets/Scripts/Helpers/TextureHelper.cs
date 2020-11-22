
using UnityEngine;


public static class TextureHelper {

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
