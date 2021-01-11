using System;
using UnityEngine;

[Serializable]
public class Vector3Reference : Reference<Vector3, Vector3Variable> {
  public Vector3Reference(Vector3 val) : base(val) {
  }
  public Vector3Reference() {
  }
}

[CreateAssetMenu(menuName = "Variables/Vector3")]
public class Vector3Variable : Variable<Vector3> {
}