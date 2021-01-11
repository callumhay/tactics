using UnityEngine;
#pragma warning disable 649

/// <summary>
/// Used to monitor debris movement and collisions in the game and eschew events
/// in special circumstances.
/// </summary>
public class DebrisCollisionMonitor : MonoBehaviour {

  public GameObjectEvent onSleepEvent;    // Called when debris slows/stops and comes to rest within the terrain
  public GameObjectEvent onFellOffEvent;  // Called when the debris falls off and goes below the terrain
  public GameObjectEvent onMoveUpdateEvent;     // Called as the debris falls/moves
  
  [SerializeField] private float ySqrDistToFalloff = 25.0f; // Squared distance below the y-axis before the mesh has "fallen off" the map
  [SerializeField] private float minVelForSleep    = 1e-4f; // Minimum velocity on each axis of a rigidbody, below this assumes it's no longer active
  [SerializeField] private float minAngVelForSleep = 1e-3f; // Min angular vel on each axis (see above)

  [Tooltip("Frequency for firing move update events.")]
  [SerializeField] private FloatReference moveUpdateFrequency;

  [Tooltip("Time in seconds until the debris is considered fully asleep once it satisfies min velocities for sleeping.")]
  [SerializeField] private float sleepTime = 2.0f;    // Time in seconds where 'IsSleeping' is true until the debris is considered fully asleep
  
  // Non-serialized fields - used to track state
  private float sleepTimeCount = 0;
  private float moveTimeCount = 0;
  private bool sleepEventFired = false;
  private bool fellOffEventFired = false;
  private Rigidbody rigidBody;
  private MeshFilter meshFilter;

  private void Start() {
    rigidBody  = GetComponent<Rigidbody>();
    meshFilter = GetComponent<MeshFilter>();
  }

  private void FixedUpdate() {
    var mesh = meshFilter.mesh;

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
      Mathf.Abs(rigidBody.velocity.x) < minVelForSleep &&  
      Mathf.Abs(rigidBody.velocity.y) < minVelForSleep &&
      Mathf.Abs(rigidBody.velocity.z) < minVelForSleep &&
      Mathf.Abs(rigidBody.angularVelocity.x) < minAngVelForSleep &&
      Mathf.Abs(rigidBody.angularVelocity.y) < minAngVelForSleep &&
      Mathf.Abs(rigidBody.angularVelocity.z) < minAngVelForSleep) {

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
      float moveEventUpdateTime = 1f / moveUpdateFrequency.value;
      if (moveTimeCount >= moveEventUpdateTime) {
        onMoveUpdateEvent?.FireEvent(gameObject);
        moveTimeCount -= moveEventUpdateTime;
      }
      
    }
  }

}
