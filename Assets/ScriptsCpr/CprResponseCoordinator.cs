using System;
using UnityEngine;

/// <summary>
/// Result of one CPR &amp; AED Response attempt, surfaced to CprResultsUI.
/// </summary>
[Serializable]
public struct CprResult
{
    public bool calledForHelp;
    public int compressionCount;
    public int goodRateCompressionCount;
    public int targetCompressionCount;
    public bool breathsGiven;
    public bool aedOpened;
    public int aedPadsCorrect;
    public int aedPadsRequired;
    public int aedFalsePositives;
    public bool shockDelivered;
    public bool passed;
    public int score;
    public float elapsedSeconds;
}

/// <summary>
/// Sequences and scores the CPR &amp; AED Response module: call for help, chest
/// compressions, rescue breaths, then AED pad placement and shock delivery. Each stage
/// gates the next exactly like FireResponseCoordinator gates its own stages — e.g.
/// compressions don't start until the call-for-help prop is activated, and the AED
/// placement tap doesn't become available (see CprPlacementController's Stage.Aed) until
/// breaths are given (ReadyForAed).
/// </summary>
public class CprResponseCoordinator : MonoBehaviour
{
    [SerializeField] private ManualCallPointController callPoint;
    [SerializeField] private CprCompressionController compressions;
    [SerializeField] private CprBreathController breaths;
    [SerializeField] private FireHazardInspectionController aedPads;
    [SerializeField] private CprAedController aedController;

    public event Action<CprResult> OnModuleComplete;

    public bool CallForHelpDone { get; private set; }
    public bool CompressionsDone { get; private set; }
    public bool BreathsDone { get; private set; }

    /// <summary>True once breaths are given — gates CprPlacementController's AED-placement tap.</summary>
    public bool ReadyForAed => BreathsDone;

    /// <summary>Live elapsed time since this attempt started — for the AR toolbar's timer pill (see ArHudToolbar).</summary>
    public float ElapsedSeconds => Time.time - startTime;

    private float startTime;
    private bool moduleCompleted;

    private void Awake()
    {
        startTime = Time.time;
    }

    /// <summary>Wired by CprPlacementController the moment the call-for-help prop is placed.</summary>
    public void ConfigureCallPoint(ManualCallPointController controller)
    {
        if (callPoint != null)
        {
            callPoint.OnActivated.RemoveListener(HandleCallForHelp);
        }

        callPoint = controller;
        CallForHelpDone = false;

        if (callPoint != null)
        {
            callPoint.OnActivated.AddListener(HandleCallForHelp);
        }
    }

    /// <summary>Wired by CprPlacementController the moment the mannequin is placed.</summary>
    public void ConfigurePatient(CprCompressionController compressionController, CprBreathController breathController)
    {
        compressions = compressionController;
        breaths = breathController;
        CompressionsDone = false;
        BreathsDone = false;

        if (compressions != null)
        {
            compressions.OnCycleComplete.AddListener(HandleCompressionsComplete);
        }
        if (breaths != null)
        {
            breaths.OnBreathsComplete.AddListener(HandleBreathsComplete);
        }
    }

    /// <summary>Wired by CprPlacementController the moment the AED is placed (and the mannequin's pad markers activated).</summary>
    public void ConfigureAed(FireHazardInspectionController padController, CprAedController controller)
    {
        aedPads = padController;
        aedController = controller;

        if (aedPads != null)
        {
            aedPads.OnInspectionComplete.AddListener(HandlePadsComplete);
        }
        if (aedController != null)
        {
            aedController.OnShockDelivered.AddListener(HandleShockDelivered);
        }
    }

    private void HandleCallForHelp()
    {
        CallForHelpDone = true;
        compressions?.StartStage();
    }

    private void HandleCompressionsComplete()
    {
        CompressionsDone = true;
        breaths?.StartStage();
    }

    private void HandleBreathsComplete()
    {
        BreathsDone = true;
    }

    private void HandlePadsComplete()
    {
        aedController?.EnableShockButton();
    }

    private void HandleShockDelivered()
    {
        if (moduleCompleted)
        {
            return;
        }
        moduleCompleted = true;

        var result = new CprResult
        {
            calledForHelp = CallForHelpDone,
            compressionCount = compressions != null ? compressions.CompressionCount : 0,
            goodRateCompressionCount = compressions != null ? compressions.GoodRateCount : 0,
            targetCompressionCount = compressions != null ? compressions.TargetCompressionCount : 0,
            breathsGiven = BreathsDone,
            aedOpened = aedController != null && aedController.IsOpen,
            aedPadsCorrect = aedPads != null ? aedPads.HazardsFound : 0,
            aedPadsRequired = aedPads != null ? aedPads.HazardsRequired : 0,
            aedFalsePositives = aedPads != null ? aedPads.FalsePositives : 0,
            shockDelivered = true,
            elapsedSeconds = Time.time - startTime,
        };
        result.passed = result.calledForHelp && result.breathsGiven
                         && result.aedPadsCorrect == result.aedPadsRequired && result.aedFalsePositives == 0
                         && result.shockDelivered
                         && result.targetCompressionCount > 0
                         && result.goodRateCompressionCount >= result.targetCompressionCount / 2;
        result.score = ComputeScore(result);

        OnModuleComplete?.Invoke(result);
    }

    /// <summary>
    /// Deterministic points for the actions actually performed this attempt, normalized to
    /// a 0-100 scale — same correct-action-scores-positive/mistake-costs-points spirit as
    /// FireResponseCoordinator.ComputeScore.
    /// </summary>
    private static int ComputeScore(CprResult result)
    {
        const int maxRawScore = 500; // call(50) + compressions(200) + breaths(50) + pads(100) + shock(100)

        int raw = 0;
        raw += result.calledForHelp ? 50 : 0;
        raw += result.targetCompressionCount > 0
            ? Mathf.RoundToInt(200f * result.goodRateCompressionCount / result.targetCompressionCount)
            : 0;
        raw += result.breathsGiven ? 50 : 0;
        raw += result.aedPadsRequired > 0
            ? Mathf.RoundToInt(100f * result.aedPadsCorrect / result.aedPadsRequired) - result.aedFalsePositives * 25
            : 0;
        raw += result.shockDelivered ? 100 : 0;

        return Mathf.Clamp(Mathf.RoundToInt(raw / (float)maxRawScore * 100f), 0, 100);
    }
}
