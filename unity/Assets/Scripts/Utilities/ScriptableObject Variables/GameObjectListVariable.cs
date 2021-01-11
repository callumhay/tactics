using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameObjectListReference : Reference<List<GameObject>, GameObjectListVariable> {
    public GameObjectListReference(List<GameObject> Value) : base(Value) {
    }
    public GameObjectListReference() {
    }
}

[CreateAssetMenu(menuName = "Variables/GameObject List")]
public class GameObjectListVariable : Variable<List<GameObject>> {
}