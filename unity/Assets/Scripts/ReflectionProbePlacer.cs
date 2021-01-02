using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
public class ReflectionProbePlacer : MonoBehaviour {
  public static readonly string GAME_OBJ_NAME = "Reflection Probes";

  private static readonly float PROBE_HEIGHT_SPACING = 2*TerrainColumn.SIZE;
  [Range(1,10)] public int placementFrequency = 10; // Approx. number of columns squared per probe

  private List<ReflectionProbe> reflectionProbes = new List<ReflectionProbe>();

  public static (GameObject, ReflectionProbePlacer) buildOrFindReflProbes() {
    var reflProbeGO = GameObject.Find(GAME_OBJ_NAME);
    if (!reflProbeGO) {
      reflProbeGO = new GameObject(GAME_OBJ_NAME);
    }
    var reflProbePlacer = reflProbeGO.GetComponent<ReflectionProbePlacer>();
    if (!reflProbePlacer) {
      reflProbePlacer = reflProbeGO.AddComponent<ReflectionProbePlacer>();
    }
    return (reflProbeGO, reflProbePlacer);
  }

  private void regenerateProbes() {
    clear();

    var terrainGrid = TerrainGrid.FindTerrainGrid();
    float terrainSizeX = terrainGrid.xSize;
    float terrainSizeZ = terrainGrid.zSize;

    // Figure out how many probes to place
    int numProbesX = Mathf.CeilToInt(terrainSizeX / (float)placementFrequency);
    int numProbesZ = Mathf.CeilToInt(terrainSizeZ / (float)placementFrequency);
    //int numProbes = numProbesX*numProbesZ;

    float probeUnitsX = terrainGrid.xUnitSize() / (float)numProbesX;
    float probeUnitsZ = terrainGrid.zUnitSize() / (float)numProbesZ;

    float halfProbeUnitsX = probeUnitsX / 2f;
    float halfProbeUnitsZ = probeUnitsZ / 2f;

    int colIdxPerProbeX = Mathf.CeilToInt(terrainSizeX / (float)numProbesX)-1;
    int colIdxPerProbeZ = Mathf.CeilToInt(terrainSizeZ / (float)numProbesZ)-1;

    var tempVec2Int = new Vector2Int();
    //reflectionProbes = new List<ReflectionProbe>();
    for (int x = 0; x < numProbesX; x++) {
      int minXColIdx = x*colIdxPerProbeX;
      int maxXColIdx = minXColIdx + colIdxPerProbeX;

      for (int z = 0; z < numProbesZ; z++) {
        int minZColIdx = z*colIdxPerProbeZ;
        int maxZColIdx = minZColIdx + colIdxPerProbeZ;

        // Find the set of terrain columns applicable to each probe
        var terrainCols = new List<TerrainColumn>();
        for (int tcX = minXColIdx; tcX <= maxXColIdx; tcX++) {
          for (int tcZ = minZColIdx; tcZ <= maxZColIdx; tcZ++) {
            tempVec2Int.Set(tcX,tcZ);
            terrainCols.Add(terrainGrid.terrainColumn(tempVec2Int));
          }
        }

        // Analyze the columns to find out where to place the probe
        //float minColY = terrainGrid.yUnitSize() + PROBE_HEIGHT_SPACING;
        float maxColY = 0;
        foreach (var col in terrainCols) {
          var bounds = col.bounds();
          //minColY = Mathf.Min(minColY, bounds.max.y);
          maxColY = Mathf.Max(maxColY, bounds.max.y);
        }
        
        // Build the GameObject and ReflectionProbe component and add them to this
        float probeXPos = x*probeUnitsX + halfProbeUnitsX;
        float probeZPos = z*probeUnitsZ + halfProbeUnitsZ;
        
        var probeGOName = "Reflection Probe (" + x + "," + z + ")";
        var probeGO = transform.Find(probeGOName)?.gameObject;
        if (!probeGO) { probeGO = new GameObject(probeGOName); }
        probeGO.transform.SetParent(transform);
        probeGO.transform.position = new Vector3(probeXPos, maxColY + PROBE_HEIGHT_SPACING, probeZPos);

        var probe = probeGO.GetComponent<ReflectionProbe>();
        if (!probe) { probe = probeGO.AddComponent<ReflectionProbe>(); }
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.hdr = true;
        probe.resolution = 128;
        probe.nearClipPlane = 0.3f;
        probe.farClipPlane  = 10000f;
        probe.cullingMask &= ~(1 << LayerMask.NameToLayer(LayerHelper.WATER_LAYER_NAME));

        // Hack to get resolution working in HDRP
        var hdProbe = probeGO.GetComponent<HDProbe>();
        if (hdProbe) {
          hdProbe.resolution = PlanarReflectionAtlasResolution.PlanarReflectionResolution128;
        }
        
        reflectionProbes.Add(probe);
      }

      // Rerender all the probes
      foreach (var probe in reflectionProbes) {
        probe.RenderProbe();
      }
    }
  }

  private void clear() {
    for (int i = 0; i < reflectionProbes.Count; i++) {
      GameObject.DestroyImmediate(reflectionProbes[i].gameObject);
    }
    reflectionProbes.Clear();
  }

  private void Start() {
    regenerateProbes();
  }

  private void OnValidate() {
    regenerateProbes();
  }


}
