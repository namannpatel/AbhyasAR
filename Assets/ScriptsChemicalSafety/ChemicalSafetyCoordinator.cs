using System;
using UnityEngine;

/// <summary>
/// Sequences and scores the full Chemical Hazard Inspection module: PPE selection
/// first (don PPE before approaching the area — real-world order), then the
/// hazard-inspection stage, which stays inactive until PPE is confirmed. Same
/// event-struct convention as FireResponseCoordinator, kept as its own independent
/// script rather than sharing a base class with it.
/// </summary>
public class ChemicalSafetyCoordinator : MonoBehaviour
{
    [SerializeField] private PpeSelectionController ppeSelection;
    [SerializeField] private ChemicalInspectionController inspection;

    [Tooltip("Shared root of the hazard-inspection content. Stays inactive until PPE selection is confirmed.")]
    [SerializeField] private GameObject inspectionStageRoot;

    public event Action<ChemicalSafetyResult> OnModuleComplete;

    private bool ppeStageDone;
    private bool moduleCompleted;
    private float startTime;

    private void OnEnable()
    {
        startTime = Time.time;
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Wires this coordinator to a newly placed scenario's inspection content. Called
    /// at runtime by ChemicalPlacementController once the scenario prefab has been
    /// placed — mirrors FireResponseCoordinator.Configure()'s runtime-wiring pattern.
    /// </summary>
    public void Configure(ChemicalInspectionController inspectionController, GameObject stageRoot)
    {
        Unsubscribe();

        inspection = inspectionController;
        inspectionStageRoot = stageRoot;

        moduleCompleted = false;
        startTime = Time.time;

        // Sync rather than blindly reset: the player can confirm PPE before the
        // scenario finishes auto-placing (floor detection usually beats a deliberate
        // multi-tap UI action, but isn't guaranteed to), so pick up whatever state
        // ppeSelection is already in instead of discarding a real confirmation.
        ppeStageDone = ppeSelection != null && ppeSelection.IsResolved;

        if (inspectionStageRoot != null)
        {
            inspectionStageRoot.SetActive(ppeStageDone);
        }

        Subscribe();
    }

    /// <summary>
    /// Resets the PPE panel for a fresh attempt. Call before the next scenario
    /// auto-places (alongside ChemicalPlacementController.ResetTraining()) so the
    /// following Configure() picks up a clean, unconfirmed PPE state.
    /// </summary>
    public void ResetForRetry()
    {
        ppeSelection?.ResetSelection();
    }

    private void Subscribe()
    {
        if (ppeSelection != null) ppeSelection.OnConfirmed.AddListener(HandlePpeConfirmed);
        if (inspection != null) inspection.OnInspectionComplete.AddListener(HandleInspectionComplete);
    }

    private void Unsubscribe()
    {
        if (ppeSelection != null) ppeSelection.OnConfirmed.RemoveListener(HandlePpeConfirmed);
        if (inspection != null) inspection.OnInspectionComplete.RemoveListener(HandleInspectionComplete);
    }

    private void HandlePpeConfirmed()
    {
        ppeStageDone = true;

        if (inspectionStageRoot != null)
        {
            inspectionStageRoot.SetActive(true);
        }
    }

    private void HandleInspectionComplete()
    {
        if (moduleCompleted || !ppeStageDone || inspection == null || ppeSelection == null)
        {
            return;
        }
        moduleCompleted = true;

        var result = new ChemicalSafetyResult
        {
            hazardsFound = inspection.HazardsFound,
            hazardsRequired = inspection.HazardsRequired,
            falsePositives = inspection.FalsePositives,
            ppeCorrect = !ppeSelection.WrongPpeSelected && !ppeSelection.MissingRequiredPpe,
            wrongPpeSelected = ppeSelection.WrongPpeSelected,
            missingRequiredPpe = ppeSelection.MissingRequiredPpe,
            elapsedSeconds = Time.time - startTime,
        };
        result.passed = result.ppeCorrect && result.hazardsFound == result.hazardsRequired && result.falsePositives == 0;

        // Normalize to 0-100 against the maximum possible raw total (100 per required
        // hazard, from HazardInspectionSession's own scoring, plus 100 for a fully
        // correct PPE loadout), clamped at 0 so extra false positives don't push a bad
        // run below zero.
        int rawScore = inspection.Score + ComputePpeScore(result);
        int maxRawScore = result.hazardsRequired * 100 + 100;
        result.score = maxRawScore > 0
            ? Mathf.Clamp(Mathf.RoundToInt(rawScore / (float)maxRawScore * 100f), 0, 100)
            : 0;

        OnModuleComplete?.Invoke(result);
    }

    /// <summary>
    /// PPE loadout points, on top of HazardInspectionSession's own +100/-25-per-target
    /// score (already surfaced via inspection.Score): a fully correct loadout is worth
    /// 100, and each kind of mistake (missing a required item, wearing a wrong one)
    /// costs 25 — the same point scale vr-safety-training's TrainingSession uses.
    /// </summary>
    private static int ComputePpeScore(ChemicalSafetyResult result)
    {
        if (result.ppeCorrect)
        {
            return 100;
        }

        int score = 0;
        if (result.missingRequiredPpe) score -= 25;
        if (result.wrongPpeSelected) score -= 25;
        return score;
    }
}
