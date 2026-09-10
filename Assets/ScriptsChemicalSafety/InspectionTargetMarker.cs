using UnityEngine;

/// <summary>
/// One tap-inspectable prop in the chemical hazard-inspection stage. Plain data, in the
/// same style as ExitSign/EvacuationWaypoint — the logic lives in
/// ChemicalInspectionController, which owns a group of these. Content (id/hazard flag/
/// rationale/corrective action) is transplanted from vr-safety-training's Chemical
/// Processing site (SafetySiteFactory.cs).
///
/// Important: renderers must start with a neutral material and get tinted only by
/// ChemicalInspectionController AFTER a tap — nothing here pre-hints which targets are
/// hazards, matching the source project's "nothing is labeled or colored as a hazard
/// before inspection" design principle.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InspectionTargetMarker : MonoBehaviour
{
    [Tooltip("Stable identifier passed to HazardInspectionSession.Inspect(id).")]
    [SerializeField] private string targetId = "target";

    [Tooltip("Whether this is a real hazard or a safe look-alike.")]
    [SerializeField] private bool isHazard;

    [SerializeField] private string displayName = "Inspection target";
    [SerializeField, TextArea] private string rationale = "Explain the observed condition.";
    [SerializeField, TextArea] private string correctiveAction = "Apply the site control procedure.";

    public string TargetId => targetId;
    public bool IsHazard => isHazard;
    public string DisplayName => displayName;
    public string Rationale => rationale;
    public string CorrectiveAction => correctiveAction;

    private Collider targetCollider;
    private Renderer[] renderers;

    public Collider Collider => targetCollider != null ? targetCollider : (targetCollider = GetComponent<Collider>());

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>Applies post-tap feedback color. Called by ChemicalInspectionController only.</summary>
    public void SetFeedbackColor(Color color)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        foreach (var r in renderers)
        {
            r.material.color = color;
        }
    }
}
