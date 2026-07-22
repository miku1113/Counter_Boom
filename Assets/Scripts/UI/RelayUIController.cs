using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RelayUIController : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button generateCodeButton;
    [SerializeField] private Button disconnectButton;

    [Header("Input / Outputs")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TextMeshProUGUI generatedCodeText;

    [Header("Status Feedback")]
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        // 1. Assign Click Listeners
        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
        if (generateCodeButton != null) generateCodeButton.onClick.AddListener(OnGenerateCodeClicked);
        if (disconnectButton != null) disconnectButton.onClick.AddListener(OnDisconnectClicked);

        // 2. Clear Initial Text Values
        if (generatedCodeText != null) generatedCodeText.text = "JOIN CODE: -";
        UpdateStatus("Ready to connect");

        if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
    }

    private async void OnGenerateCodeClicked()
    {
        if (RelayNetworkManager.Instance == null)
        {
            UpdateStatus("<color=red>Error: Network Manager Missing</color>");
            return;
        }

        SetInteractiveState(false);
        UpdateStatus("Generating private Relay room code...");

        string joinCode = await RelayNetworkManager.Instance.StartPrivateHostWithRelay();

        if (!string.IsNullOrEmpty(joinCode))
        {
            UpdateStatus("<color=green>Private room created! Auto-filled code.</color>");
            if (generatedCodeText != null)
            {
                generatedCodeText.text = $"JOIN CODE: {joinCode}";
            }
            if (joinCodeInputField != null)
            {
                joinCodeInputField.text = joinCode;
            }
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(true);
        }
        else
        {
            UpdateStatus("<color=red>Failed to generate private room code</color>");
            SetInteractiveState(true);
        }
    }

    private async void OnHostClicked()
    {
        if (RelayNetworkManager.Instance == null)
        {
            Debug.LogError("[RelayUI] RelayNetworkManager instance is missing in the scene!");
            UpdateStatus("<color=red>Error: Network Manager Missing</color>");
            return;
        }

        SetInteractiveState(false);
        UpdateStatus("Connecting to Unity Services & allocating Relay...");

        // Call the manager to request a server allocation and start NGO Host
        string joinCode = await RelayNetworkManager.Instance.StartHostWithRelay();

        if (!string.IsNullOrEmpty(joinCode))
        {
            UpdateStatus("<color=green>Host started. Waiting for players...</color>");
            if (generatedCodeText != null)
            {
                generatedCodeText.text = $"JOIN CODE: {joinCode}";
            }
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(true);
        }
        else
        {
            UpdateStatus("<color=red>Failed to create Relay host allocation</color>");
            SetInteractiveState(true);
        }
    }

    private async void OnJoinClicked()
    {
        if (RelayNetworkManager.Instance == null)
        {
            Debug.LogError("[RelayUI] RelayNetworkManager instance is missing in the scene!");
            UpdateStatus("<color=red>Error: Network Manager Missing</color>");
            return;
        }

        if (joinCodeInputField == null || string.IsNullOrEmpty(joinCodeInputField.text))
        {
            UpdateStatus("<color=yellow>Please enter a valid 6-char Join Code</color>");
            return;
        }

        string rawJoinCode = joinCodeInputField.text.Trim().ToUpper();

        SetInteractiveState(false);
        UpdateStatus($"Resolving Join Code: {rawJoinCode}...");

        // Call the manager to connect using the user's Join Code
        bool joinSuccess = await RelayNetworkManager.Instance.StartClientWithRelay(rawJoinCode);

        if (joinSuccess)
        {
            UpdateStatus("<color=green>Connected! Joining game...</color>");
            if (disconnectButton != null) disconnectButton.gameObject.SetActive(true);
        }
        else
        {
            UpdateStatus("<color=red>Failed to connect. Check code and connection.</color>");
            SetInteractiveState(true);
        }
    }

    private void OnDisconnectClicked()
    {
        if (RelayNetworkManager.Instance != null)
        {
            RelayNetworkManager.Instance.Disconnect();
        }

        if (generatedCodeText != null) generatedCodeText.text = "JOIN CODE: -";
        if (joinCodeInputField != null) joinCodeInputField.text = "";
        
        UpdateStatus("Disconnected from Relay session");
        SetInteractiveState(true);
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Updates status UI text safely.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[RelayUI] {message}");
    }

    /// <summary>
    /// Prevents multiple click interactions while asynchronous connections are resolving.
    /// </summary>
    private void SetInteractiveState(bool state)
    {
        if (hostButton != null) hostButton.interactable = state;
        if (joinButton != null) joinButton.interactable = state;
        if (generateCodeButton != null) generateCodeButton.interactable = state;
        if (joinCodeInputField != null) joinCodeInputField.interactable = state;
    }
}
