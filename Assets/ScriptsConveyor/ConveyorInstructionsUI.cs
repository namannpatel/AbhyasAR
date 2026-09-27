using TMPro;
using UnityEngine;

/// <summary>
/// Single-line "what to do next" prompt for the Conveyor Belt module -- same per-frame
/// recomputed-from-state role as ChemicalInstructionsUI/TrainingInstructionsUI. Priority
/// order (highest first) so it never says two contradictory things at once: not placed,
/// then E-stopped, then whichever of Auto/Manual is the current mode. A short suffix reports
/// speed (and, only when relevant, Manual mode) without cluttering the main line.
/// </summary>
public class ConveyorInstructionsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Tooltip("Shared AR toolbar chrome (see ArHudToolbar) -- optional, wires this module's timer and reposition action into it if present.")]
    [SerializeField] private ArHudToolbar toolbar;

    private ConveyorPlacementController placementController;
    private ConveyorSpeedDialController speedDial;

    private void OnEnable()
    {
        placementController = FindFirstObjectByType<ConveyorPlacementController>();

        if (toolbar != null)
        {
            toolbar.ElapsedSecondsProvider = () => placementController != null ? placementController.ElapsedSeconds : 0f;
            toolbar.OnReposition = () => placementController?.ResetTraining();
        }
    }

    private void Update()
    {
        if (label == null)
        {
            return;
        }

        label.text = GetCurrentInstruction();
    }

    private string GetCurrentInstruction()
    {
        if (placementController == null)
        {
            placementController = FindFirstObjectByType<ConveyorPlacementController>();
            if (placementController == null)
            {
                return string.Empty;
            }
        }

        if (placementController.PlacedConveyor == null)
        {
            return "Tap the floor to place the conveyor";
        }

        var motion = placementController.MotionController;
        if (motion == null)
        {
            return string.Empty;
        }

        string line;
        if (motion.IsEStopped)
        {
            line = "E-STOPPED — tap the E-stop again to reset before continuing";
        }
        else if (motion.Mode == ConveyorMotionController.OperatingMode.Manual)
        {
            line = motion.IsJogging
                ? "Release to stop jogging the belt"
                : "Hold the green button to jog the belt";
        }
        else
        {
            line = motion.IsRunning
                ? "Tap STOP to stop the belt safely"
                : "Tap START to run the belt";
        }

        return line + GetStatusSuffix(motion);
    }

    private string GetStatusSuffix(ConveyorMotionController motion)
    {
        if (speedDial == null)
        {
            speedDial = FindFirstObjectByType<ConveyorSpeedDialController>();
        }

        string suffix = string.Empty;
        if (speedDial != null)
        {
            suffix += $"  •  Speed: {speedDial.CurrentPreset}";
        }
        if (motion.Mode == ConveyorMotionController.OperatingMode.Manual)
        {
            suffix += "  •  Mode: MANUAL";
        }
        return suffix;
    }
}
