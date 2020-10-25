using UnityEngine;

public static class VectorHelper {
  public static float angleTo(this Vector3 v1, in Vector3 v2) {
    var denom = Mathf.Sqrt(v1.sqrMagnitude * v2.sqrMagnitude);
    if (denom == 0) { return Mathf.PI / 2.0f; }
    var theta = Vector3.Dot(v1,v2) / denom;
    return Mathf.Acos(Mathf.Clamp(theta,-1,1));
  }
}
