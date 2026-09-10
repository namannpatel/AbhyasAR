using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal results panel: shown once PassChecklistTracker reports an attempt complete.
/// Displays a checklist (one line per PASS step), the overall PASS/FAIL and score for
/// this scenario, and Retry/Back-to-menu buttons. On a campaign with more than one
/// scenario left to train, a NEXT button also appears — clicking it moves straight on to
/// the next scenario (see ARPlacementController.AdvanceToNextScenario) instead of ending
/// the session; NEXT only disappears once every scenario has been trained, at which point
/// Retry/Back/Certificate are the session's final actions.
/// </summary>
public class TrainingResultsUI : MonoBehaviour
{
    [SerializeField] private FireResponseCoordinator coordinator;
    [SerializeField] private GameObject panelRoot;

    [SerializeField] private TMP_Text alarmLine;
    [SerializeField] private TMP_Text pullLine;
    [SerializeField] private TMP_Text aimLine;
    [SerializeField] private TMP_Text squeezeLine;
    [SerializeField] private TMP_Text sweepLine;
    [SerializeField] private TMP_Text extinguisherLine;
    [SerializeField] private TMP_Text overallResultText;

    [SerializeField] private Button retryButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private MenuManager menuManager;

    [Tooltip("Moves on to the next scenario in the campaign. Shown only while a scenario is still left to train.")]
    [SerializeField] private Button nextButton;

    [Tooltip("Opens the QR-based certificate panel. Only shown on a PASS result.")]
    [SerializeField] private Button getCertificateButton;
    [SerializeField] private CertificateUI certificateUI;

    private FireResponseResult lastResult;

    private const string Pass = "✓";
    private const string Fail = "✗";

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (coordinator != null)
        {
            coordinator.OnModuleComplete += ShowResult;
        }
        retryButton?.onClick.AddListener(HandleRetry);
        backToMenuButton?.onClick.AddListener(HandleBackToMenu);
        nextButton?.onClick.AddListener(HandleNext);
        getCertificateButton?.onClick.AddListener(HandleGetCertificate);
    }

    /// <summary>
    /// Wires the FireResponseCoordinator this results panel listens to. Called at
    /// runtime by ARPlacementController once the extinguisher content has been placed —
    /// this HUD lives on the always-present ARRig, so it can't be wired to the
    /// coordinator at design time.
    /// </summary>
    public void SetCoordinator(FireResponseCoordinator newCoordinator)
    {
        if (coordinator != null)
        {
            coordinator.OnModuleComplete -= ShowResult;
        }

        coordinator = newCoordinator;

        if (coordinator != null)
        {
            coordinator.OnModuleComplete += ShowResult;
        }
    }

    private void OnDisable()
    {
        if (coordinator != null)
        {
            coordinator.OnModuleComplete -= ShowResult;
        }
        retryButton?.onClick.RemoveListener(HandleRetry);
        backToMenuButton?.onClick.RemoveListener(HandleBackToMenu);
        nextButton?.onClick.RemoveListener(HandleNext);
        getCertificateButton?.onClick.RemoveListener(HandleGetCertificate);
    }

    private void ShowResult(FireResponseResult result)
    {
        lastResult = result;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetLine(alarmLine, LocalizationManager.Get("line_alarm"), result.alarmActivated);
        SetLine(pullLine, LocalizationManager.Get("pass_pull_pin"), result.pinPulled);
        SetLine(aimLine, LocalizationManager.Get("line_aim_short"), result.aimedAtBase);
        SetLine(squeezeLine, LocalizationManager.Get("line_squeeze"), result.squeezed);
        SetLine(sweepLine, LocalizationManager.Get("line_sweep_short"), result.swept);
        SetLine(extinguisherLine,
            result.forcedFailure ? LocalizationManager.Get("line_extinguisher_procedure") : LocalizationManager.Get("line_extinguisher_correct"),
            result.forcedFailure ? false : !result.wrongExtinguisherUsed);

        var placementController = FindFirstObjectByType<ARPlacementController>();
        string scenarioTag = placementController != null
            ? LocalizationManager.Get("scenario_tag_format", placementController.ScenarioNumber, placementController.TotalScenarios)
            : string.Empty;

        if (overallResultText != null)
        {
            overallResultText.text = result.passed
                ? LocalizationManager.Get("overall_pass_format", result.elapsedSeconds.ToString("0.0"), result.score, scenarioTag)
                : LocalizationManager.Get("overall_fail_format", result.score, scenarioTag);
        }

        bool hasNext = placementController != null && placementController.HasNextScenario;
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(hasNext);
        }

        if (getCertificateButton != null)
        {
            getCertificateButton.gameObject.SetActive(result.passed);
        }
    }

    private void HandleGetCertificate()
    {
        certificateUI?.Show(LocalizationManager.Get("module_fire_safety_training"), lastResult.score);
    }

    private static void SetLine(TMP_Text label, string text, bool ok)
    {
        if (label == null)
        {
            return;
        }
        label.text = $"{(ok ? Pass : Fail)} {text}";
    }

    private void HandleRetry()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // Reset in place rather than reloading the scene — reloading destroys and
        // recreates the AR Session/XR Origin, which made the wall/floor planes the
        // player already scanned stop being detected after a retry. Resetting keeps
        // the AR session (and its already-tracked planes) alive.
        var placementController = FindFirstObjectByType<ARPlacementController>();
        if (placementController != null)
        {
            placementController.ResetTraining();
        }
    }

    private void HandleNext()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        var placementController = FindFirstObjectByType<ARPlacementController>();
        placementController?.AdvanceToNextScenario();
    }

    private void HandleBackToMenu()
    {
        menuManager?.LoadMainMenu();
    }
}
