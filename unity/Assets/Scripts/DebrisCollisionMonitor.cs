using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used to monitor debris movement and collisions in the game and eschew events
/// in special circumstances.
/// </summary>
public class DebrisCollisionMonitor : MonoBehaviour {

  public GameEvent onSleepEvent;    // Called when debris slows/stops and comes to rest within the terrain
  public GameEvent onFellOffEvent;  // Called when the debris falls off and goes below the terrain

  private void FixedUpdate() {
    var rigidbody = GetComponent<Rigidbody>();
    var mesh = GetComponent<MeshFilter>().mesh;

    var maxExtentSqrMag = Vector3.SqrMagnitude(mesh.bounds.extents);
    var yPos = gameObject.transform.position.y;
    if (yPos < -5 && (yPos*yPos) < maxExtentSqrMag) {
      // The debris has fallen off the map
      onFellOffEvent?.FireEvent();
    }
    else if (rigidbody.IsSleeping()) {
      // The debris is at rest
      onSleepEvent?.FireEvent();
    }
  }


}
