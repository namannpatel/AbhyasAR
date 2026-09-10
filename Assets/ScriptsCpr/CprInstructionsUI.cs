using TMPro;
using UnityEngine;

/// <summary>
/// Single-line "what to do next" prompt for the CPR &amp; AED module, same two-layer
/// pattern as TrainingInstructionsUI: CprPlacementController.CurrentPlacementPrompt()
/// covers "tap here to place the next thing"; once that's null (already placed, or not
/// ready yet), this falls back to fine-grained in-stage messages by polling the placed
/// content's own components each frame (they only exist once placed).
/// </summary>
public class CprInstructionsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("wikiHow-style instructional caption panel shown once the hands-on technique steps begin (call for help onward). Leave empty to skip — only the single-line label above is required.")]
    [SerializeField] private GameObject captionPanel;
    [SerializeField] private TMP_Text captionText;

    [Tooltip("Shared AR toolbar chrome (see ArHudToolbar) — optional, wires this module's timer and reposition action into it if present.")]
    [SerializeField] private ArHudToolbar toolbar;

    private CprPlacementController placementController;
    private CprResponseCoordinator coordinator;

    private void OnEnable()
    {
        placementController = FindFirstObjectByType<CprPlacementController>();
        coordinator = FindFirstObjectByType<CprResponseCoordinator>();

        if (toolbar != null)
        {
            toolbar.ElapsedSecondsProvider = () => coordinator != null ? coordinator.ElapsedSeconds : 0f;
            toolbar.OnReposition = () => placementController?.ResetTraining();
        }
    }

    private void Update()
    {
        if (label != null)
        {
            label.text = GetCurrentInstruction();
        }

        if (captionText != null)
        {
            string caption = GetCaption();
            if (captionPanel != null)
            {
                captionPanel.SetActive(caption != null);
            }
            if (caption != null)
            {
                captionText.text = caption;
            }
        }
    }

    /// <summary>
    /// Longer wikiHow-style instructional caption for the current technique step (own
    /// wording — not the illustrated wikiHow panels themselves, which are copyrighted).
    /// Null before the hands-on steps begin (placement/lesson stages already have their
    /// own prompt via the single-line label) or once the module is complete.
    /// </summary>
    private string GetCaption()
    {
        if (placementController == null || coordinator == null || placementController.CurrentPlacementPrompt() != null)
        {
            return null;
        }

        if (!coordinator.CallForHelpDone)
        {
            return "Call for emergency help before starting compressions.";
        }

        if (!coordinator.CompressionsDone)
        {
            return "Give 30 chest compressions at least 2 inches deep, at a rate of 100 to 120 per minute.";
        }

        if (!coordinator.ReadyForAed)
        {
            return "Give 2 rescue breaths after every 30 compressions.";
        }

        if (placementController.PlacedAed == null)
        {
            return "Bring the AED to the patient.";
        }

        var aedController = placementController.PlacedAed.GetComponentInChildren<CprAedController>(true);
        if (aedController != null && !aedController.IsOpen)
        {
            return "Turn on the AED and follow the voice prompts.";
        }

        var aedPads = placementController.PlacedMannequin != null
            ? placementController.PlacedMannequin.GetComponentInChildren<FireHazardInspectionController>(true)
            : null;
        if (aedPads != null && !aedPads.IsComplete)
        {
            return "Attach the pads to the bare chest as shown by the red patches.";
        }

        if (aedController != null && !aedController.ShockDelivered)
        {
            return "Make sure no one is touching the patient, then press the shock button.";
        }

        return null;
    }

    private string GetCurrentInstruction()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<CprPlacementController>();
            if (placementController == null)
            {
                return string.Empty;
            }
        }

        string placementPrompt = placementController.CurrentPlacementPrompt();
        if (placementPrompt != null)
        {
            return $"Step {placementController.CurrentStageNumber}/{placementController.TotalStages} — {placementPrompt}";
        }

        if (placementController.LessonController != null && !placementController.LessonController.IsComplete)
        {
            var current = placementController.LessonController.CurrentExhibit;
            string title = current != null ? current.Title : "…";
            return $"Learning: {title} ({placementController.LessonController.ViewedCount}/{placementController.LessonController.ExhibitCount})";
        }

        if (coordinator == null)
        {
            return string.Empty;
        }

        if (!coordinator.CallForHelpDone)
        {
            return "Tap the phone to call for emergency help";
        }

        if (!coordinator.CompressionsDone)
        {
            var compressions = placementController.PlacedMannequin != null
                ? placementController.PlacedMannequin.GetComponentInChildren<CprCompressionController>(true)
                : null;
            if (compressions != null)
            {
                string rateNote = compressions.LastRate <= 0f ? "" : compressions.LastRate < 100f ? " — push faster" : compressions.LastRate > 120f ? " — slow down" : " — good rate";
                return $"Compressions: {compressions.CompressionCount}/{compressions.TargetCompressionCount}{rateNote}";
            }
            return "Begin chest compressions";
        }

        if (!coordinator.ReadyForAed)
        {
            var breaths = placementController.PlacedMannequin != null
                ? placementController.PlacedMannequin.GetComponentInChildren<CprBreathController>(true)
                : null;
            if (breaths != null)
            {
                return $"Give rescue breaths: {breaths.BreathCount}/{breaths.TargetBreathCount}";
            }
            return "Give rescue breaths";
        }

        if (placementController.PlacedAed == null)
        {
            return null; // covered by CurrentPlacementPrompt's "Tap the floor to bring in the AED"
        }

        var aedController = placementController.PlacedAed.GetComponentInChildren<CprAedController>(true);
        if (aedController != null && !aedController.IsOpen)
        {
            return "Tap the AED case to open it";
        }

        var aedPads = placementController.PlacedMannequin != null
            ? placementController.PlacedMannequin.GetComponentInChildren<FireHazardInspectionController>(true)
            : null;
        if (aedPads != null && !aedPads.IsComplete)
        {
            return $"Place the AED pads on the chest ({aedPads.HazardsFound}/{aedPads.HazardsRequired})";
        }

        if (aedController != null && !aedController.ShockDelivered)
        {
            return "Stand clear, then tap the shock button";
        }

        return "CPR & AED response complete!";
    }
}
