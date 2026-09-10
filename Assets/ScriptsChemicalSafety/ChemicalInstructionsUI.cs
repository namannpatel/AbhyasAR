using TMPro;
using UnityEngine;

/// <summary>
/// Single-line "what to do next" prompt for the Chemical Hazard Inspection module —
/// same role as TrainingInstructionsUI in module 1, kept independent since the stages
/// differ (PPE selection, then hazard inspection, rather than pull/aim/squeeze/sweep).
/// </summary>
public class ChemicalInstructionsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private PpeSelectionController ppeSelection;

    private ChemicalPlacementController placementController;

    private void OnEnable()
    {
        placementController = FindFirstObjectByType<ChemicalPlacementController>();
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
            placementController = FindFirstObjectByType<ChemicalPlacementController>();
            if (placementController == null)
            {
                return string.Empty;
            }
        }

        if (placementController.PlacedScenario == null)
        {
            return "Scanning the room for a floor…";
        }

        if (ppeSelection != null && !ppeSelection.IsResolved)
        {
            return "Select your PPE, then press Confirm";
        }

        var inspection = placementController.PlacedScenario.GetComponentInChildren<ChemicalInspectionController>(true);
        if (inspection == null)
        {
            return string.Empty;
        }

        if (inspection.IsComplete)
        {
            return "Inspection complete!";
        }

        return $"Inspect the site — tap anything that looks unsafe ({inspection.HazardsFound}/{inspection.HazardsRequired} found)";
    }
}
