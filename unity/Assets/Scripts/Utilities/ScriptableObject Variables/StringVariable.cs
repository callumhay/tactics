using System;
using UnityEngine;

[Serializable]
public class StringReference : Reference<string, StringVariable> {
  public StringReference(string val) : base(val) {
  }
  public StringReference() {
  }
}

[CreateAssetMenu(menuName = "Variables/String")]
public class StringVariable : Variable<string> {
}