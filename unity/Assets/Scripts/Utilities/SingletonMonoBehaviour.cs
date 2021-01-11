using UnityEngine;
 
/// <summary>
/// Inherit from this base class to create a singleton for MonoBehaviours.
/// e.g. public class MyClassName : Singleton<MyClassName> {}
/// </summary>
public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour {
  // Check to see if we're about to be destroyed.
  private static bool _shuttingDown = false;
  private static object _lock = new object();
  private static T _instance;

  /// <summary>
  /// Access singleton instance through this property.
  /// </summary>
  public static T Instance {
    get {
      if (_shuttingDown) {
        Debug.LogWarning("[Singleton] Instance '" + typeof(T) + "' already destroyed. Returning null.");
        return null;
      }

      lock (_lock) {
        if (!_instance) {
          // Search for existing instance.
          _instance = FindObjectOfType<T>();

          // Create new instance if one doesn't already exist
          if (!_instance) {
            // Create a new GameObject for the Singleton
            var singletonObject = new GameObject();
            _instance = singletonObject.AddComponent<T>();
            singletonObject.name = typeof(T).ToString() + " (Singleton)";

            // Make the instance persistent
            DontDestroyOnLoad(singletonObject);
          }
        }

        return _instance;
      }
    }
  }

  private void OnApplicationQuit() {
    _shuttingDown = true;
  }

  private void OnDestroy() {
    _shuttingDown = true;
  }
}