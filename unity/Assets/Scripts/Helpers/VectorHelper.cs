using UnityEngine;

public static class VectorHelper {
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
