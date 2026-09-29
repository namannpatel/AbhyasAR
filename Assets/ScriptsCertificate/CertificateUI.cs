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
    private string moduleKey;
    private int score;
    private bool showing;
    private GameObject certificateVisual;
    private RawImage certificateQr;
    private TMP_Text certificateName;
    private TMP_Text certificateModule;
    private TMP_Text certificateDate;
    private TMP_Text certificateMeta;

    private void Awake()
    {
        // This script lives on panelRoot itself, which is saved inactive -- so Awake first runs
        // during Show()'s SetActive(true). Hiding unconditionally here undid that first Show(),
        // so the first "Get Certificate" tap appeared to do nothing.
        if (panelRoot != null && !showing)
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
        Show(InferModuleKey(forModuleName), forModuleName, forScore);
    }

    public void Show(string forModuleKey, string forModuleName, int forScore)
    {
        moduleKey = forModuleKey;
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
            showing = true;
            panelRoot.SetActive(true);
            showing = false;
        }
        if (qrImage != null)
        {
            qrImage.texture = null;
        }
        if (certificateVisual != null)
        {
            certificateVisual.SetActive(false);
        }
        if (detailsText != null)
        {
            detailsText.text = LocalizationManager.Get("cert_enter_name_prompt");
        }
    }

    /// <summary>
    /// Opens the panel and, when the trainee's name is already known (logged-in worker, or the
    /// name used last time on this device), generates the certificate straight away -- used when
    /// a training is completed, so there's no separate "Get Certificate" step. With no known name
    /// the panel just waits for one, same as Show().
    /// </summary>
    public void ShowAndGenerate(string forModuleName, int forScore)
    {
        ShowAndGenerate(InferModuleKey(forModuleName), forModuleName, forScore);
    }

    public void ShowAndGenerate(string forModuleKey, string forModuleName, int forScore)
    {
        Show(forModuleKey, forModuleName, forScore);
        string knownName = AuthService.IsLoggedIn
            ? AuthService.CurrentWorker.displayName
            : nameInput != null ? nameInput.text : string.Empty;
        if (!string.IsNullOrWhiteSpace(knownName))
        {
            HandleGenerate();
        }
    }

    /// <summary>
    /// Opens the certificate from any training scene. It reuses an authored CertificateUI when
    /// available and creates a full-screen runtime host otherwise (FireTraining currently has no
    /// serialized certificate panel).
    /// </summary>
    public static void ShowForTraining(string forModuleKey, string forModuleName, int forScore)
    {
        CertificateUI target = FindOrCreatePanel();
        target?.ShowAndGenerate(forModuleKey, forModuleName, forScore);
    }

    /// <summary>Reopens an already earned certificate without issuing another one.</summary>
    public static void ShowSaved(CertificateRecord certificate, string traineeName, string forModuleName)
    {
        if (certificate == null) return;
        CertificateUI target = FindOrCreatePanel();
        if (target == null) return;
        target.Show(certificate.module, forModuleName, certificate.score);
        target.ShowCertificateArtwork(traineeName, certificate,
            CertificateService.RenderQrTexture(CertificateService.BuildVerificationUrl(certificate.id)));
    }

    private static CertificateUI FindOrCreatePanel()
    {
        CertificateUI target = null;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<CertificateUI>())
        {
            if (candidate.gameObject.scene.IsValid())
            {
                target = candidate;
                break;
            }
        }

        if (target == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("CertificateUI: no Canvas found in this scene.");
                return null;
            }
            var host = new GameObject("RuntimeCertificatePanel", typeof(RectTransform), typeof(Image));
            host.transform.SetParent(canvas.transform, false);
            var rect = host.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            host.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.96f);
            target = host.AddComponent<CertificateUI>();
            target.panelRoot = host;
        }

        return target;
    }

    private void HandleGenerate()
    {
        string traineeName = AuthService.IsLoggedIn
            ? AuthService.CurrentWorker.displayName
            : nameInput != null ? nameInput.text.Trim() : string.Empty;
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

        var certificate = CertificateStore.GetOrCreate(moduleKey, score, DateTime.UtcNow);
        string verificationUrl = CertificateService.BuildVerificationUrl(certificate.id);
        Texture2D qrTexture = CertificateService.RenderQrTexture(verificationUrl);

        if (qrImage != null)
        {
            qrImage.texture = qrTexture;
        }
        if (detailsText != null)
        {
            detailsText.text = LocalizationManager.Get("cert_details_format", traineeName, moduleName, score);
        }
        ShowCertificateArtwork(traineeName, certificate, qrTexture);
        SyncService.Instance?.RequestSyncSoon();
    }

    private void HandleClose()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void ShowCertificateArtwork(string traineeName, CertificateRecord certificate, Texture2D qrTexture)
    {
        EnsureCertificateArtwork();
        if (certificateVisual == null) return;

        certificateName.text = traineeName;
        certificateModule.text = $"For successfully completing {moduleName}.";
        DateTime issuedAt = DateTime.TryParse(certificate.issuedAtUtc, out var parsed) ? parsed : DateTime.UtcNow;
        certificateDate.text = issuedAt.ToLocalTime().ToString("dd MMMM yyyy");
        string shortId = string.IsNullOrEmpty(certificate.id)
            ? "—"
            : certificate.id.Substring(0, Math.Min(8, certificate.id.Length)).ToUpperInvariant();
        certificateMeta.text = $"Score {certificate.score}/100   •   Certificate {shortId}";
        certificateQr.texture = qrTexture;
        certificateVisual.SetActive(true);
        certificateVisual.transform.SetAsLastSibling();
    }

    private void EnsureCertificateArtwork()
    {
        if (certificateVisual != null || panelRoot == null) return;
        Texture2D template = Resources.Load<Texture2D>("Certificates/SurakshaCertificateTemplate");
        if (template == null)
        {
            Debug.LogError("CertificateUI: certificate template is missing from Resources/Certificates.");
            return;
        }

        certificateVisual = new GameObject("CertificateArtwork", typeof(RectTransform), typeof(Image));
        certificateVisual.transform.SetParent(panelRoot.transform, false);
        var rootRect = certificateVisual.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        certificateVisual.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.97f);

        var frame = new GameObject("Certificate", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        frame.transform.SetParent(certificateVisual.transform, false);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.035f, 0.055f);
        frameRect.anchorMax = new Vector2(0.965f, 0.945f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        frame.GetComponent<RawImage>().texture = template;
        frame.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        frame.GetComponent<AspectRatioFitter>().aspectRatio = 1607f / 1080f;

        certificateName = AddText(frame.transform, "TraineeName", new Vector2(0.20f, 0.57f), new Vector2(0.80f, 0.67f), 52, FontStyles.Bold);
        certificateModule = AddText(frame.transform, "Module", new Vector2(0.16f, 0.32f), new Vector2(0.79f, 0.45f), 29, FontStyles.Normal);
        certificateDate = AddText(frame.transform, "IssueDate", new Vector2(0.45f, 0.23f), new Vector2(0.68f, 0.30f), 25, FontStyles.Bold);
        certificateMeta = AddText(frame.transform, "CertificateMeta", new Vector2(0.16f, 0.08f), new Vector2(0.66f, 0.15f), 17, FontStyles.Normal);

        var qr = new GameObject("VerificationQr", typeof(RectTransform), typeof(RawImage));
        qr.transform.SetParent(frame.transform, false);
        var qrRect = qr.GetComponent<RectTransform>();
        qrRect.anchorMin = new Vector2(0.79f, 0.055f);
        qrRect.anchorMax = new Vector2(0.925f, 0.255f);
        qrRect.offsetMin = Vector2.zero;
        qrRect.offsetMax = Vector2.zero;
        certificateQr = qr.GetComponent<RawImage>();

        var close = new GameObject("CloseCertificate", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(certificateVisual.transform, false);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.975f, 0.96f);
        closeRect.sizeDelta = new Vector2(52, 52);
        close.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.2f, 0.94f);
        close.GetComponent<Button>().onClick.AddListener(HandleClose);
        var closeLabel = AddText(close.transform, "Label", Vector2.zero, Vector2.one, 28, FontStyles.Normal, Color.white);
        closeLabel.text = "×";
    }

    private static TMP_Text AddText(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax,
        float fontSize, FontStyles style, Color? color = null)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(11, fontSize * 0.48f);
        text.fontStyle = style;
        text.color = color ?? new Color32(46, 58, 81, 255);
        text.enableWordWrapping = true;
        return text;
    }

    private static string InferModuleKey(string localizedName)
    {
        string value = (localizedName ?? string.Empty).ToLowerInvariant();
        return value.Contains("fire") || value.Contains("अग्नि") || value.Contains("ᱥᱮᱸᱜᱮᱞ")
            ? "fire_safety"
            : "machine_training";
    }
}
