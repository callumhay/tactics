using System;
using UnityEngine;

[Serializable]
public class RectTransformReference : Reference<RectTransform, RectTransformVariable> {
    public RectTransformReference(RectTransform Value) : base(Value) {
    }
    public RectTransformReference() {
    }
}

[CreateAssetMenu(menuName = "Variables/RectTransform")]
public class RectTransformVariable : Variable<RectTransform> {
}