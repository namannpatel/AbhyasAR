using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tap-to-pick-up for a wall-mounted extinguisher (see ARPlacementController, which
/// mounts the extinguisher prefab on a tapped wall). Before pickup the extinguisher
/// sits still where it was mounted; tapping its body (this object's Collider)
/// re-parents it to the AR camera at a fixed held-position offset, like a
/// first-person held prop — pointing the phone then naturally aims the nozzle,
/// no separate aim input needed (see ExtinguisherAimController).
/// </summary>
public class ExtinguisherPickup : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps, and that the extinguisher is held relative to. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    [Tooltip("Local position (relative to the camera) the extinguisher snaps to once held. Keep the handle (and the rest of the model) within the camera's FOV cone on a real device's narrower aspect ratio, not just in the Editor's Game View -- Unity's FOV is vertical, so a taller/narrower phone screen has a noticeably tighter horizontal FOV than a wider test window, and a held-prop offset that looks fine in the Editor can clip on-device. See the -- now corrected -- default here: the previous (0.12, -0.32, 0.55) put the handle's edge ~28 degrees off camera-forward against a real device's ~19-degree horizontal half-FOV budget, clipping it off-screen.")]
    [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.05f, -0.22f, 0.85f);

    [Tooltip("Local rotation (euler, relative to the camera) the extinguisher snaps to once held.")]
    [SerializeField] private Vector3 heldLocalEulerAngles = new Vector3(0f, 0f, 0f);

    [Tooltip("The nozzle transform (see ExtinguisherAimController) — its rest-pose direction rarely points straight out of the model, so pickup auto-corrects rotation to make it point exactly where the camera looks once held. Optional.")]
    [SerializeField] private Transform nozzleReference;

    [Tooltip("Raised once, the moment the extinguisher is picked up.")]
    public UnityEvent OnPickedUp;

    [Tooltip("Sound played the instant the extinguisher is picked up.")]
    [SerializeField] private AudioClip pickupClip;

    public bool IsHeld { get; private set; }

    /// <summary>
    /// The anchor this extinguisher was originally mounted under (see ARPlacementController.
    /// CreateAnchor) — stays correct even once held, unlike transform.parent, which PickUp
    /// changes to the AR camera. ARPlacementController's Retry/Next-scenario cleanup needs
    /// this: destroying transform.parent.gameObject after this extinguisher had been picked
    /// up used to destroy the AR camera itself (it briefly IS this transform's parent while
    /// held), taking raycasting/rendering/UI down with it.
    /// </summary>
    public Transform HomeAnchor => originalParent;

    private Collider bodyCollider;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private void Awake()
    {
        bodyCollider = GetComponent<Collider>();
        if (bodyCollider == null)
        {
            Debug.LogWarning($"{name}: ExtinguisherPickup has no Collider, it can never be picked up.", this);
        }

        // Best-effort fallback only -- ARPlacementController calls CaptureRestPose() right
        // after it finishes positioning a freshly-placed extinguisher, which is what
        // actually matters (see CaptureRestPose's own comment for why Awake's snapshot
        // can't be trusted for this).
        CaptureRestPose();
    }

    /// <summary>
    /// Snapshots the current parent/local pose as "home" -- where UndoPickup puts this
    /// extinguisher back when it's rejected as the wrong choice for the fire. Must be
    /// called once placement is actually finished, not from Awake(): ARPlacementController
    /// instantiates each option via Instantiate(prefab, worldPos, worldRot, anchor), and
    /// with a parent argument those position/rotation values are WORLD space, not local --
    /// so at the moment Awake() runs (mid-Instantiate, before the caller's next lines get a
    /// chance to run), transform.localPosition is whatever local offset happens to map to
    /// world (0,0,0) under the anchor, not the anchor's own position. Caching that in Awake
    /// used to send a rejected extinguisher flying off toward world origin (visibly "down
    /// to the floor") instead of back to where it was actually mounted on the wall.
    /// </summary>
    public void CaptureRestPose()
    {
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        if (IsHeld || bodyCollider == null)
        {
            return;
        }

        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (ArTouchInput.TryGetTapPosition(out Vector2 screenPos))
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (bodyCollider.Raycast(ray, out _, float.PositiveInfinity))
            {
                PickUp(cam);
            }
        }
    }

    private void PickUp(Camera cam)
    {
        IsHeld = true;
        if (pickupClip != null)
        {
            AudioSource.PlayClipAtPoint(pickupClip, transform.position);
        }
        transform.SetParent(cam.transform, worldPositionStays: false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);

        if (nozzleReference != null)
        {
            // The nozzle's rest-pose forward direction comes from however the source
            // model happened to be authored, not necessarily "straight out of the
            // extinguisher" — correct for that here so pointing the phone at
            // something always aims the nozzle at it, regardless of model quirks.
            Quaternion correction = Quaternion.FromToRotation(nozzleReference.forward, cam.transform.forward);
            transform.rotation = correction * transform.rotation;
        }

        // This project has Physics.autoSyncTransforms disabled (Project Settings >
        // Physics), so colliders don't automatically pick up transform changes made
        // from script. Without this, every collider on the extinguisher (pin,
        // handle, body) stays physically stuck at its old wall-mounted position —
        // raycasts against them (tap-to-pull-pin, tap-to-squeeze) would keep testing
        // the wrong spot even though the model visibly moved to the held pose.
        Physics.SyncTransforms();

        OnPickedUp?.Invoke();
    }

    /// <summary>
    /// Puts the extinguisher back where it was mounted and lets it be picked up again —
    /// called by ARPlacementController when the player grabs an extinguisher that isn't
    /// rated for the current fire, instead of letting them commit to a losing choice.
    /// </summary>
    public void UndoPickup()
    {
        IsHeld = false;
        transform.SetParent(originalParent, worldPositionStays: false);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        Physics.SyncTransforms();
    }
}
