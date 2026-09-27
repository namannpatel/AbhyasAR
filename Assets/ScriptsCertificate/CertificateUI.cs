using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Get Certificate" panel shown from a results screen once a module is passed.
/// Builds a self-verifying offline payload via CertificateService and renders it as a
/// QR code the trainee can screenshot or show to be checked by CertificateVerifyUI
/// (on this device or another one running the app) — no network or blockchain involved.
/// </summary>
public class CertificateUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private RawImage qrImage;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button closeButton;

    private const string TraineeNamePrefKey = "ARBT_TraineeName";

    private string moduleName;
    private int score;

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        generateButton?.onClick.AddListener(HandleGenerate);
        closeButton?.onClick.AddListener(HandleClose);
    }

    private void OnDisable()
    {
        generateButton?.onClick.RemoveListener(HandleGenerate);
        closeButton?.onClick.RemoveListener(HandleClose);
    }

    /// <summary>
    /// Opens the panel for the given completed module/score. Called by a results UI's
    /// PASS branch (e.g. TrainingResultsUI, ChemicalResultsUI).
    /// </summary>
    public void Show(string forModuleName, int forScore)
    {
        moduleName = forModuleName;
        score = forScore;

        if (nameInput != null)
        {
            // Prefill with whatever name was used last time, so a returning trainee
            // doesn't have to retype it for every module.
            nameInput.text = AuthService.IsLoggedIn
                ? AuthService.CurrentWorker.displayName
                : PlayerPrefs.GetString(TraineeNamePrefKey, string.Empty);
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        if (qrImage != null)
        {
            qrImage.texture = null;
        }
        if (detailsText != null)
        {
            detailsText.text = LocalizationManager.Get("cert_enter_name_prompt");
        }
    }

    private void HandleGenerate()
    {
        string traineeName = nameInput != null ? nameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(traineeName))
        {
            if (detailsText != null)
            {
                detailsText.text = LocalizationManager.Get("cert_enter_name_required");
            }
            return;
        }

        PlayerPrefs.SetString(TraineeNamePrefKey, traineeName);
        PlayerPrefs.Save();

        string payload = CertificateService.GenerateCertificate(traineeName, moduleName, score, DateTime.UtcNow);
        Texture2D qrTexture = CertificateService.RenderQrTexture(payload);

        if (qrImage != null)
        {
            qrImage.texture = qrTexture;
        }
        if (detailsText != null)
        {
            detailsText.text = LocalizationManager.Get("cert_details_format", traineeName, moduleName, score);
        }
    }

    private void HandleClose()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
