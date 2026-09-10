using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// AED device interaction: tap the case to open it, then — once CprResponseCoordinator
/// confirms both pads are correctly placed on the mannequin (via the reused
/// FireHazardInspectionController) and calls EnableShockButton — tap the shock button to
/// deliver the shock. Same plain touch-raycast approach as every other interaction in this
/// project. Attach to the root of the AED prefab.
/// </summary>
public class CprAedController : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    [Tooltip("Collider on the AED case — tapping it opens the device.")]
    [SerializeField] private Collider caseCollider;

    [Tooltip("Collider on the shock button — only tappable once EnableShockButton has been called.")]
    [SerializeField] private Collider shockButtonCollider;

    [Tooltip("Raised once, the moment the case is opened.")]
    public UnityEvent OnCaseOpened;

    [Tooltip("Raised once, the moment the shock is delivered.")]
    public UnityEvent OnShockDelivered;

    public bool IsOpen { get; private set; }
    public bool ShockButtonEnabled { get; private set; }
    public bool ShockDelivered { get; private set; }

    /// <summary>Called by CprResponseCoordinator once both AED pads are correctly placed.</summary>
    public void EnableShockButton()
    {
        ShockButtonEnabled = true;
    }

    private void Update()
    {
        if (ShockDelivered)
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

        if (!IsOpen)
        {
            if (caseCollider != null && caseCollider.Raycast(ray, out _, float.PositiveInfinity))
            {
                IsOpen = true;
                OnCaseOpened?.Invoke();
            }
            return;
        }

        if (ShockButtonEnabled && shockButtonCollider != null && shockButtonCollider.Raycast(ray, out _, float.PositiveInfinity))
        {
            ShockDelivered = true;
            OnShockDelivered?.Invoke();
        }
    }
}
