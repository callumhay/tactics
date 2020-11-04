using UnityEngine;

public static class VectorHelper {

  public static float angleTo(this Vector3 v1, in Vector3 v2) {
    var denom = Mathf.Sqrt(v1.sqrMagnitude * v2.sqrMagnitude);
    if (denom == 0) { return Mathf.PI / 2.0f; }
    var theta = Vector3.Dot(v1,v2) / denom;
    return Mathf.Acos(Mathf.Clamp(theta,-1,1));
  }

  public static Vector3 perpendicularTo(this Vector3 v) {
    Vector3 perpendicularMH = new Vector3(v.x, v.y, v.z);
    int smallestIndex = 0;
    if (v[0] < v[1]) {
      if (v[0] < v[2]) {
        smallestIndex = 0;
        perpendicularMH[1] = -v[2];
        perpendicularMH[2] =  v[1]; 
      }
      else {
        smallestIndex = 2;
        perpendicularMH[0] = -v[1];
        perpendicularMH[1] =  v[0]; 
      }
    }
    else {
      if (v[1] < v[2]) {
        smallestIndex = 1;
        perpendicularMH[0] = -v[2];
        perpendicularMH[2] =  v[0]; 
      }
      else {
        smallestIndex = 2;
        perpendicularMH[0] = -v[1];
        perpendicularMH[1] =  v[0]; 
      }
    }
    // We take the smallest coordinate component and set it to zero
    perpendicularMH[smallestIndex] = 0;
    return perpendicularMH;
  }

}
