using System;

[Serializable]
public abstract class Reference {
}

[Serializable]
public class Reference<T, G> : Reference where G : Variable<T> {

  public bool useConstant = true;
  public T constantValue;
  public G variable;

  public Reference() { }
  public Reference(T value) {
    useConstant = true;
    constantValue = value;
  }

  public T value {
    get { return useConstant ? constantValue : variable.value; }
    set {
      if (useConstant) { constantValue = value; }
      else { variable.value = value; }
    }
  }

  public static implicit operator T(Reference<T, G> reference) {
    return reference.value;
  }

  public static implicit operator Reference<T, G>(T val) {
    return new Reference<T, G>(val);
  }
}