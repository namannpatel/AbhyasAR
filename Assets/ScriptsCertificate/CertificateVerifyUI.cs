using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Verify Certificate" panel reachable from MainMenu: paste a certificate payload
/// (the text a Get-Certificate QR encodes) and check it offline via
/// CertificateService.TryVerify — no network, no server, no blockchain. Camera-based
/// QR scanning is not implemented here (it would need a separate decode library and
/// camera pipeline); pasting the payload text is the supported flow for this pass.
/// </summary>
public class CertificateVerifyUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_InputField payloadInput;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button verifyButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (panelRoot != null)
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
            panelRoot.SetActive(true);
        }
        if (payloadInput != null)
        {
            payloadInput.text = string.Empty;
        }
        if (resultText != null)
        {
            resultText.text = string.Empty;
        }
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
