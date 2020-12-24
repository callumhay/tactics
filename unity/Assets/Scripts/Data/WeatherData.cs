using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeatherData : ScriptableObject {

  public virtual void init(WeatherController weatherController) {}
  public virtual void update(WeatherController weatherController) {}
}
