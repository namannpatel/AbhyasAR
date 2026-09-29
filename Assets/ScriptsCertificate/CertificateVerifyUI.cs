using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MainMenu certificate panel. Shows the signed-in worker's saved certificates, refreshes
/// their issued certificates from Supabase, and explains the pass mark for missing ones.
/// The serialized legacy verification controls remain behind the worker view.
/// </summary>
public class CertificateVerifyUI : MonoBehaviour
{
    [Serializable] private class CertificateListRequest { public string p_token; }
    [Serializable] private class ServerCertificate { public string id; public string module; public int score; public string issued_at; }
    [Serializable] private class CertificateListResponse { public ServerCertificate[] certificates; }
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField payloadInput;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button verifyButton;
    [SerializeField] private Button closeButton;

    private bool showing;
    private GameObject workerCertificatesView;

    private void Awake()
    {
        // This script lives on panelRoot, which is saved inactive, so Awake first runs during
        // Show()'s SetActive(true) -- hiding unconditionally here undid the first tap.
        if (panelRoot != null && !showing)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        verifyButton?.onClick.AddListener(HandleVerify);
        closeButton?.onClick.AddListener(HandleClose);
    }

    private void OnDisable()
    {
        verifyButton?.onClick.RemoveListener(HandleVerify);
        closeButton?.onClick.RemoveListener(HandleClose);
    }

    /// <summary>
    /// Opens the panel with an empty input, ready for a payload to be pasted in.
    /// Called by MainMenu's "Verify Certificate" button.
    /// </summary>
    public void Show()
    {
        if (panelRoot != null)
        {
            showing = true;
            panelRoot.SetActive(true);
            showing = false;
        }
        if (payloadInput != null)
        {
            payloadInput.text = string.Empty;
        }
        if (resultText != null)
        {
            resultText.text = string.Empty;
        }

        ShowWorkerCertificates();
        if (AuthService.IsLoggedIn && !string.IsNullOrEmpty(AuthService.CurrentWorker.token))
            StartCoroutine(RefreshFromServer(AuthService.CurrentWorker.workerId, AuthService.CurrentWorker.token));
    }

    private IEnumerator RefreshFromServer(string workerId, string token)
    {
        RpcResult result = default;
        yield return SupabaseRpc.Call("worker_list_certificates",
            JsonUtility.ToJson(new CertificateListRequest { p_token = token }), r => result = r);
        if (!result.Ok) yield break; // Cached certificates remain available offline.

        CertificateListResponse response;
        try { response = JsonUtility.FromJson<CertificateListResponse>(result.body); }
        catch (ArgumentException) { yield break; }
        if (response?.certificates == null) yield break;

        var records = new List<CertificateRecord>();
        foreach (var item in response.certificates)
            records.Add(new CertificateRecord { id = item.id, module = item.module,
                score = item.score, issuedAtUtc = item.issued_at, synced = true });
        CertificateStore.MergeFromServer(workerId, records);
        if (panelRoot != null && panelRoot.activeSelf && AuthService.IsLoggedIn
            && AuthService.CurrentWorker.workerId == workerId)
            ShowWorkerCertificates();
    }

    private void ShowWorkerCertificates()
    {
        if (panelRoot == null) return;
        if (workerCertificatesView != null) Destroy(workerCertificatesView);

        workerCertificatesView = new GameObject("WorkerCertificates", typeof(RectTransform), typeof(Image));
        workerCertificatesView.transform.SetParent(panelRoot.transform, false);
        var root = workerCertificatesView.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        workerCertificatesView.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.15f, 0.98f);

        AddText(root, "My Certificates", new Vector2(0.08f, 0.81f), new Vector2(0.80f, 0.93f), 48);
        AddButton(root, "×", new Vector2(0.86f, 0.84f), new Vector2(0.95f, 0.94f),
            () => panelRoot.SetActive(false));

        if (!AuthService.IsLoggedIn)
        {
            AddText(root, "Sign in to view your certificates.", new Vector2(0.1f, 0.35f),
                new Vector2(0.9f, 0.65f), 32);
            return;
        }

        string workerId = AuthService.CurrentWorker.workerId;
        string workerName = AuthService.CurrentWorker.displayName;
        List<CertificateRecord> certificates = CertificateStore.GetForWorker(workerId);
        AddModule(root, certificates, "fire_safety", "Fire Safety Training", workerName, 0.49f);
        AddModule(root, certificates, "machine_training", "Machinery Training", workerName, 0.19f);
    }

    private static void AddModule(RectTransform root, List<CertificateRecord> certificates,
        string module, string title, string workerName, float bottom)
    {
        var card = new GameObject(title, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root, false);
        var rect = card.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, bottom);
        rect.anchorMax = new Vector2(0.92f, bottom + 0.25f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.13f, 0.17f, 0.25f, 1f);

        AddText(rect, title, new Vector2(0.04f, 0.59f), new Vector2(0.72f, 0.93f), 32);
        CertificateRecord certificate = certificates.Find(c => c.module == module && c.synced)
            ?? certificates.Find(c => c.module == module);
        if (certificate == null)
        {
            AddText(rect, $"Complete training with at least {TrainingScoring.PassMark}% to receive the certificate.",
                new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.57f), 24);
            return;
        }

        string status = certificate.synced ? "Verified online" : "Saved on this device · awaiting sync";
        AddText(rect, $"Earned · {certificate.score}%  |  {status}",
            new Vector2(0.04f, 0.12f), new Vector2(0.71f, 0.57f), 22);
        AddButton(rect, "View", new Vector2(0.73f, 0.24f), new Vector2(0.96f, 0.76f),
            () => CertificateUI.ShowSaved(certificate, workerName, title));
    }

    private static void AddText(Transform parent, string value, Vector2 min, Vector2 max, int size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15;
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
    }

    private static void AddButton(Transform parent, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.22f, 0.45f, 0.82f, 1f);
        go.GetComponent<Button>().onClick.AddListener(action);
        AddText(rect, title, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), 26);
        go.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
    }

    private void HandleVerify()
    {
        string payload = payloadInput != null ? payloadInput.text : string.Empty;
        if (resultText == null)
        {
            return;
        }

        if (CertificateService.TryVerify(payload, out CertificateInfo info))
        {
            resultText.text = LocalizationManager.Get("cert_verify_valid_format",
                info.traineeName, info.moduleName, info.score, info.completedAt.ToString("yyyy-MM-dd HH:mm"));
        }
        else
        {
            resultText.text = LocalizationManager.Get("cert_verify_invalid");
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
