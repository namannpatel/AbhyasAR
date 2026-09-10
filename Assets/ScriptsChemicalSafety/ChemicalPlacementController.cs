using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Placement for the Chemical Hazard Inspection scenario: auto-places the scenario
/// (all 4 inspection targets as one prefab) on the first suitable horizontal surface
/// the player's device detects — same best-floor-plane selection as
/// ARPlacementController.TryAutoPlaceFire, reused here since there's no fire/wall-mount
/// split for this module (everything is one scenario prefab).
/// </summary>
public class ChemicalPlacementController : MonoBehaviour
{
    [Tooltip("The chemical hazard-inspection scenario prefab (must include ChemicalInspectionController).")]
    [SerializeField] private GameObject chemicalSiteScenarioPrefab;

    [Tooltip("Plane manager scanned to auto-place the scenario, and whose visualizers get hidden once placed.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("Minimum floor plane area (m^2) to auto-place the scenario on — avoids seeding onto a tiny spurious plane before the real floor is fully scanned.")]
    [SerializeField] private float minFloorArea = 0.2f;

    public GameObject PlacedScenario { get; private set; }

    private void Update()
    {
        if (PlacedScenario == null)
        {
            TryAutoPlaceScenario();
        }
    }

    private void TryAutoPlaceScenario()
    {
        if (chemicalSiteScenarioPrefab == null || planeManager == null)
        {
            return;
        }

        ARPlane best = null;
        float bestArea = minFloorArea;
        foreach (var plane in planeManager.trackables)
        {
            if (plane.alignment != PlaneAlignment.HorizontalUp)
            {
                continue;
            }

            float area = plane.size.x * plane.size.y;
            if (area >= bestArea)
            {
                best = plane;
                bestArea = area;
            }
        }

        if (best != null)
        {
            PlaceScenario(new Pose(best.center, Quaternion.identity));
        }
    }

    private void PlaceScenario(Pose hitPose)
    {
        if (PlacedScenario != null || chemicalSiteScenarioPrefab == null)
        {
            return;
        }

        PlacedScenario = Instantiate(chemicalSiteScenarioPrefab, hitPose.position, hitPose.rotation);
        FinishPlacement();
    }

    private void FinishPlacement()
    {
        SetPlanesVisible(false);

        var inspection = PlacedScenario.GetComponentInChildren<ChemicalInspectionController>(true);

        var coordinator = FindFirstObjectByType<ChemicalSafetyCoordinator>();
        if (coordinator != null)
        {
            coordinator.Configure(inspection, PlacedScenario);
        }

        var resultsUI = FindFirstObjectByType<ChemicalResultsUI>();
        if (resultsUI != null && coordinator != null)
        {
            resultsUI.SetCoordinator(coordinator);
        }
    }

    /// <summary>
    /// Clears the current placement and re-enables plane detection so the player can
    /// place again — used by the Retry button. Deliberately does NOT reload the scene,
    /// same reasoning as ARPlacementController.ResetTraining: reloading would drop
    /// already-tracked planes.
    /// </summary>
    public void ResetTraining()
    {
        if (PlacedScenario != null)
        {
            Destroy(PlacedScenario);
            PlacedScenario = null;
        }

        SetPlanesVisible(true);
    }

    private void SetPlanesVisible(bool visible)
    {
        if (planeManager == null)
        {
            return;
        }

        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(visible);
        }
        planeManager.enabled = visible;
    }
}
