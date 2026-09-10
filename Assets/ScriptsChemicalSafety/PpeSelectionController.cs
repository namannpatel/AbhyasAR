using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One PPE choice in the loadout panel — text-labeled UI Toggle, not a modeled 3D item
/// (porting CoachPpeBuilder's composited-primitive PPE geometry from vr-safety-training
/// is a listed follow-up polish item, not required for a working stage).
/// </summary>
[Serializable]
public class PpeOption
{
    public string label = "PPE Item";
    public bool isRequired;
    public Toggle toggle;
}

/// <summary>
/// PPE-selection stage: don the correct required items (chemical goggles, respirator)
/// without also selecting the wrong decoys (dust mask, ear plugs) before Confirm is
/// pressed. Runs first, before the hazard-inspection stage.
/// </summary>
public class PpeSelectionController : MonoBehaviour
{
    [SerializeField] private PpeOption[] options;
    [SerializeField] private Button confirmButton;

    [Tooltip("The panel this controller's toggles/button live on. Hidden once Confirm is pressed, shown again on ResetSelection.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Raised once, when Confirm is pressed (regardless of whether the loadout was correct).")]
    public UnityEvent OnConfirmed;

    public bool IsResolved { get; private set; }
    public bool WrongPpeSelected { get; private set; }
    public bool MissingRequiredPpe { get; private set; }

    private void OnEnable()
    {
        confirmButton?.onClick.AddListener(Confirm);
    }

    private void OnDisable()
    {
        confirmButton?.onClick.RemoveListener(Confirm);
    }

    /// <summary>Resets toggle state and resolution flags for a fresh attempt.</summary>
    public void ResetSelection()
    {
        IsResolved = false;
        WrongPpeSelected = false;
        MissingRequiredPpe = false;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (options == null)
        {
            return;
        }

        foreach (var option in options)
        {
            if (option?.toggle != null)
            {
                option.toggle.isOn = false;
            }
        }
    }

    private void Confirm()
    {
        if (IsResolved || options == null)
        {
            return;
        }

        bool missingRequired = false;
        bool wrongSelected = false;

        foreach (var option in options)
        {
            if (option?.toggle == null)
            {
                continue;
            }

            bool selected = option.toggle.isOn;
            if (option.isRequired && !selected)
            {
                missingRequired = true;
            }
            if (!option.isRequired && selected)
            {
                wrongSelected = true;
            }
        }

        MissingRequiredPpe = missingRequired;
        WrongPpeSelected = wrongSelected;
        IsResolved = true;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        OnConfirmed?.Invoke();
    }
}
