using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Terse "GREAT JOB!" modal shown the instant a Fire Safety attempt completes: a short
/// header plus a full ✓/✗ checklist of every scored step (see BuildChecklistText) -- this
/// is the first thing the player sees on completion, so it needs to show what was done
/// correctly, not just what was missed. Styled like FireAlertIntro's opening AlertModal
/// (same card shape, dismiss button). Sits on top of — and dismisses to reveal — the
/// existing TrainingResultsUI full checklist panel underneath, which already becomes
/// active on the same event; this doesn't replace it, it fronts it with a terser
/// presentation while keeping Retry/Certificate/Back-to-menu working unchanged.
/// </summary>
public class FireResultsModal : MonoBehaviour
{
    [SerializeField] private FireResponseCoordinator coordinator;
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text missedStepsText;
    [SerializeField] private Button continueButton;

    private NarrationPlayer narrationPlayer;

    private void Awake()
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (coordinator != null)
        {
            coordinator.OnModuleComplete += ShowResult;
        }
        continueButton?.onClick.AddListener(Dismiss);
        if (narrationPlayer == null)
        {
            narrationPlayer = FindFirstObjectByType<NarrationPlayer>();
        }
    }

    private void OnDisable()
    {
        if (coordinator != null)
        {
            coordinator.OnModuleComplete -= ShowResult;
        }
        continueButton?.onClick.RemoveListener(Dismiss);
    }

    /// <summary>
    /// Wires the FireResponseCoordinator this modal listens to. Called at runtime by
    /// ARPlacementController the moment the player commits to an extinguisher, mirroring
    /// TrainingResultsUI.SetCoordinator -- this HUD lives on the always-present ARRig, so
    /// it can't be wired to the coordinator at design time.
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

    private void ShowResult(FireResponseResult result)
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(true);
        }

        if (headerText != null)
        {
            headerText.text = result.forcedFailure ? LocalizationManager.Get("header_furnace_exploded")
                : result.passed ? LocalizationManager.Get("header_great_job")
                : LocalizationManager.Get("header_not_quite");
        }

        if (missedStepsText != null)
        {
            missedStepsText.text = BuildChecklistText(result);
        }

        narrationPlayer?.Play(result.forcedFailure ? "result_furnace_exploded"
            : result.passed ? "result_great_job"
            : "result_not_quite");
    }

    /// <summary>
    /// Every scored step of the attempt, each with a ✓ (done correctly) or ✗ (missed/wrong)
    /// marker — this modal is the first thing the player sees on completion, so it needs to
    /// show what they got RIGHT as well as what they missed, not just a bare list of
    /// failures (a player who did everything correctly used to just get a single "great
    /// job" line with no visible confirmation of which steps that covered).
    /// </summary>
    private static string BuildChecklistText(FireResponseResult result)
    {
        var sb = new StringBuilder();

        if (result.forcedFailure)
        {
            // Not tracked as its own pass/fail flag on FireResponseResult (only whether the
            // attempt got force-failed for missing it) -- so, unlike every other line below,
            // this one can only ever appear as a miss, never a confirmed checkmark.
            sb.AppendLine("✗ " + LocalizationManager.Get("check_gas_shutoff"));
        }

        AppendCheck(sb, result.alarmActivated, LocalizationManager.Get("check_alarm"));
        AppendCheck(sb, !result.wrongExtinguisherUsed, LocalizationManager.Get("check_extinguisher_class"));
        AppendCheck(sb, result.pinPulled, LocalizationManager.Get("pass_pull_pin"));
        AppendCheck(sb, result.aimedAtBase, LocalizationManager.Get("pass_aim_base"));
        AppendCheck(sb, result.squeezed, LocalizationManager.Get("check_squeeze_spray"));
        AppendCheck(sb, result.swept, LocalizationManager.Get("check_sweep"));
        AppendCheck(sb, result.fireFullyOut, LocalizationManager.Get("check_fire_out"));

        return sb.ToString().TrimEnd();
    }

    private static void AppendCheck(StringBuilder sb, bool ok, string label)
    {
        sb.AppendLine((ok ? "✓ " : "✗ ") + label);
    }

    private void Dismiss()
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
    }
}
