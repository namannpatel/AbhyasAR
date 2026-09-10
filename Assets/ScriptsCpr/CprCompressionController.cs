using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Chest-compression mechanic for the CPR module: repeated taps on the mannequin's chest
/// Collider, scored for rate against the AHA-recommended 100-120 compressions/minute — no
/// accelerometer or physical mannequin needed, matching every other tap-interaction in this
/// project (see ArTouchInput). Stays inactive until StartStage() is called by
/// CprResponseCoordinator, once the call-for-help stage resolves.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CprCompressionController : MonoBehaviour
{
    [Tooltip("Camera used to raycast from screen taps. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera raycastCamera;

    [Tooltip("Compressions required to complete this stage.")]
    [SerializeField] private int targetCompressionCount = 30;

    [Tooltip("Compressions/minute range counted as a good rate (AHA guideline: 100-120).")]
    [SerializeField] private float minGoodRate = 100f;
    [SerializeField] private float maxGoodRate = 120f;

    [Tooltip("Raised once, when targetCompressionCount is reached.")]
    public UnityEvent OnCycleComplete;

    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }
    public int CompressionCount { get; private set; }
    public int GoodRateCount { get; private set; }
    public int TargetCompressionCount => targetCompressionCount;

    /// <summary>Instantaneous rate (compressions/min) of the most recent tap — 0 before the second tap. Polled by CprInstructionsUI for live feedback.</summary>
    public float LastRate { get; private set; }

    private Collider chestCollider;
    private float lastTapTime = -1f;

    private void Awake()
    {
        chestCollider = GetComponent<Collider>();
        if (chestCollider == null)
        {
            Debug.LogWarning($"{name}: CprCompressionController has no Collider, it can never be tapped.", this);
        }
    }

    /// <summary>Called by CprResponseCoordinator once the call-for-help stage is activated.</summary>
    public void StartStage()
    {
        IsActive = true;
        IsComplete = false;
        CompressionCount = 0;
        GoodRateCount = 0;
        LastRate = 0f;
        lastTapTime = -1f;
    }

    private void Update()
    {
        if (!IsActive || IsComplete || chestCollider == null)
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
        if (chestCollider.Raycast(ray, out _, float.PositiveInfinity))
        {
            RegisterCompression();
        }
    }

    private void RegisterCompression()
    {
        float now = Time.time;
        if (lastTapTime >= 0f)
        {
            float interval = now - lastTapTime;
            LastRate = interval > 0.01f ? 60f / interval : 0f;
        }
        lastTapTime = now;

        CompressionCount++;
        if (LastRate >= minGoodRate && LastRate <= maxGoodRate)
        {
            GoodRateCount++;
        }

        if (CompressionCount >= targetCompressionCount)
        {
            IsComplete = true;
            IsActive = false;
            OnCycleComplete?.Invoke();
        }
    }
}
