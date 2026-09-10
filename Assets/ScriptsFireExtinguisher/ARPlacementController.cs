using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Placement for the training scenario: a single-owner, tap-driven stage sequence. Only
/// one stage is ever "listening" for a tap at a time, and each stage names the surface
/// type it needs (floor vs. wall) — a tap on the wrong surface type is ignored rather
/// than misinterpreted as a different stage's placement.
///
/// Stage order: tap a wall to mount the alarm and the extinguisher choices together, then
/// tap the floor to place the fire. Both floor and wall planes are scanned (with the blue
/// plane overlay visible the whole time — see ARPlaneVisualizer.mat), so the "keep
/// scanning" message only asks for whichever surface the *current* stage still needs, not
/// both up front.
///
/// Every placed object is attached to a real ARAnchor (see CreateAnchor) rather than a
/// bare Transform, so it stays put relative to the room as the device's own tracking
/// keeps refining itself — without an anchor, world-placed AR content visibly drifts/
/// slides as you move, which reads exactly like it's "stuck to the screen" following you.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ARPlacementController : MonoBehaviour
{
    private enum Stage { Lesson, WallContent, Fire, Extinguisher, Done }

    [Tooltip("AR-native 'Learn' stage content (auto-playing exhibits — must include a FireSafetyLessonController). Placed on the floor at the first tap. Leave empty to skip.")]
    [SerializeField] private GameObject fireSafetyLessonPrefab;

    [Tooltip("Activate-stage content: a single manual call point (must include a ManualCallPointController). Placed on the wall, together with the extinguisher choices, at the next wall tap. Leave empty to skip.")]
    [SerializeField] private GameObject manualCallPointPrefab;

    [Tooltip("The scenario options placed on the floor at the next floor tap (each must include FireSource, and may include its own ignition-cause dressing) — one is picked at random per attempt so the module isn't locked to a single scenario.")]
    [SerializeField] private GameObject[] firePrefabOptions;

    [Tooltip("The extinguisher choices placed on the wall, alongside the alarm, at the same tap (mix of correct and incorrect for variety — each must include ExtinguisherPickup + PassChecklistTracker).")]
    [SerializeField] private GameObject[] extinguisherOptionPrefabs;

    [Tooltip("Distance (meters) between adjacent extinguisher options along the wall.")]
    [SerializeField] private float extinguisherSpacing = 0.5f;

    [Tooltip("Vertical offset (meters) of the alarm button below the tapped wall point / extinguisher row, so it doesn't overlap the extinguishers mounted at the tap itself.")]
    [SerializeField] private float callPointVerticalOffset = -0.5f;

    [Tooltip("Plane manager scanned for taps and whose visualizers get hidden once every stage is placed.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("Creates the ARAnchor each placed object is attached to, so it doesn't drift as AR tracking refines. Auto-found on this GameObject if left empty.")]
    [SerializeField] private ARAnchorManager anchorManager;

    private ARRaycastManager raycastManager;
    private FireResponseCoordinator coordinator;
    private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private readonly List<GameObject> placedExtinguisherOptions = new List<GameObject>();

    private List<Stage> activeStages;
    private int stageIndex;
    private bool planesVisible = true;

    // Sequential campaign: every scenario in firePrefabOptions gets trained exactly once,
    // in a shuffled order fixed for the whole session -- not a fresh random pick each
    // attempt. The wall/floor taps from the first placement are remembered so each later
    // scenario auto-places without asking the player to re-tap the room.
    private GameObject[] scenarioOrder;
    private int scenarioIndex;
    private Pose lastFloorPose;
    private ARPlane lastFloorPlane;
    private Pose lastWallPose;
    private ARPlane lastWallPlane;

    public IReadOnlyList<GameObject> PlacedExtinguisherOptions => placedExtinguisherOptions;
    public GameObject ChosenExtinguisher { get; private set; }
    public GameObject PlacedFire { get; private set; }
    public GameObject PlacedLesson { get; private set; }
    public FireSafetyLessonController LessonController { get; private set; }
    public GameObject PlacedCallPoint { get; private set; }
    public ManualCallPointController CallPointController { get; private set; }

    /// <summary>1-based index of the stage currently awaiting a tap or in progress, for a "Step X/Y" HUD.</summary>
    public int CurrentStageNumber => stageIndex + 1;

    /// <summary>Total number of stages actually wired on this scene (unassigned prefab fields are skipped).</summary>
    public int TotalStages => activeStages?.Count ?? 0;

    /// <summary>1-based index of the scenario currently being trained, out of TotalScenarios — for "Scenario X/Y" on the results screen.</summary>
    public int ScenarioNumber => scenarioIndex + 1;

    /// <summary>How many scenarios this campaign runs through in total.</summary>
    public int TotalScenarios => scenarioOrder?.Length ?? (firePrefabOptions?.Length ?? 0);

    /// <summary>True once there's a next scenario left to train — drives the results screen's NEXT button.</summary>
    public bool HasNextScenario => scenarioOrder != null && scenarioIndex < scenarioOrder.Length - 1;

    private Stage CurrentStage => activeStages != null && stageIndex < activeStages.Count ? activeStages[stageIndex] : Stage.Done;

    /// <summary>"wall" or "floor" — the surface type the current stage's tap needs, for scanning-feedback wording.</summary>
    public string CurrentStageSurfaceName => CurrentStage == Stage.WallContent ? "wall" : "floor";

    /// <summary>
    /// True for any plane that isn't clearly a floor/ceiling -- deliberately more permissive
    /// than requiring PlaneAlignment.Vertical exactly. In the field, ARCore/ARKit frequently
    /// classify a real, roughly-vertical wall as NotAxisAligned rather than strictly Vertical
    /// (tracking noise, a wall that isn't perfectly plumb, or just early in detection) --
    /// requiring an exact Vertical match silently rejected those real walls, which is why
    /// floor detection worked reliably (floors consistently classify as HorizontalUp) while
    /// wall detection did not.
    /// </summary>
    private static bool IsWallPlane(ARPlane plane)
    {
        return plane.alignment != PlaneAlignment.HorizontalUp && plane.alignment != PlaneAlignment.HorizontalDown;
    }

    /// <summary>
    /// True when the current stage still needs a tap but ARCore/ARKit hasn't found a
    /// plane of the required type (wall vs. floor) yet — drives a "keep scanning" message
    /// instead of a bare tap prompt that looks stuck with no feedback while the player is
    /// still moving the phone around the room.
    /// </summary>
    public bool IsScanningForSurface
    {
        get
        {
            if (planeManager == null || CurrentStage == Stage.Done || IsCurrentStagePlaced())
            {
                return false;
            }

            bool needsWall = CurrentStage == Stage.WallContent;
            foreach (var plane in planeManager.trackables)
            {
                bool matchesNeed = needsWall ? IsWallPlane(plane) : plane.alignment == PlaneAlignment.HorizontalUp;
                if (matchesNeed)
                {
                    return false; // at least one plane of the needed type already exists
                }
            }
            return true;
        }
    }

    private void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        if (anchorManager == null)
        {
            anchorManager = GetComponent<ARAnchorManager>();
        }
        coordinator = FindFirstObjectByType<FireResponseCoordinator>();
        BuildActiveStages();

        if (planeManager != null)
        {
            planeManager.trackablesChanged.AddListener(HandlePlanesChanged);
        }
    }

    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.trackablesChanged.RemoveListener(HandlePlanesChanged);
        }
    }

    /// <summary>
    /// Builds the ordered list of stages this scene actually uses, skipping any whose
    /// prefab field is left unassigned (preserves "leave empty to skip" for scenes that
    /// don't wire every stage). Fire and Extinguisher are always present — there's no
    /// training attempt without them. Extinguisher has no placement tap of its own (it's
    /// placed together with WallContent); it stays as a distinct stage purely so the
    /// "Step X/Y" HUD numbering still counts it as its own step of the attempt.
    /// </summary>
    private void BuildActiveStages()
    {
        activeStages = new List<Stage>();
        if (fireSafetyLessonPrefab != null) activeStages.Add(Stage.Lesson);
        // Fire goes down before the wall content (alarm + extinguishers) so the "sound the
        // alarm"/"mount the extinguishers" prompt never appears while there's no fire yet
        // to respond to -- the opening alert modal (see PlaceFireAt) is also what frames
        // the scenario, so it needs to fire first too.
        activeStages.Add(Stage.Fire);
        if (manualCallPointPrefab != null) activeStages.Add(Stage.WallContent);
        activeStages.Add(Stage.Extinguisher);
        stageIndex = 0;
    }

    /// <summary>
    /// Returns a LocalizationManager key (not display text) for whatever the current stage
    /// still needs placed, or null if the current stage is already placed (and
    /// TrainingInstructionsUI should fall back to its own fine-grained in-stage messages —
    /// pulling the pin, aiming, etc.). Returning a key rather than English text lets the
    /// caller localize it, and doubles as the matching NarrationPlayer clip id for two of
    /// the three prompts (see TrainingInstructionsUI).
    /// </summary>
    public string CurrentPlacementPrompt()
    {
        switch (CurrentStage)
        {
            case Stage.Lesson: return PlacedLesson == null ? "prompt_lesson" : null;
            case Stage.WallContent: return PlacedCallPoint == null ? "prompt_place_wall_content" : null;
            case Stage.Fire: return PlacedFire == null ? "prompt_place_fire" : null;
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
            case Stage.WallContent: return PlacedCallPoint != null;
            case Stage.Fire: return PlacedFire != null;
            case Stage.Extinguisher: return placedExtinguisherOptions.Count > 0;
            default: return true;
        }
    }

    /// <summary>
    /// Moves to the next stage once the current one's own completion condition is met —
    /// the same per-stage "done" flags already used for scoring, just also driving when
    /// the next tap prompt appears.
    /// </summary>
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
            case Stage.WallContent:
                done = CallPointController != null && CallPointController.IsActivated;
                break;
            case Stage.Fire:
                done = PlacedFire != null;
                break;
            case Stage.Extinguisher:
                done = coordinator != null && coordinator.ExtinguisherStageDone;
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
            // A tap can land just outside the currently-tracked polygon edge — very
            // common while a plane is still growing, especially vertical ones — so
            // fall back to the plane's tracked bounds before giving up on this tap.
            if (!raycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinBounds))
            {
                return;
            }
        }

        ARPlane plane = hits[0].trackable as ARPlane;
        if (plane == null)
        {
            return;
        }

        bool needsWall = CurrentStage == Stage.WallContent;
        if (needsWall && !IsWallPlane(plane))
        {
            return;
        }
        if (!needsWall && plane.alignment != PlaneAlignment.HorizontalUp)
        {
            return;
        }

        Pose hitPose = hits[0].pose;

        switch (CurrentStage)
        {
            case Stage.Lesson:
                PlaceLessonAt(hitPose, plane);
                break;
            case Stage.WallContent:
                PlaceCallPointAt(hitPose, plane);
                PlaceExtinguisherOptions(hitPose, plane);
                break;
            case Stage.Fire:
                PlaceFireAt(hitPose, plane);
                break;
        }

        // Drop the scanning overlay the moment every tap-placed artifact actually exists,
        // not when the whole attempt finishes (Extinguisher's own "done" flag only flips
        // once the fire is fully out -- that's minutes away, and leaves the blue plane
        // overlay cluttering the view through the entire fight). WallContent's own stage
        // "done" condition is deliberately "alarm pressed" (see AdvanceStageIfCurrentComplete)
        // for HUD/scoring purposes, which is a different question from "has everything been
        // placed" -- checked separately here.
        if (AllPlacementTapsComplete())
        {
            SetPlanesVisible(false);
        }
    }

    /// <summary>
    /// True once every stage that places something via a tap has actually placed it --
    /// independent of whether that content has since been used/activated (WallContent's
    /// own stage-advancement condition waits for the alarm to be pressed, not just mounted;
    /// see AdvanceStageIfCurrentComplete). Drives when the blue scanning overlay disappears.
    /// </summary>
    private bool AllPlacementTapsComplete()
    {
        if (fireSafetyLessonPrefab != null && PlacedLesson == null) return false;
        if (PlacedFire == null) return false;
        if (manualCallPointPrefab != null && PlacedCallPoint == null) return false;
        return true;
    }

    /// <summary>
    /// Creates a real ARAnchor at the given pose (attached to the plane it was tapped on,
    /// so it tracks the same refinements the plane itself receives) and returns its
    /// transform — parent placed content under this instead of leaving it as a bare
    /// scene-root object at a static Transform, or it will visibly drift as the device's
    /// tracking keeps refining its understanding of the room. Falls back to a plain
    /// Transform (old behavior) if no ARAnchorManager is wired, or if the platform has no
    /// anchor subsystem available (e.g. testing in the Editor without a live AR session,
    /// where AttachAnchor throws rather than returning null) — placement still works
    /// rather than the whole attempt breaking.
    /// </summary>
    private Transform CreateAnchor(Pose pose, ARPlane plane, string name)
    {
        if (anchorManager != null)
        {
            try
            {
                ARAnchor anchor = anchorManager.AttachAnchor(plane, pose);
                if (anchor != null)
                {
                    anchor.gameObject.name = name;
                    return anchor.transform;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Fall through to the plain-Transform fallback below.
            }
        }

        var fallback = new GameObject(name);
        fallback.transform.SetPositionAndRotation(pose.position, pose.rotation);
        return fallback.transform;
    }

    private void PlaceLessonAt(Pose pose, ARPlane floor)
    {
        Transform anchor = CreateAnchor(pose, floor, "LessonAnchor");
        PlacedLesson = Instantiate(fireSafetyLessonPrefab, Vector3.zero, Quaternion.identity, anchor);
        PlacedLesson.transform.localPosition = Vector3.zero;
        PlacedLesson.transform.localRotation = Quaternion.identity;
        LessonController = PlacedLesson.GetComponentInChildren<FireSafetyLessonController>(true);
    }

    private void PlaceCallPointAt(Pose pose, ARPlane wall)
    {
        // Remembered so AdvanceToNextScenario can re-place fresh extinguisher options here
        // for each later scenario without asking the player to re-tap the wall.
        lastWallPose = pose;
        lastWallPlane = wall;

        // Mounted below the extinguisher row (see PlaceExtinguisherOptions, which sits at
        // the tap point itself) so the two don't overlap.
        Vector3 position = pose.position + wall.transform.up * callPointVerticalOffset;
        Quaternion rotation = Quaternion.LookRotation(wall.normal, Vector3.up);
        Transform anchor = CreateAnchor(new Pose(position, rotation), wall, "CallPointAnchor");
        PlacedCallPoint = Instantiate(manualCallPointPrefab, Vector3.zero, Quaternion.identity, anchor);
        PlacedCallPoint.transform.localPosition = Vector3.zero;
        PlacedCallPoint.transform.localRotation = Quaternion.identity;
        CallPointController = PlacedCallPoint.GetComponentInChildren<ManualCallPointController>(true);
        coordinator?.ConfigureActivateStage(CallPointController);
    }

    /// <summary>
    /// Builds the fixed, shuffled-once order every scenario in firePrefabOptions is
    /// trained in across the campaign, the first time it's needed. Shuffled (not just
    /// firePrefabOptions' own order) so which scenario comes first still varies between
    /// sessions, while every scenario still gets trained exactly once per campaign.
    /// </summary>
    private void EnsureScenarioOrder()
    {
        if (scenarioOrder != null)
        {
            return;
        }

        if (firePrefabOptions == null || firePrefabOptions.Length == 0)
        {
            scenarioOrder = new GameObject[0];
            return;
        }

        scenarioOrder = (GameObject[])firePrefabOptions.Clone();
        for (int i = scenarioOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (scenarioOrder[i], scenarioOrder[j]) = (scenarioOrder[j], scenarioOrder[i]);
        }
    }

    private void PlaceFireAt(Pose pose, ARPlane floor)
    {
        EnsureScenarioOrder();
        if (scenarioOrder.Length == 0)
        {
            return;
        }

        // Remembered so AdvanceToNextScenario can re-place each later scenario at the same
        // floor spot without asking the player to re-tap the room.
        lastFloorPose = pose;
        lastFloorPlane = floor;

        GameObject chosen = scenarioOrder[scenarioIndex];
        if (chosen == null)
        {
            return;
        }

        Transform anchor = CreateAnchor(pose, floor, "FireAnchor");
        PlacedFire = Instantiate(chosen, Vector3.zero, Quaternion.identity, anchor);
        PlacedFire.transform.localPosition = Vector3.zero;
        PlacedFire.transform.localRotation = Quaternion.identity;

        // The fire may not exist yet when the wall content (including the extinguishers)
        // was placed — wire every already-placed extinguisher's target now that it does.
        var placedFireSource = PlacedFire.GetComponent<FireSource>();
        foreach (var option in placedExtinguisherOptions)
        {
            var tracker = option != null ? option.GetComponent<PassChecklistTracker>() : null;
            if (tracker != null && placedFireSource != null)
            {
                tracker.SetTargetFire(placedFireSource);
            }
        }

        // Frame whichever scenario actually got picked -- the alert modal only knows the
        // right wording (not always "trash can") once the fire exists to ask it.
        var alertIntro = FindFirstObjectByType<FireAlertIntro>();
        if (alertIntro != null && placedFireSource != null)
        {
            alertIntro.Show(placedFireSource.scenarioNarrationId);
        }
    }

    /// <summary>
    /// Instantiates every entry in extinguisherOptionPrefabs along the tapped wall, in a
    /// shuffled left-to-right order so the correct choice isn't always in the same slot.
    /// The fire doesn't necessarily exist yet at this point (wall content is placed before
    /// the floor tap that places it) — each option's target fire is wired here if it
    /// already exists, and PlaceFireAt backfills it for any that placed first.
    /// </summary>
    private void PlaceExtinguisherOptions(Pose hitPose, ARPlane wall)
    {
        if (placedExtinguisherOptions.Count > 0 || extinguisherOptionPrefabs == null || extinguisherOptionPrefabs.Length == 0)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(wall.normal, Vector3.up);

        Vector3 tangent = Vector3.Cross(Vector3.up, wall.normal);
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = wall.transform.up;
        }
        tangent.Normalize();

        // Row sits at the tap point itself -- the alarm mounts below it (see PlaceCallPointAt).
        Vector3 rowCenter = hitPose.position;

        var shuffled = (GameObject[])extinguisherOptionPrefabs.Clone();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        FireSource fireSource = PlacedFire != null ? PlacedFire.GetComponent<FireSource>() : null;

        float mid = (shuffled.Length - 1) / 2f;
        for (int i = 0; i < shuffled.Length; i++)
        {
            if (shuffled[i] == null)
            {
                continue;
            }

            Vector3 position = rowCenter + tangent * ((i - mid) * extinguisherSpacing);
            Transform anchor = CreateAnchor(new Pose(position, rotation), wall, $"ExtinguisherAnchor{i}");
            GameObject instance = Instantiate(shuffled[i], Vector3.zero, Quaternion.identity, anchor);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            placedExtinguisherOptions.Add(instance);

            var tracker = instance.GetComponent<PassChecklistTracker>();
            if (tracker != null && fireSource != null)
            {
                tracker.SetTargetFire(fireSource);
            }

            var pickup = instance.GetComponent<ExtinguisherPickup>();
            if (pickup != null)
            {
                // Must run after the localPosition/localRotation fix-up above -- see
                // CaptureRestPose's own comment for why Awake()'s own snapshot can't be
                // trusted as the "put it back here" pose for a rejected pickup.
                pickup.CaptureRestPose();

                GameObject capturedInstance = instance;
                pickup.OnPickedUp.AddListener(() => HandleExtinguisherChosen(capturedInstance));
            }
        }
    }

    /// <summary>
    /// Called once, the moment the player picks up an extinguisher option. If it isn't
    /// rated for the current fire, the pickup is rejected outright — put back where it
    /// was mounted, a warning shown, and every option (including this one) stays
    /// available so the player can try a different one instead of being locked into a
    /// losing attempt. Only a correctly-rated pickup actually commits: wires its tracker
    /// into the module coordinator and disables pickup on the other options (no back-out/
    /// reselect once genuinely committed). Also backfills SetTargetFire here in case the
    /// fire still didn't exist when this option was placed and PlaceFireAt hadn't run yet
    /// either (shouldn't happen in the normal wall-then-floor order, but keeps pickup
    /// correctness checks reliable regardless of exactly when each stage was placed).
    /// </summary>
    private void HandleExtinguisherChosen(GameObject chosen)
    {
        if (ChosenExtinguisher != null || chosen == null)
        {
            return;
        }

        var pickup = chosen.GetComponent<ExtinguisherPickup>();
        var identity = chosen.GetComponentInChildren<ExtinguisherIdentity>();
        var fireSource = PlacedFire != null ? PlacedFire.GetComponent<FireSource>() : null;

        var trackerForFire = chosen.GetComponent<PassChecklistTracker>();
        if (trackerForFire != null && fireSource != null)
        {
            trackerForFire.SetTargetFire(fireSource);
        }

        if (identity != null && fireSource != null && !identity.CanExtinguish(fireSource.fireClass))
        {
            pickup?.UndoPickup();
            coordinator?.OnWrongExtinguisherWarning?.Invoke();
            return;
        }

        ChosenExtinguisher = chosen;

        foreach (var option in placedExtinguisherOptions)
        {
            if (option == null || option == chosen)
            {
                continue;
            }

            var otherPickup = option.GetComponent<ExtinguisherPickup>();
            if (otherPickup != null)
            {
                otherPickup.enabled = false;
            }
        }

        var tracker = chosen.GetComponent<PassChecklistTracker>();

        if (coordinator != null && tracker != null)
        {
            coordinator.ConfigureExtinguisher(tracker);
        }

        var resultsUI = FindFirstObjectByType<TrainingResultsUI>();
        if (resultsUI != null && coordinator != null)
        {
            resultsUI.SetCoordinator(coordinator);
        }

        var resultsModal = FindFirstObjectByType<FireResultsModal>();
        if (resultsModal != null && coordinator != null)
        {
            resultsModal.SetCoordinator(coordinator);
        }
    }

    /// <summary>
    /// Clears the current placement, resets the stage sequence to the start, and
    /// re-enables plane detection so the player can place again — used by the Retry
    /// button. Deliberately does NOT reload the scene: destroying/recreating the AR
    /// Session/XR Origin on every retry is what caused "wall no longer detected after
    /// retry" — planes already being tracked stay tracked, so placement works
    /// immediately instead of needing the camera to re-scan the room from scratch.
    /// </summary>
    public void ResetTraining()
    {
        foreach (var option in placedExtinguisherOptions)
        {
            DestroyPlacedExtinguisher(option);
        }
        placedExtinguisherOptions.Clear();
        ChosenExtinguisher = null;

        if (PlacedFire != null)
        {
            Destroy(PlacedFire.transform.parent != null ? PlacedFire.transform.parent.gameObject : PlacedFire);
            PlacedFire = null;
        }

        if (PlacedLesson != null)
        {
            Destroy(PlacedLesson.transform.parent != null ? PlacedLesson.transform.parent.gameObject : PlacedLesson);
            PlacedLesson = null;
            LessonController = null;
        }

        if (PlacedCallPoint != null)
        {
            Destroy(PlacedCallPoint.transform.parent != null ? PlacedCallPoint.transform.parent.gameObject : PlacedCallPoint);
            PlacedCallPoint = null;
            CallPointController = null;
        }

        stageIndex = 0;
        SetPlanesVisible(true);
    }

    /// <summary>
    /// Moves the campaign on to the next scenario in sequence, called by the results
    /// screen's NEXT button. Clears the current fire and extinguisher choices (the alarm
    /// stays mounted — it doesn't need re-activating for each scenario) and re-places
    /// fresh ones at the same wall/floor spots already tapped, so training the next
    /// scenario doesn't require scanning or tapping the room again.
    /// </summary>
    public void AdvanceToNextScenario()
    {
        if (!HasNextScenario)
        {
            return;
        }

        scenarioIndex++;

        foreach (var option in placedExtinguisherOptions)
        {
            DestroyPlacedExtinguisher(option);
        }
        placedExtinguisherOptions.Clear();
        ChosenExtinguisher = null;

        if (PlacedFire != null)
        {
            Destroy(PlacedFire.transform.parent != null ? PlacedFire.transform.parent.gameObject : PlacedFire);
            PlacedFire = null;
        }

        // Fire first (matches the campaign's normal stage order) so the extinguisher
        // options below get PlacedFire's FireSource wired immediately instead of relying
        // on PlaceFireAt's backfill for options placed before any fire existed.
        if (lastFloorPlane != null)
        {
            PlaceFireAt(lastFloorPose, lastFloorPlane);
        }
        if (lastWallPlane != null)
        {
            PlaceExtinguisherOptions(lastWallPose, lastWallPlane);
        }
    }

    /// <summary>
    /// Safely destroys a placed extinguisher option and the anchor it was mounted under.
    /// Never destroys via option.transform.parent directly -- if the player had picked this
    /// extinguisher up, ExtinguisherPickup.PickUp reparents it under the AR camera for the
    /// held/aiming pose, so transform.parent at that point IS the scene camera, not an
    /// anchor. Destroying that (as this used to, in Retry and AdvanceToNextScenario) took
    /// the actual AR camera down with it -- breaking raycasting, rendering, and every UI
    /// button on screen (see ExtinguisherPickup.HomeAnchor, which stays correct regardless
    /// of hold state).
    /// </summary>
    private static void DestroyPlacedExtinguisher(GameObject option)
    {
        if (option == null)
        {
            return;
        }

        var pickup = option.GetComponent<ExtinguisherPickup>();
        Transform homeAnchor = pickup != null ? pickup.HomeAnchor : option.transform.parent;

        Destroy(option);

        if (homeAnchor != null)
        {
            Destroy(homeAnchor.gameObject);
        }
    }

    /// <summary>
    /// Toggles the blue scanning-overlay visuals only -- deliberately never touches
    /// planeManager.enabled. Disabling an ARPlaneManager calls SubsystemLifecycleManager.
    /// OnDisable(), which stops the underlying native plane-tracking subsystem outright;
    /// re-enabling it restarts that subsystem asynchronously and can require the device to
    /// redetect planes from scratch. That's exactly what broke Retry after the overlay
    /// started hiding itself right after placement instead of only at the very end of the
    /// whole attempt: hitting Retry mid-session re-enabled the manager, but tracking hadn't
    /// necessarily come back yet, so the very next tap had no plane to raycast against and
    /// looked like Retry "did nothing." Hiding the plane GameObjects' renderers is enough
    /// to make the overlay disappear without ever touching tracking itself.
    /// </summary>
    private void SetPlanesVisible(bool visible)
    {
        planesVisible = visible;

        if (planeManager == null)
        {
            return;
        }

        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Keeps newly-detected planes in sync with the last SetPlanesVisible call -- without
    /// this, a plane discovered while the overlay is hidden (e.g. late in a scan, or after
    /// AllPlacementTapsComplete already fired) would default to visible and the blue
    /// overlay would unexpectedly pop back in for just that one plane.
    /// </summary>
    private void HandlePlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (planesVisible)
        {
            return;
        }

        foreach (var plane in args.added)
        {
            plane.gameObject.SetActive(false);
        }
    }
}
