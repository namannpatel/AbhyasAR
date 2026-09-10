using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Rescue-breath mechanic (the 30:2 ratio's "2"): two taps on the mannequin's head/mouth
/// Collider, the same plain touch-raycast approach as CprCompressionController. Stays
/// inactive until StartStage() is called by CprResponseCoordinator, once compressions
/// complete.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CprBreathController : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    [Tooltip("Rescue breaths required to complete this stage.")]
    [SerializeField] private int targetBreathCount = 2;

    [Tooltip("Raised once, when targetBreathCount is reached.")]
    public UnityEvent OnBreathsComplete;

    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }
    public int BreathCount { get; private set; }
    public int TargetBreathCount => targetBreathCount;

    private Collider headCollider;

    private void Awake()
    {
        headCollider = GetComponent<Collider>();
        if (headCollider == null)
        {
            Debug.LogWarning($"{name}: CprBreathController has no Collider, it can never be tapped.", this);
        }
    }

    /// <summary>Called by CprResponseCoordinator once compressions complete.</summary>
    public void StartStage()
    {
        IsActive = true;
        IsComplete = false;
        BreathCount = 0;
    }

    private void Update()
    {
        if (!IsActive || IsComplete || headCollider == null)
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
        if (!headCollider.Raycast(ray, out _, float.PositiveInfinity))
        {
            return;
        }

        BreathCount++;
        if (BreathCount >= targetBreathCount)
        {
            IsComplete = true;
            IsActive = false;
            OnBreathsComplete?.Invoke();
        }
    }
}
