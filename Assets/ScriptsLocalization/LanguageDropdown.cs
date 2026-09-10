using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Populates a TMP_Dropdown with every language LocalizationManager ships (see
/// AvailableLanguages) and switches the active language when the player picks one. Works
/// from any scene it's placed in -- MainMenu is the primary spot (a language choice made
/// there should already be in effect once FireTraining loads), but the same component would
/// work anywhere.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private readonly List<string> languageCodes = new List<string>();
    private bool suppressCallback;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        LocalizationManager.EnsureInitialized();
        PopulateOptions();
        dropdown.onValueChanged.AddListener(HandleValueChanged);
    }

    private void OnDisable()
    {
        dropdown.onValueChanged.RemoveListener(HandleValueChanged);
    }

    private void PopulateOptions()
    {
        languageCodes.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        int selectedIndex = 0;

        for (int i = 0; i < LocalizationManager.AvailableLanguages.Length; i++)
        {
            var (code, displayName) = LocalizationManager.AvailableLanguages[i];
            languageCodes.Add(code);
            options.Add(new TMP_Dropdown.OptionData(displayName));
            if (code == LocalizationManager.CurrentLanguage)
            {
                selectedIndex = i;
            }
        }

        suppressCallback = true;
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(selectedIndex);
        suppressCallback = false;
    }

    private void HandleValueChanged(int index)
    {
        if (suppressCallback || index < 0 || index >= languageCodes.Count)
        {
            return;
        }
        LocalizationManager.SetLanguage(languageCodes[index]);
    }
}
