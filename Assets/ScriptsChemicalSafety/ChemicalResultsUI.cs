using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Results panel for the Chemical Hazard Inspection module: shown once
/// ChemicalSafetyCoordinator reports an attempt complete. Same shape as
/// TrainingResultsUI (module 1) — checklist lines + overall PASS/FAIL + Retry/Back —
/// kept as an independent script rather than a shared base class, matching this
/// project's existing per-module UI scripts.
/// </summary>
public class ChemicalResultsUI : MonoBehaviour
{
    [SerializeField] private ChemicalSafetyCoordinator coordinator;
    [SerializeField] private GameObject panelRoot;

    [SerializeField] private TMP_Text ppeLine;
    [SerializeField] private TMP_Text hazardsLine;
    [SerializeField] private TMP_Text falsePositivesLine;
    [SerializeField] private TMP_Text overallResultText;

    [SerializeField] private Button retryButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private MenuManager menuManager;

    [Tooltip("Opens the QR-based certificate panel. Only shown on a PASS result.")]
    [SerializeField] private Button getCertificateButton;
    [SerializeField] private CertificateUI certificateUI;

    private ChemicalSafetyResult lastResult;

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
        getCertificateButton?.onClick.AddListener(HandleGetCertificate);
    }

    /// <summary>
    /// Wires the ChemicalSafetyCoordinator this results panel listens to. Called at
    /// runtime by ChemicalPlacementController once the scenario has been placed.
    /// </summary>
    public void SetCoordinator(ChemicalSafetyCoordinator newCoordinator)
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
        getCertificateButton?.onClick.RemoveListener(HandleGetCertificate);
    }

    private void ShowResult(ChemicalSafetyResult result)
    {
        lastResult = result;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetLine(ppeLine, "Correct PPE worn", result.ppeCorrect);
        SetLine(hazardsLine, $"Hazards found ({result.hazardsFound}/{result.hazardsRequired})", result.hazardsFound == result.hazardsRequired);
        SetLine(falsePositivesLine, $"No false positives ({result.falsePositives})", result.falsePositives == 0);

        if (overallResultText != null)
        {
            overallResultText.text = result.passed
                ? $"PASS ({result.elapsedSeconds:0.0}s) — Score: {result.score}/100"
                : $"FAIL — Score: {result.score}/100";
        }

        if (getCertificateButton != null)
        {
            getCertificateButton.gameObject.SetActive(result.passed);
        }
    }

    private void HandleGetCertificate()
    {
        certificateUI?.Show("Chemical Hazard Inspection", lastResult.score);
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

        coordinator?.ResetForRetry();

        var placementController = FindFirstObjectByType<ChemicalPlacementController>();
        if (placementController != null)
        {
            placementController.ResetTraining();
        }
    }

    private void HandleBackToMenu()
    {
        menuManager?.LoadMainMenu();
    }
}
