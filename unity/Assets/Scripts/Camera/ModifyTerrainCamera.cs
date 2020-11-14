using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ModifyTerrainCamera : MonoBehaviour {

  [Range(0.1f,5.0f)]
  [Tooltip("Size of the chunk removed or added when clicking")]
  public float radius = 0.5f;


  private static Color defaultReticleColour = new Color(1,1,1,1);
  private Image reticleImg;
  private IEnumerator coroutine;

  void Start() {
    var reticle = GameObject.Find("Debug Reticle");
    if (reticle != null) {
      reticleImg = reticle.GetComponent<Image>();
    }
  }

  private bool castRayFromViewportCenter(out RaycastHit hit) {
    var camera = GetComponent<Camera>();
    // Cast a ray into the scene to find out if we're even pointing at the terrain
    var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    return Physics.Raycast(ray, out hit);
  }

  private void addIsoValuesAtHit(in RaycastHit hit, float val) {
    var terrain = hit.transform.parent.GetComponent<TerrainGrid>();
    if (terrain) {
      var hitNodes = terrain.getNodesInsideSphere(hit.point, radius, false, terrain.ySize*TerrainColumn.size);
      terrain.addIsoValuesToNodes(val, hitNodes);
    }
  }

  void Update() {
    // Left mouse button = Remove from terrain
    if (Input.GetMouseButtonDown(0)) {
      RaycastHit hit;
      if (castRayFromViewportCenter(out hit)) {
        coroutine = reticleColorChange(new Color(1,0,0,1));
        StartCoroutine(coroutine);
        addIsoValuesAtHit(hit, -1);
      }
    }
    // Right mouse button = Add to terrain
    else if (Input.GetMouseButtonDown(1)) {
      RaycastHit hit;
      if (castRayFromViewportCenter(out hit)) {
        coroutine = reticleColorChange(new Color(0,1,0,1));
        StartCoroutine(coroutine);
        addIsoValuesAtHit(hit, 1);
      }
    }
  }

  private IEnumerator reticleColorChange(Color c) {
    if (!reticleImg) { yield break; }
    reticleImg.color = c;
    yield return new WaitForSeconds(0.2f);
    reticleImg.color = defaultReticleColour;
  }

}
