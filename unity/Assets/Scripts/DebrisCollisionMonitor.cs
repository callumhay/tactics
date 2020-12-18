﻿using UnityEngine;

/// <summary>
/// Used to monitor debris movement and collisions in the game and eschew events
/// in special circumstances.
/// </summary>
public class DebrisCollisionMonitor : MonoBehaviour {
  public static readonly float ySqrDistToFalloff = 25.0f; // Squared distance below the y-axis before the mesh has "fallen off" the map
  public static readonly float minVelForSleep    = 1e-4f; // Minimum velocity on each axis of a rigidbody, below this assumes it's no longer active
  public static readonly float minAngVelForSleep = 1e-3f; // Min angular vel on each axis (see above)
  public static readonly float moveEventUpdateTime = 1f/30f;

  public GameEvent onSleepEvent;    // Called when debris slows/stops and comes to rest within the terrain
  public GameEvent onFellOffEvent;  // Called when the debris falls off and goes below the terrain
  public GameEvent onMoveEvent;     // Called as the debris falls/moves
  
  public float sleepTime = 2.0f;    // Time in seconds where 'IsSleeping' is true until the debris is considered fully asleep
  private float sleepTimeCount = 0;

  private float moveTimeCount = 0;

  private bool sleepEventFired = false;
  private bool fellOffEventFired = false;

  private void Start() {
    onSleepEvent   = Resources.Load<GameEvent>(GameEvent.DEBRIS_SLEEP_EVENT);
    onFellOffEvent = Resources.Load<GameEvent>(GameEvent.DEBRIS_FELL_OFF_EVENT);
    onMoveEvent    = Resources.Load<GameEvent>(GameEvent.DEBRIS_MOVE_UPDATE_EVENT);
  }

  private void FixedUpdate() {
    var rigidbody = GetComponent<Rigidbody>();
    var mesh = GetComponent<MeshFilter>().mesh;

    var maxExtentSqrMag = Vector3.SqrMagnitude(mesh.bounds.extents);
    var yPos = transform.position.y;
    //Debug.Log("Y-Position: " + yPos);
    if (!fellOffEventFired && yPos < -1 && (yPos*yPos) > (ySqrDistToFalloff + maxExtentSqrMag)) {
      sleepTimeCount = 0;
      // The debris has fallen off the map
      onFellOffEvent?.FireEvent(gameObject);
      fellOffEventFired = true;
    }
    else if (!sleepEventFired &&
      Mathf.Abs(rigidbody.velocity.x) < minVelForSleep &&  
      Mathf.Abs(rigidbody.velocity.y) < minVelForSleep &&
      Mathf.Abs(rigidbody.velocity.z) < minVelForSleep &&
      Mathf.Abs(rigidbody.angularVelocity.x) < minAngVelForSleep &&
      Mathf.Abs(rigidbody.angularVelocity.y) < minAngVelForSleep &&
      Mathf.Abs(rigidbody.angularVelocity.z) < minAngVelForSleep) {

      if (sleepTimeCount >= sleepTime) {
        // The debris is at rest
        onSleepEvent?.FireEvent(gameObject);
        sleepEventFired = true;
      }
      sleepTimeCount += Time.fixedDeltaTime;
    }
    else {
      sleepTimeCount = 0;
     
      // Debris is moving, fire update events -
      // Since this is an expensive operation (and accuracy is not super important) we don't do it on every update
      moveTimeCount += Time.fixedDeltaTime;
      if (moveTimeCount >= moveEventUpdateTime) {
        onMoveEvent?.FireEvent(gameObject);
        moveTimeCount -= moveEventUpdateTime;
      }
      
    }
  }

}
