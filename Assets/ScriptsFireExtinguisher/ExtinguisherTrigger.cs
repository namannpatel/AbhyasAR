using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Step 3 of the PASS technique: squeezing the lever. Attach directly to the
/// extinguisher's handle/lever 3D object (with a Collider) — tap-and-hold it on
/// screen, the same plain touch-raycast approach as ExtinguisherPin, so it works
/// identically once the extinguisher is held in front of the camera. No UI button
/// or interaction package required.
/// </summary>
public class ExtinguisherTrigger : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    [SerializeField] private ExtinguisherPin pin;
    [SerializeField] private ParticleSystem spray;
    [SerializeField] private ExtinguisherAimController aimController;

    [Tooltip("Optional — if set, the lever can only be squeezed once the extinguisher has been picked up.")]
    [SerializeField] private ExtinguisherPickup pickup;

    [Tooltip("Raised when the lever is squeezed (pin must already be pulled).")]
    public UnityEvent OnSqueezeStart;

    [Tooltip("Raised when the lever is released.")]
    public UnityEvent OnSqueezeEnd;

    public bool IsSqueezed { get; private set; }

    private Collider handleCollider;
    private bool heldLastFrame;

    private void Awake()
    {
        handleCollider = GetComponent<Collider>();
        if (handleCollider == null)
        {
            Debug.LogWarning($"{name}: ExtinguisherTrigger has no Collider, it can never be squeezed.", this);
        }
    }

    private void Update()
    {
        if (pickup != null && !pickup.IsHeld)
        {
            return;
        }

        // Once squeezed, keep spraying until the finger is lifted -- drifting off the small lever
        // collider (or over a HUD button) must not cut the stream.
        bool held = heldLastFrame ? ArTouchInput.IsPointerDown() : IsPointerHeldOnHandle();

        if (held && !heldLastFrame)
        {
            TrySqueezeStart();
        }
        else if (!held && heldLastFrame)
        {
            SqueezeEnd();
        }

        heldLastFrame = held;
    }

    private void LateUpdate()
    {
        if (spray == null)
        {
            return;
        }

        // The nozzle is aimed along the camera's forward, so seen from the phone the stream would
        // fly straight away from the viewer and just swell into a blob at the tip. Angle the
        // visible stream from the nozzle toward the point the player is aiming at, so it reads as
        // leaving the nozzle. Only this child emitter turns; the nozzle pivot (aim/sweep checks) is untouched.
        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (IsSqueezed && cam != null)
        {
            Vector3 focus = cam.transform.position + cam.transform.forward * SprayConvergeDistance;
            Vector3 toFocus = focus - spray.transform.position;
            if (toFocus.sqrMagnitude > 0.0001f)
            {
                spray.transform.rotation = Quaternion.LookRotation(toFocus.normalized, cam.transform.up);
            }
        }
        else
        {
            spray.transform.localRotation = Quaternion.identity;
        }
    }

    private const float SprayConvergeDistance = 1.6f;

    private void OnDisable()
    {
        // Make sure a held spray doesn't keep emitting if this gets disabled mid-squeeze.
        if (IsSqueezed)
        {
            SqueezeEnd();
        }
    }

    private bool IsPointerHeldOnHandle()
    {
        if (handleCollider == null)
        {
            return false;
        }

        Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        if (!ArTouchInput.TryGetHeldPosition(out Vector2 screenPos))
        {
            return false;
        }

        Ray ray = cam.ScreenPointToRay(screenPos);
        return handleCollider.Raycast(ray, out _, float.PositiveInfinity);
    }

    private void TrySqueezeStart()
    {
        if (pin != null && !pin.IsPulled)
        {
            // Real PASS-technique order: can't spray before the pin is pulled.
            return;
        }

        if (IsSqueezed)
        {
            return;
        }

        IsSqueezed = true;
        spray?.Play();
        if (aimController != null)
        {
            aimController.IsSpraying = true;
        }
        OnSqueezeStart?.Invoke();
    }

    private void SqueezeEnd()
    {
        if (!IsSqueezed)
        {
            return;
        }

        IsSqueezed = false;
        spray?.Stop();
        if (aimController != null)
        {
            aimController.IsSpraying = false;
        }
        OnSqueezeEnd?.Invoke();
    }
}
