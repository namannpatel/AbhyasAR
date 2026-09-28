using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End of the Machine Training (conveyor) module, mirroring the Fire Training flow: once
/// ConveyorTrainingTracker reports every control practised, the belt is stopped, the practice
/// is recorded, and the machine-safety quiz runs. When the quiz closes, this results panel
/// shows the practice checklist and the training outcome.
///
/// Passing the quiz is mandatory: the training only counts as passed when the practice is done
/// AND the quiz was passed -- and only then does the certificate open (automatically; there is
/// no separate certificate button). Closing or failing the quiz shows NOT PASSED with no
/// certificate; RETRY restarts the practice, after which the quiz runs again. A quiz already
/// passed this session isn't asked again.
/// </summary>
public class MachineTrainingResultsUI : MonoBehaviour
{
    [SerializeField] private ConveyorTrainingTracker tracker;
    [SerializeField] private ConveyorPlacementController placementController;

    [Tooltip("Machine-safety quiz run once the practice is complete. Leave unset to skip the quiz.")]
    [SerializeField] private TrainingQuizUI quiz;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text checklistText;
    [SerializeField] private TMP_Text overallResultText;
    [SerializeField] private TMP_Text quizStatusText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private MenuManager menuManager;

    [Tooltip("QR certificate panel, opened automatically once the training (practice + quiz) is passed.")]
    [SerializeField] private CertificateUI certificateUI;

    private const string ProgressModule = "machine_training";
    private static readonly Color PassColor = new Color32(0x2E, 0x7D, 0x32, 0xFF);
    private static readonly Color FailColor = new Color32(0xC6, 0x28, 0x28, 0xFF);

    [Serializable]
    private class PracticeDetails
    {
        public int tasksCompleted;
    }

    private bool TrainingPassed => tracker != null && tracker.IsComplete && (quiz == null || quiz.HasPassed);

    /// <summary>Certificate score: the best quiz result as a percentage (the practice itself isn't scored).</summary>
    private int CertificateScore => quiz != null && quiz.QuestionCount > 0
        ? Mathf.RoundToInt(quiz.BestScore * 100f / quiz.QuestionCount)
        : 100;

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (tracker != null)
        {
            tracker.OnCompleted += HandlePracticeComplete;
        }
        retryButton?.onClick.AddListener(HandleRetry);
        backToMenuButton?.onClick.AddListener(HandleBackToMenu);
        LocalizationManager.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (tracker != null)
        {
            tracker.OnCompleted -= HandlePracticeComplete;
        }
        if (quiz != null)
        {
            quiz.OnClosed -= HandleQuizClosed;
        }
        retryButton?.onClick.RemoveListener(HandleRetry);
        backToMenuButton?.onClick.RemoveListener(HandleBackToMenu);
        LocalizationManager.OnLanguageChanged -= Refresh;
    }

    private void HandlePracticeComplete()
    {
        // Don't leave the belt running (and humming) behind the quiz.
        placementController?.MotionController?.Stop();

        ProgressStore.Record(ProgressModule, gameObject.scene.name + "/Practice", true, 100,
            tracker.CompletedElapsedSeconds, JsonUtility.ToJson(new PracticeDetails { tasksCompleted = tracker.TaskCount }));

        if (quiz != null && !quiz.HasPassed)
        {
            quiz.OnClosed -= HandleQuizClosed;
            quiz.OnClosed += HandleQuizClosed;
            quiz.Show();
            return;
        }

        ShowPanel();
    }

    private void HandleQuizClosed()
    {
        quiz.OnClosed -= HandleQuizClosed;
        ShowPanel();
    }

    private void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        Refresh();

        FindAnyObjectByType<NarrationPlayer>()?.Play(TrainingPassed ? "machine_result_pass" : "machine_result_fail");

        if (TrainingPassed)
        {
            certificateUI?.ShowAndGenerate(LocalizationManager.Get("module_machine_safety_training"), CertificateScore);
        }
    }

    private void Refresh()
    {
        if (panelRoot == null || !panelRoot.activeSelf || tracker == null)
        {
            return;
        }

        if (checklistText != null)
        {
            var sb = new StringBuilder();
            foreach (ConveyorTrainingTracker.Task task in Enum.GetValues(typeof(ConveyorTrainingTracker.Task)))
            {
                bool ok = tracker.IsDone(task);
                sb.AppendLine((ok ? "<color=#2E7D32>✓</color> " : "<color=#C62828>✗</color> ") + ConveyorTrainingTracker.DoneLabel(task));
            }
            checklistText.text = sb.ToString().TrimEnd();
        }

        bool passed = TrainingPassed;
        if (overallResultText != null)
        {
            overallResultText.text = LocalizationManager.Get(passed ? "machine_result_pass" : "machine_result_fail");
            overallResultText.color = passed ? PassColor : FailColor;
        }

        if (quizStatusText != null)
        {
            quizStatusText.gameObject.SetActive(quiz != null);
            if (quiz != null)
            {
                quizStatusText.text = quiz.HasPassed
                    ? "<color=#2E7D32>✓</color> " + LocalizationManager.Get("machine_quiz_status_passed_format", quiz.BestScore, quiz.QuestionCount)
                    : "<color=#C62828>✗</color> " + LocalizationManager.Get("machine_quiz_status_not_passed");
            }
        }
    }

    private void HandleRetry()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        // Restart the practice from scratch: clear progress and let the trainee place the
        // conveyor again (same in-place reset as the toolbar's Reposition -- no scene reload).
        tracker?.ResetProgress();
        placementController?.ResetTraining();
    }

    private void HandleBackToMenu()
    {
        menuManager?.LoadMainMenu();
    }
}
