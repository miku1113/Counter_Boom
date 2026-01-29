using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class MainMenuManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private Button playOnlineButton;
    [SerializeField] private Button createCustomButton;
    [SerializeField] private Button joinCustomButton;
    [SerializeField] private Button createHotspotButton;
    [SerializeField] private Button joinHotspotButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject roomJoinPopup;
    [SerializeField] private GameObject nameEntryPanel;
    
    [Header("Error Display")]
    [SerializeField] private TextMeshProUGUI errorText;
    
    [Header("Room Join Popup")]
    [SerializeField] private TMP_InputField roomIDInput;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button cancelButton;
    
    [Header("Name Entry Panel")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button submitNameButton;
    [SerializeField] private TextMeshProUGUI nameInstructionText;
    
    [Header("Player Name Display")]
    [SerializeField] private TextMeshProUGUI playerNameDisplay;
    
    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        
        // Hide panels initially
        settingsPanel.SetActive(false);
        roomJoinPopup.SetActive(false);
        ClearError();
        
        // Check if player has a saved name
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        
        if (string.IsNullOrEmpty(savedName))
        {
            // No name saved - show name entry panel and hide main menu
            ShowNameEntryPanel();
        }
        else
        {
            // Name exists - set it and show main menu
            PhotonNetwork.NickName = savedName;
            ShowMainMenu();
        }
        
        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        
        // Setup button listeners
        playOnlineButton.onClick.AddListener(OnPlayOnline);
        createCustomButton.onClick.AddListener(OnCreateCustom);
        joinCustomButton.onClick.AddListener(OnJoinCustom);
        createHotspotButton.onClick.AddListener(OnCreateHotspot);
        joinHotspotButton.onClick.AddListener(OnJoinHotspot);
        settingsButton.onClick.AddListener(OnSettings);
        exitButton.onClick.AddListener(OnExit);
        
        // Room join popup
        joinRoomButton.onClick.AddListener(OnJoinRoomConfirm);
        cancelButton.onClick.AddListener(() => roomJoinPopup.SetActive(false));
        
        // Name entry
        submitNameButton.onClick.AddListener(OnSubmitName);
        nameInput.onSubmit.AddListener((value) => OnSubmitName()); // Allow Enter key to submit
    }
    
    private void ShowNameEntryPanel()
    {
        nameEntryPanel.SetActive(true);
        mainPanel.SetActive(false);
        nameInput.text = "";
        nameInstructionText.text = "Enter your player name:";
        nameInput.Select();
        nameInput.ActivateInputField();
    }
    
    private void ShowMainMenu()
    {
        nameEntryPanel.SetActive(false);
        mainPanel.SetActive(true);
        
        // Display player name if available
        if (playerNameDisplay != null)
        {
            string playerName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(playerName))
            {
                playerNameDisplay.text = playerName;
            }
        }
    }
    
    private void OnSubmitName()
    {
        string playerName = nameInput.text.Trim();
        
        // Validate name
        if (string.IsNullOrEmpty(playerName))
        {
            nameInstructionText.text = "<color=red>Please enter a valid name!</color>";
            return;
        }
        
        if (playerName.Length < 3)
        {
            nameInstructionText.text = "<color=red>Name must be at least 3 characters!</color>";
            return;
        }
        
        if (playerName.Length > 20)
        {
            nameInstructionText.text = "<color=red>Name must be less than 20 characters!</color>";
            return;
        }
        
        // Save the name
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();
        
        // Set Photon nickname
        PhotonNetwork.NickName = playerName;
        
        Debug.Log($"Player name set to: {playerName}");
        
        // Show main menu
        ShowMainMenu();
    }
    
    #region Button Handlers
    
    private void OnPlayOnline()
    {
        if (!PhotonNetwork.IsConnected)
        {
            ShowError("Not connected to Photon servers. Please wait...");
            return;
        }
        
        PhotonNetwork.LoadLevel("Lobby");
    }
    
    private void OnCreateCustom()
    {
        if (!PhotonNetwork.IsConnected)
        {
            ShowError("Not connected to Photon servers. Please wait...");
            return;
        }
        
        PhotonNetwork.LoadLevel("CustomLobby");
    }
    
    private void OnJoinCustom()
    {
        if (!PhotonNetwork.IsConnected)
        {
            ShowError("Not connected to Photon servers. Please wait...");
            return;
        }
        
        roomJoinPopup.SetActive(true);
        roomIDInput.text = "";
    }
    
    private void OnJoinRoomConfirm()
    {
        string roomID = roomIDInput.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(roomID))
        {
            ShowError("Please enter a valid Room ID");
            return;
        }
        
        if (roomID.Length != 6)
        {
            ShowError("Room ID must be 6 characters");
            return;
        }
        
        // Store room ID for CustomLobby to use
        PlayerPrefs.SetString("JoinRoomID", roomID);
        PhotonNetwork.LoadLevel("CustomLobby");
    }
    
    private void OnCreateHotspot()
    {
        // Check if hotspot is enabled (Android/iOS specific)
        if (!CheckHotspotEnabled())
        {
            ShowError("Mobile Hotspot is not enabled. Please enable it in your device settings.");
            return;
        }
        
        // Set flag that we're hosting
        PlayerPrefs.SetInt("IsHotspotHost", 1);
        UnityEngine.SceneManagement.SceneManager.LoadScene("HostsportLobby");
    }
    
    private void OnJoinHotspot()
    {
        // Check WiFi connectivity
        if (Application.internetReachability != NetworkReachability.ReachableViaLocalAreaNetwork)
        {
            ShowError("WiFi is not connected. Please connect to a WiFi network.");
            return;
        }
        
        // Store that we're joining (not hosting)
        PlayerPrefs.SetInt("IsHotspotHost", 0);
        UnityEngine.SceneManagement.SceneManager.LoadScene("HostsportLobby");
    }
    
    private void OnSettings()
    {
        settingsPanel.SetActive(true);
    }
    
    private void OnExit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    #endregion
    
    #region Helper Methods
    
    private bool CheckHotspotEnabled()
    {
        // This is platform-specific
        // For Android, you'd need a native plugin
        // For now, we'll assume it's enabled
        // You can implement native checks later
        
        #if UNITY_ANDROID
        // TODO: Implement Android hotspot check
        return true;
        #elif UNITY_IOS
        // iOS doesn't allow hotspot detection programmatically
        return true;
        #else
        return true;
        #endif
    }
    
    private void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        
        // Auto-hide after 5 seconds
        CancelInvoke(nameof(ClearError));
        Invoke(nameof(ClearError), 5f);
    }
    
    private void ClearError()
    {
        errorText.text = "";
        errorText.gameObject.SetActive(false);
    }
    
    #endregion
    
    #region Photon Callbacks
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server");
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        ShowError($"Disconnected from Photon: {cause}");
    }
    
    #endregion
}
