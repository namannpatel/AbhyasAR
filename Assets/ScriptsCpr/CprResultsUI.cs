using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Results panel for the CPR &amp; AED module — same shape as TrainingResultsUI: a
/// checklist line per step, overall PASS/FAIL + score, Retry/Back-to-menu, and a
/// QR-certificate button on a PASS result via the shared CertificateService/CertificateUI.
/// </summary>
public class CprResultsUI : MonoBehaviour
{
    [SerializeField] private CprResponseCoordinator coordinator;
    [SerializeField] private GameObject panelRoot;

    [SerializeField] private TMP_Text callLine;
    [SerializeField] private TMP_Text compressionsLine;
    [SerializeField] private TMP_Text breathsLine;
    [SerializeField] private TMP_Text aedPadsLine;
    [SerializeField] private TMP_Text shockLine;
    [SerializeField] private TMP_Text overallResultText;

    [SerializeField] private Button retryButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private MenuManager menuManager;

    [Tooltip("Opens the QR-based certificate panel. Only shown on a PASS result.")]
    [SerializeField] private Button getCertificateButton;
    [SerializeField] private CertificateUI certificateUI;

    private CprResult lastResult;

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
        if (coordinator == null)
        {
            coordinator = FindFirstObjectByType<CprResponseCoordinator>();
        }
        if (coordinator != null)
        {
            coordinator.OnModuleComplete += ShowResult;
        }
        retryButton?.onClick.AddListener(HandleRetry);
        backToMenuButton?.onClick.AddListener(HandleBackToMenu);
        getCertificateButton?.onClick.AddListener(HandleGetCertificate);
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

    private void ShowResult(CprResult result)
    {
        lastResult = result;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetLine(callLine, "Called for help", result.calledForHelp);
        SetLine(compressionsLine, $"Compressions at correct rate ({result.goodRateCompressionCount}/{result.targetCompressionCount})",
            result.targetCompressionCount > 0 && result.goodRateCompressionCount >= result.targetCompressionCount / 2);
        SetLine(breathsLine, "Rescue breaths given", result.breathsGiven);
        SetLine(aedPadsLine, $"AED pads placed correctly ({result.aedPadsCorrect}/{result.aedPadsRequired})",
            result.aedPadsRequired > 0 && result.aedPadsCorrect == result.aedPadsRequired && result.aedFalsePositives == 0);
        SetLine(shockLine, "Shock delivered", result.shockDelivered);

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
        certificateUI?.Show("CPR & AED Response", lastResult.score);
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

        var placementController = FindFirstObjectByType<CprPlacementController>();
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
