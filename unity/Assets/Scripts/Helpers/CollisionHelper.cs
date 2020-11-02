
using UnityEngine;

public static class CollisionHelper {

  public static bool IsPointInside(this Collider collider, in Vector3 point) {
    return (collider.ClosestPoint(point) - point).sqrMagnitude < (Mathf.Epsilon * Mathf.Epsilon);
  }
}