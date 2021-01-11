using System;
using UnityEngine;

[Serializable]
public class FloatReference : Reference<float, FloatVariable> {
  public FloatReference(float val) : base(val) { }
  public FloatReference() { }
}

[CreateAssetMenu(menuName = "Variables/Float")]
public class FloatVariable : Variable<float> { }