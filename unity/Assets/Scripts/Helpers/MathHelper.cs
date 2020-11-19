using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MathHelper {
  public static int nextMultipleOf(int value, int multiple) {
    return ((value + (multiple-1)) / multiple) * multiple;
  }
}
