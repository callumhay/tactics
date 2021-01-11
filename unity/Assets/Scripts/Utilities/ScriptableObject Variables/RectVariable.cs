using System;
using UnityEngine;

[Serializable]
public class RectReference : Reference<Rect, RectVariable> {
    public RectReference(Rect Value) : base(Value) {
    }
    public RectReference() {
    }
}

[CreateAssetMenu(menuName = "Variables/Rect")]
public class RectVariable : Variable<Rect> {
}