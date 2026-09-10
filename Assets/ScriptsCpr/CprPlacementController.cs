using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Placement for the CPR &amp; AED module: the same single-owner, tap-driven stage sequence
/// as ARPlacementController (see that file's doc comment for the general shape). Two of
/// this module's stages — the compressions and rescue-breath cycle — aren't tap-to-place
/// at all (they're interactions on the already-placed mannequin, handled by
/// CprCompressionController/CprBreathController directly); this controller only owns the
/// three real placement taps (patient, call-for-help prop, AED) plus the optional AR
/// "Learn" intro, and defers to CprResponseCoordinator.ReadyForAed to know when the AED tap
/// should become available.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class CprPlacementController : MonoBehaviour
{
    private enum Stage { Lesson, PlacePatient, CallForHelp, Aed, Done }

    [Tooltip("AR-native 'Learn' stage content (auto-playing exhibits — compression rate, hand position, 30:2 ratio, AED basics — must include a FireSafetyLessonController; reused as-is, it's not fire-specific). Placed on the floor at the first tap. Leave empty to skip.")]
    [SerializeField] private GameObject cprLessonPrefab;

    [Tooltip("The mannequin — must include a CprCompressionController on its chest, a CprBreathController on its head, and an initially-inactive AED-pad-markers child holding a FireHazardInspectionController + FireHazardMarker children. Placed on the floor at the next tap.")]
    [SerializeField] private GameObject mannequinPrefab;

    [Tooltip("The call-for-help prop (e.g. a phone) — must include a ManualCallPointController (reused as-is). Placed at the next tap.")]
    [SerializeField] private GameObject callPointPrefab;

    [Tooltip("The AED device — must include a CprAedController. Placed on the floor once compressions and breaths are both done.")]
    [SerializeField] private GameObject aedPrefab;

    [Tooltip("Plane manager scanned for taps and whose visualizers get hidden once every stage is placed.")]
    [SerializeField] private ARPlaneManager planeManager;

    private ARRaycastManager raycastManager;
    private CprResponseCoordinator coordinator;
    private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private List<Stage> activeStages;
    private int stageIndex;

    public GameObject PlacedLesson { get; private set; }
    public FireSafetyLessonController LessonController { get; private set; }
    public GameObject PlacedMannequin { get; private set; }
    public GameObject PlacedCallPoint { get; private set; }
    public GameObject PlacedAed { get; private set; }

    private GameObject aedPadsRoot;

    /// <summary>1-based index of the stage currently awaiting a tap or in progress, for a "Step X/Y" HUD.</summary>
    public int CurrentStageNumber => stageIndex + 1;

    /// <summary>Total number of stages actually wired on this scene (unassigned prefab fields are skipped).</summary>
    public int TotalStages => activeStages?.Count ?? 0;

    private Stage CurrentStage => activeStages != null && stageIndex < activeStages.Count ? activeStages[stageIndex] : Stage.Done;

    private void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        coordinator = FindFirstObjectByType<CprResponseCoordinator>();
        BuildActiveStages();
    }

    private void BuildActiveStages()
    {
        activeStages = new List<Stage>();
        if (cprLessonPrefab != null) activeStages.Add(Stage.Lesson);
        activeStages.Add(Stage.PlacePatient);
        activeStages.Add(Stage.CallForHelp);
        activeStages.Add(Stage.Aed);
        stageIndex = 0;
    }

    /// <summary>
    /// The tap prompt for whatever the current stage still needs placed, or null if the
    /// current stage is already placed, or not yet ready to be placed (e.g. the AED before
    /// compressions/breaths are done) — CprInstructionsUI falls back to its own
    /// fine-grained in-stage messages (compression rate, breaths remaining, pad placement)
    /// in either of those null cases, same two-layer pattern as TrainingInstructionsUI.
    /// </summary>
    public string CurrentPlacementPrompt()
    {
        switch (CurrentStage)
        {
            case Stage.Lesson: return PlacedLesson == null ? "Tap the floor to begin the lesson" : null;
            case Stage.PlacePatient: return PlacedMannequin == null ? "Tap the floor to place the patient" : null;
            case Stage.CallForHelp: return PlacedCallPoint == null ? "Tap to place a phone and call for help" : null;
            case Stage.Aed:
                if (PlacedAed != null) return null;
                return (coordinator != null && coordinator.ReadyForAed) ? "Tap the floor to bring in the AED" : null;
            default: return null;
        }
    }

    private void Update()
    {
        AdvanceStageIfCurrentComplete();

        if (CurrentStage == Stage.Done)
        {
            return;
        }

        if (!IsCurrentStagePlaced() && ArTouchInput.TryGetTapPosition(out Vector2 screenPos))
        {
            TryPlaceCurrentStage(screenPos);
        }
    }

    private bool IsCurrentStagePlaced()
    {
        switch (CurrentStage)
        {
            case Stage.Lesson: return PlacedLesson != null;
            case Stage.PlacePatient: return PlacedMannequin != null;
            case Stage.CallForHelp: return PlacedCallPoint != null;
            // Also "placed" (i.e. don't attempt to place) while not yet ready — blocks a
            // stray tap from placing the AED before compressions/breaths are done.
            case Stage.Aed: return PlacedAed != null || coordinator == null || !coordinator.ReadyForAed;
            default: return true;
        }
    }

    private void AdvanceStageIfCurrentComplete()
    {
        if (activeStages == null || stageIndex >= activeStages.Count)
        {
            return;
        }

        bool done;
        switch (CurrentStage)
        {
            case Stage.Lesson:
                done = LessonController != null && LessonController.IsComplete;
                break;
            case Stage.PlacePatient:
                done = PlacedMannequin != null;
                break;
            case Stage.CallForHelp:
                done = PlacedCallPoint != null;
                break;
            case Stage.Aed:
                done = PlacedAed != null; // terminal — module completion is tracked by CprResponseCoordinator, not this stage machine
                break;
            default:
                done = false;
                break;
        }

        if (!done)
        {
            return;
        }

        stageIndex++;
        if (CurrentStage == Stage.Done)
        {
            SetPlanesVisible(false);
        }
    }

    private void TryPlaceCurrentStage(Vector2 screenPos)
    {
        if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
        {
            if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinBounds))
            {
                return;
            }
        }

        ARPlane plane = hits[0].trackable as ARPlane;
        if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp)
        {
            return; // every CPR stage places on the floor — no wall-mounted content here
        }

        Pose hitPose = hits[0].pose;

        switch (CurrentStage)
        {
            case Stage.Lesson:
                PlaceLessonAt(hitPose);
                break;
            case Stage.PlacePatient:
                PlacePatientAt(hitPose);
                break;
            case Stage.CallForHelp:
                PlaceCallPointAt(hitPose);
                break;
            case Stage.Aed:
                PlaceAedAt(hitPose);
                break;
        }
    }

    private void PlaceLessonAt(Pose pose)
    {
        PlacedLesson = Instantiate(cprLessonPrefab, pose.position, Quaternion.identity);
        LessonController = PlacedLesson.GetComponentInChildren<FireSafetyLessonController>(true);
    }

    private void PlacePatientAt(Pose pose)
    {
        PlacedMannequin = Instantiate(mannequinPrefab, pose.position, Quaternion.identity);

        var compressionController = PlacedMannequin.GetComponentInChildren<CprCompressionController>(true);
        var breathController = PlacedMannequin.GetComponentInChildren<CprBreathController>(true);
        coordinator?.ConfigurePatient(compressionController, breathController);

        var padsController = PlacedMannequin.GetComponentInChildren<FireHazardInspectionController>(true);
        aedPadsRoot = padsController != null ? padsController.gameObject : null;
    }

    private void PlaceCallPointAt(Pose pose)
    {
        PlacedCallPoint = Instantiate(callPointPrefab, pose.position, Quaternion.identity);
        var callPointController = PlacedCallPoint.GetComponentInChildren<ManualCallPointController>(true);
        coordinator?.ConfigureCallPoint(callPointController);
    }

    private void PlaceAedAt(Pose pose)
    {
        PlacedAed = Instantiate(aedPrefab, pose.position, Quaternion.identity);
        var aedController = PlacedAed.GetComponentInChildren<CprAedController>(true);

        FireHazardInspectionController padsController = null;
        if (aedPadsRoot != null)
        {
            aedPadsRoot.SetActive(true); // triggers its OnEnable -> BuildSession(), same lazy-activation pattern FireResponseCoordinator uses for the evacuation stage root
            padsController = aedPadsRoot.GetComponent<FireHazardInspectionController>();
        }

        coordinator?.ConfigureAed(padsController, aedController);
    }

    /// <summary>
    /// Clears the current placement, resets the stage sequence to the start, and
    /// re-enables plane detection — used by the Retry button. Deliberately does NOT
    /// reload the scene, for the same reason ARPlacementController.ResetTraining doesn't:
    /// destroying/recreating the AR Session on retry loses already-tracked planes.
    /// </summary>
    public void ResetTraining()
    {
        if (PlacedAed != null)
        {
            Destroy(PlacedAed);
            PlacedAed = null;
        }

        if (PlacedCallPoint != null)
        {
            Destroy(PlacedCallPoint);
            PlacedCallPoint = null;
        }

        if (PlacedMannequin != null)
        {
            Destroy(PlacedMannequin);
            PlacedMannequin = null;
            aedPadsRoot = null;
        }

        if (PlacedLesson != null)
        {
            Destroy(PlacedLesson);
            PlacedLesson = null;
            LessonController = null;
        }

        stageIndex = 0;
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
