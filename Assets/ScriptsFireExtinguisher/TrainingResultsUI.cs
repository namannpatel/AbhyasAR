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

    [Tooltip("The terse GREAT JOB modal that fronts this panel. When set, this panel stays " +
        "hidden (its content is still populated) until that modal is dismissed, so the two " +
        "never show at once -- see FireResultsModal.OnDismissed. Leave unset to fall back to " +
        "showing immediately, e.g. for a scene that doesn't use the modal.")]
    [SerializeField] private FireResultsModal resultsModal;

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
    private static readonly Color PassColor = new Color32(0x2E, 0x7D, 0x32, 0xFF);
    private static readonly Color FailColor = new Color32(0xC6, 0x28, 0x28, 0xFF);

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
        if (resultsModal != null)
        {
            resultsModal.OnDismissed += RevealPanel;
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
        if (resultsModal != null)
        {
            resultsModal.OnDismissed -= RevealPanel;
        }
        retryButton?.onClick.RemoveListener(HandleRetry);
        backToMenuButton?.onClick.RemoveListener(HandleBackToMenu);
        nextButton?.onClick.RemoveListener(HandleNext);
        getCertificateButton?.onClick.RemoveListener(HandleGetCertificate);
    }

    private void ShowResult(FireResponseResult result)
    {
        lastResult = result;

        // Content is populated immediately either way; only the reveal is deferred when a
        // fronting modal is wired up, so this panel never overlaps it (see resultsModal doc).
        if (resultsModal == null)
        {
            RevealPanel();
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
            overallResultText.color = result.passed ? PassColor : FailColor;
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
        string glyph = ok ? Pass : Fail;
        string hex = ok ? "#2E7D32" : "#C62828";
        label.text = $"<color={hex}>{glyph}</color> {text}";
    }

    /// <summary>
    /// Activates panelRoot. Called either immediately from ShowResult (no fronting modal
    /// wired up) or from resultsModal.OnDismissed once the player closes the GREAT JOB modal.
    /// </summary>
    private void RevealPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
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
