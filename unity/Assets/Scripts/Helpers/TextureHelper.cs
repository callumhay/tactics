
using UnityEngine;


public static class TextureHelper {

  public static Texture2D buildJitterTexture2D(int size) {
    var tex = new Texture2D(size, size, TextureFormat.R8, false);
    var numTexels = size*size;
    byte[] texData = new byte[numTexels];
    Random.InitState(Mathf.CeilToInt(Time.time));
    for (int i = 0; i < numTexels; i++) {
      texData[i] = (byte)(Random.Range(0f,1f)*256);
    }
    tex.LoadRawTextureData(texData);
    tex.Apply();
    return tex;
  }
}
