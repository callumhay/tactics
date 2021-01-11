using System;
using UnityEngine;

[Serializable]
public class GameObjectReference : Reference<GameObject, GameObjectVariable> {
    public GameObjectReference(GameObject Value) : base(Value) {
    }
    public GameObjectReference() {
    }
}

[CreateAssetMenu(menuName = "Variables/GameObject")]
public class GameObjectVariable : Variable<GameObject> {
}