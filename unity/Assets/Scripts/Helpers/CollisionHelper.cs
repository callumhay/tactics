
using UnityEngine;

public static class CollisionHelper {

  public static bool IsPointInside(this Collider collider, in Vector3 point, float epsilon=1e-6f) {
    return (collider.ClosestPoint(point) - point).sqrMagnitude < (epsilon * epsilon);
  }
}