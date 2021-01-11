using UnityEngine;

public class Variable<T> : ScriptableObject {
#if UNITY_EDITOR
    [Multiline] public string DeveloperDescription = "";
#endif
    public T value;

    public void SetValue(T val) {
        this.value = val;
    }

    public void SetValue(Variable<T> val) {
        this.value = val.value;
    }
}