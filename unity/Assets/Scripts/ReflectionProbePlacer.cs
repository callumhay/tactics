using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
#pragma warning disable 649

[ExecuteAlways]
public class ReflectionProbePlacer : MonoBehaviour {
  private static readonly float PROBE_HEIGHT_SPACING = 2*TerrainColumn.SIZE;

  [Range(1,10)] public int placementFrequency = 10; // Approx. number of columns squared per probe
  [SerializeField] private TerrainGrid terrainGrid;

  public void RegenerateProbes() {
    // Track all of the existing child probes so that we know which ones are no longer in
    // use so that we can remove them
    var childrenToRemoveDict = new Dictionary<string, GameObject>();
    foreach (Transform child in transform) {
      childrenToRemoveDict.Add(child.name, child.gameObject);
    }

    float terrainSizeX = terrainGrid.xSize;
    float terrainSizeZ = terrainGrid.zSize;

    // Figure out how many probes to place
    int numProbesX = Mathf.CeilToInt(terrainSizeX / (float)placementFrequency);
    int numProbesZ = Mathf.CeilToInt(terrainSizeZ / (float)placementFrequency);

    float probeUnitsX = terrainGrid.XUnitSize() / (float)numProbesX;
    float probeUnitsZ = terrainGrid.ZUnitSize() / (float)numProbesZ;

    float halfProbeUnitsX = probeUnitsX / 2f;
    float halfProbeUnitsZ = probeUnitsZ / 2f;

    int colIdxPerProbeX = Mathf.CeilToInt(terrainSizeX / (float)numProbesX)-1;
    int colIdxPerProbeZ = Mathf.CeilToInt(terrainSizeZ / (float)numProbesZ)-1;

    // Go through all the probes we need to find/build and make sure they're placed and set properly
    var tempVec2Int = new Vector2Int();
    var reflectionProbes = new List<ReflectionProbe>();
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
            var col = terrainGrid.GetTerrainColumn(tempVec2Int);
            if (col != null) { terrainCols.Add(col); }
          }
        }

        // Analyze the columns to find out where to place the probe
        //float minColY = terrainGrid.yUnitSize() + PROBE_HEIGHT_SPACING;
        float maxColY = 0;
        foreach (var col in terrainCols) {
          var bounds = col.Bounds();
          //minColY = Mathf.Min(minColY, bounds.max.y);
          maxColY = Mathf.Max(maxColY, bounds.max.y);
        }
        
        // Build the GameObject and ReflectionProbe component and add them to this
        float probeXPos = x*probeUnitsX + halfProbeUnitsX;
        float probeZPos = z*probeUnitsZ + halfProbeUnitsZ;
        
        // Find any existing probe or create a new one if it doesn't exist
        var probeGOName = "Reflection Probe (" + x + "," + z + ")";
        GameObject probeGO;
        if (childrenToRemoveDict.TryGetValue(probeGOName, out probeGO)) {
          childrenToRemoveDict.Remove(probeGOName);
        }
        else {
          probeGO = new GameObject(probeGOName);
        }
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

      // Remove unused probes
      foreach (var unusedGO in childrenToRemoveDict.Values) {
        GameObject.DestroyImmediate(unusedGO);
      }

      // Rerender all the probes that are in use
      foreach (var probe in reflectionProbes) {
        probe.gameObject.SetActive(true);
        probe.RenderProbe();
      }
    }
  }

  private void Start() {
    RegenerateProbes();
  }

  private void OnValidate() {
    Invoke("RegenerateProbes", 0);
  }


}
