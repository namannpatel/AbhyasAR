using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hazard-inspection stage: owns a group of InspectionTargetMarker children, scores
/// taps against a HazardInspectionSession built from their specs, and applies post-tap
/// visual feedback (green = correctly identified hazard, amber = safe object
/// mis-tapped) — same color choices as vr-safety-training's InspectionTarget.cs, for
/// continuity with the source content. Inactive/disabled until
/// ChemicalSafetyCoordinator activates it (after PPE selection).
/// </summary>
public class ChemicalInspectionController : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    private static readonly Color CorrectHazardColor = new Color(0.16f, 0.75f, 0.32f);
    private static readonly Color SafeObjectMistapColor = new Color(1f, 0.66f, 0.1f);

    [Tooltip("Raised once, when both real hazards have been correctly identified.")]
    public UnityEvent OnInspectionComplete;

    public int HazardsFound => session?.HazardsFound ?? 0;
    public int HazardsRequired => session?.HazardsRequired ?? 0;
    public int FalsePositives => session?.FalsePositives ?? 0;
    public int Score => session?.Score ?? 0;
    public bool IsComplete => session != null && session.IsComplete;

    private InspectionTargetMarker[] markers;
    private HazardInspectionSession session;

    private void Awake()
    {
        BuildSession();
    }

    private void OnEnable()
    {
        // Re-scan/rebuild in case markers were added after Awake (e.g. instantiated by
        // ChemicalPlacementController), and reset state for a fresh attempt.
        BuildSession();
    }

    private void BuildSession()
    {
        markers = GetComponentsInChildren<InspectionTargetMarker>(true);

        var specs = new List<InspectionTargetSpec>(markers.Length);
        foreach (var marker in markers)
        {
            specs.Add(new InspectionTargetSpec(marker.TargetId, marker.IsHazard));
        }

        session = specs.Count > 0 ? new HazardInspectionSession(specs) : null;
    }

    private void Update()
    {
        if (session == null || session.IsComplete)
        {
            return;
        }

        if (!ArTouchInput.TryGetTapPosition(out Vector2 screenPos))
        {
            return;
        }

        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        InspectionTargetMarker tapped = RaycastMarkers(ray);
        if (tapped == null)
        {
            return;
        }

        var result = session.Inspect(tapped.TargetId);
        switch (result.Outcome)
        {
            case InspectionOutcome.CorrectHazard:
                tapped.SetFeedbackColor(CorrectHazardColor);
                break;
            case InspectionOutcome.SafeObjectSelected:
                tapped.SetFeedbackColor(SafeObjectMistapColor);
                break;
            default:
                return;
        }

        if (result.IsComplete)
        {
            OnInspectionComplete?.Invoke();
        }
    }

    private InspectionTargetMarker RaycastMarkers(Ray ray)
    {
        InspectionTargetMarker closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (var marker in markers)
        {
            if (marker == null || marker.Collider == null)
            {
                continue;
            }

            if (marker.Collider.Raycast(ray, out RaycastHit hit, float.PositiveInfinity) && hit.distance < closestDistance)
            {
                closest = marker;
                closestDistance = hit.distance;
            }
        }

        return closest;
    }
}
