using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReflectionProbePlacer : MonoBehaviour {
  private static readonly float PROBE_HEIGHT_SPACING = 2*TerrainColumn.SIZE;

  [Range(1,10)] public int placementFrequency = 10; // Approx. number of columns squared per probe
  public int probeResolution = 128;

  private List<ReflectionProbe> reflectionProbes;

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
    reflectionProbes = new List<ReflectionProbe>();
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
        var probeGO = new GameObject("Reflection Probe (" + x + "," + z + ")");
        probeGO.transform.SetParent(transform);
        probeGO.transform.position = new Vector3(probeXPos, maxColY + PROBE_HEIGHT_SPACING, probeZPos);

        var probe = probeGO.AddComponent<ReflectionProbe>();
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.resolution = probeResolution;

        reflectionProbes.Add(probe);
      }

      // Rerender all the probes
      foreach (var probe in reflectionProbes) {
        probe.RenderProbe();
      }
    }
  }

  private void clear() {
    foreach (var probe in reflectionProbes) {
      GameObject.Destroy(probe.gameObject);
    }
    reflectionProbes.Clear();
  }

  private void Start() {
    regenerateProbes();
  }

  private void OnValidate() {
    Invoke("reinit", 0);
  }


}
