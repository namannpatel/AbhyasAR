using System;

/// <summary>
/// Combined result of one Chemical Hazard Inspection attempt: PPE selection followed by
/// the hazard-inspection stage. Surfaced to ChemicalResultsUI.
/// </summary>
[Serializable]
public struct ChemicalSafetyResult
{
    public int hazardsFound;
    public int hazardsRequired;
    public int falsePositives;
    public bool ppeCorrect;
    public bool wrongPpeSelected;
    public bool missingRequiredPpe;
    public bool passed;
    public int score;
    public float elapsedSeconds;
}
