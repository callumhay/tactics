using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

#pragma warning disable 649

public class ModifyTerrainCamera : MonoBehaviour {

  [Range(0.1f,5.0f)]
  [Tooltip("Size of the chunk removed or added when clicking")]
  public float radius = 0.5f;
  [SerializeField] private TerrainGrid terrainGrid;
  [SerializeField] private Image reticle;

  private static readonly Color defaultReticleColour = new Color(1,1,1,1);
  
  private IEnumerator coroutine;

  private bool CastRayFromViewportCenter(out RaycastHit hit) {
    var camera = GetComponent<Camera>();
    // Cast a ray into the scene to find out if we're even pointing at the terrain
    var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    return Physics.Raycast(ray, out hit);
  }

  private void AddIsoValuesAtHit(in RaycastHit hit, float val) {
    var hitNodes = terrainGrid.GetNodesInsideSphere(hit.point, radius, false, false, terrainGrid.YUnitSize());
    terrainGrid.AddIsoValuesToNodes(val, hitNodes);
  }

  public void OnFire1(InputValue inputValue) {
    if (Cursor.visible || !inputValue.isPressed) { return; }
    RaycastHit hit;
    if (CastRayFromViewportCenter(out hit)) {
      coroutine = ChangeReticleColour(new Color(1,0,0,1));
      StartCoroutine(coroutine);
      AddIsoValuesAtHit(hit, -1);
    }
  }
  public void OnFire2(InputValue inputValue) {
    if (Cursor.visible || !inputValue.isPressed) { return; }
    RaycastHit hit;
    if (CastRayFromViewportCenter(out hit)) {
      coroutine = ChangeReticleColour(new Color(0,1,0,1));
      StartCoroutine(coroutine);
      AddIsoValuesAtHit(hit, 1);
    }
  }

  private IEnumerator ChangeReticleColour(Color c) {
    if (!reticle) { yield break; }
    reticle.color = c;
    yield return new WaitForSeconds(0.2f);
    reticle.color = defaultReticleColour;
  }

}
