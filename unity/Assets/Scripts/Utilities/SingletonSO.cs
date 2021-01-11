using UnityEngine;
 
/// <summary>
/// Inherit from this base class to create a singleton for ScriptableObjects.
/// e.g. public class MyClassName : Singleton<MyClassName> {}
/// </summary>
public class SingletonSO<T> : ScriptableObject where T : ScriptableObject {
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
            _instance = CreateInstance<T>();
          }
        }

        return _instance;
      }
    }
  }

  private void OnDestroy() {
    _shuttingDown = true;
  }
}