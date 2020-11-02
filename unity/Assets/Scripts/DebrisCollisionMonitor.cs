using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used to monitor debris movement and collisions in the game and eschew events
/// in special circumstances.
/// </summary>
public class DebrisCollisionMonitor : MonoBehaviour {
  public static float minVelForSleep = 1e-4f;

  public GameEvent onSleepEvent;    // Called when debris slows/stops and comes to rest within the terrain
  public GameEvent onFellOffEvent;  // Called when the debris falls off and goes below the terrain
  public float sleepTime = 2.0f;    // Time in seconds where 'IsSleeping' is true until the debris is considered fully asleep
  private float sleepTimeCount = 0;

  private void FixedUpdate() {
    var rigidbody = GetComponent<Rigidbody>();
    var mesh = GetComponent<MeshFilter>().mesh;

    var maxExtentSqrMag = Vector3.SqrMagnitude(mesh.bounds.extents);
    var yPos = rigidbody.position.y;
    //Debug.Log("Y-Position: " + yPos);
    if (yPos < -1 && (yPos*yPos) > (25 + maxExtentSqrMag)) {
      sleepTimeCount = 0;
      // The debris has fallen off the map
      onFellOffEvent?.FireEvent();
    }
    else if (Mathf.Abs(rigidbody.velocity.x) < minVelForSleep &&  
             Mathf.Abs(rigidbody.velocity.y) < minVelForSleep &&
             Mathf.Abs(rigidbody.velocity.z) < minVelForSleep &&
             Mathf.Abs(rigidbody.angularVelocity.x) < minVelForSleep &&
             Mathf.Abs(rigidbody.angularVelocity.y) < minVelForSleep &&
             Mathf.Abs(rigidbody.angularVelocity.z) < minVelForSleep) {

      if (sleepTimeCount >= sleepTime) {
        // The debris is at rest
        onSleepEvent?.FireEvent();
      }
      sleepTimeCount += Time.deltaTime;
    }
    else {
      sleepTimeCount = 0;
    }
  }


}
