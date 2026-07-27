using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class CustomRoomManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomIDText;
    [SerializeField] private Button copyIDButton;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private GameObject playerListItemWithKickPrefab;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject hostIndicator;
    [SerializeField] private TextMeshProUGUI pingText;
    
    private string currentRoomID;
    private List<GameObject> playerListItems = new List<GameObject>();
    private bool isHost = false;
    private UnityEngine.Ping systemPingFallback;
    private int cachedPingMs = -1;
    
    private void Start()
    {
        // Validate required references
        if (!ValidateReferences())
        {
            return;
        }
        
        leaveButton.onClick.AddListener(OnLeaveRoom);
        copyIDButton.onClick.AddListener(OnCopyRoomID);
        startGameButton.onClick.AddListener(OnStartGame);
        
        // Hide start button initially
        startGameButton.gameObject.SetActive(false);
        
        ClearError();
        UpdateStatus("Initializing...");
        
        // Setup Internet Speed / Ping display UI
        EnsurePingUI();
        InvokeRepeating(nameof(UpdatePingDisplay), 0.1f, 0.5f);
        
        // Check if we're joining or creating
        string joinRoomID = PlayerPrefs.GetString("JoinRoomID", "");
        
        // Store the intent but don't execute immediately
        if (string.IsNullOrEmpty(joinRoomID))
        {
            isHost = true;
            UpdateStatus("Preparing to create room...");
        }
        else
        {
            isHost = false;
            currentRoomID = joinRoomID;
            UpdateStatus($"Preparing to join room {joinRoomID}...");
            PlayerPrefs.DeleteKey("JoinRoomID");
        }
        
        // Wait for Photon to be ready before proceeding
        if (!PhotonNetwork.IsConnected)
        {
            UpdateStatus("Connecting to servers...");
        }
        else if (!PhotonNetwork.InLobby)
        {
            UpdateStatus("Joining lobby...");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            // Ready to create or join
            ExecuteRoomAction();
        }
    }
    
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
    
    private void ExecuteRoomAction()
    {
        if (isHost)
        {
            CreateNewRoom();
        }
        else
        {
            JoinRoom(currentRoomID);
        }
    }
    
    private bool ValidateReferences()
    {
        bool isValid = true;
        
        if (roomIDText == null) { Debug.LogError("CustomRoomManager: RoomIDText is not assigned!"); isValid = false; }
        if (copyIDButton == null) { Debug.LogError("CustomRoomManager: CopyIDButton is not assigned!"); isValid = false; }
        if (playerListContent == null) { Debug.LogError("CustomRoomManager: PlayerListContent is not assigned!"); isValid = false; }
        if (playerListItemPrefab == null) { Debug.LogError("CustomRoomManager: PlayerListItemPrefab is not assigned!"); isValid = false; }
        if (leaveButton == null) { Debug.LogError("CustomRoomManager: LeaveButton is not assigned!"); isValid = false; }
        if (startGameButton == null) { Debug.LogError("CustomRoomManager: StartGameButton is not assigned!"); isValid = false; }
        if (errorText == null) { Debug.LogError("CustomRoomManager: ErrorText is not assigned!"); isValid = false; }
        if (statusText == null) { Debug.LogError("CustomRoomManager: StatusText is not assigned!"); isValid = false; }
        if (hostIndicator == null) { Debug.LogError("CustomRoomManager: HostIndicator is not assigned!"); isValid = false; }
        
        if (!isValid)
        {
            Debug.LogError("CustomRoomManager: Missing references! Please assign all fields in the Inspector.");
        }
        
        return isValid;
    }
    
    #region Room Management
    
    private void CreateNewRoom()
    {
        isHost = true;
        currentRoomID = GenerateRoomID();
        
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 10,
            IsVisible = false, // Private room
            IsOpen = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable {
                { "RoomID", currentRoomID }
            }
        };
        
        PhotonNetwork.CreateRoom(currentRoomID, roomOptions);
    }
    
    private void JoinRoom(string roomID)
    {
        isHost = false;
        currentRoomID = roomID;
        PhotonNetwork.JoinRoom(roomID);
    }
    
    private string GenerateRoomID()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] roomID = new char[6];
        
        for (int i = 0; i < 6; i++)
        {
            roomID[i] = chars[Random.Range(0, chars.Length)];
        }
        
        return new string(roomID);
    }
    
    #endregion
    
    #region Photon Callbacks
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("CustomRoomManager: Connected to Master Server");
        UpdateStatus("Connected! Joining lobby...");
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("CustomRoomManager: Joined lobby, executing room action");
        ExecuteRoomAction();
    }
    
    public override void OnCreatedRoom()
    {
        Debug.Log($"Room created with ID: {currentRoomID}");
        roomIDText.text = $"Room ID: {currentRoomID}";
        hostIndicator.SetActive(true);
        UpdateStatus("Room created! Waiting for players...");
        
        // Show start button for host
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(true);
        }
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room successfully");
        
        if (!isHost)
        {
            currentRoomID = PhotonNetwork.CurrentRoom.Name;
            roomIDText.text = $"Room ID: {currentRoomID}";
            hostIndicator.SetActive(false);
            UpdateStatus("Connected! Waiting for host to start...");
        }
        else
        {
            UpdateStatus("Room ready! You can start the game when ready.");
        }
        
        UpdatePlayerList();
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ShowError($"Failed to join room: {message}");
        UpdateStatus("Join failed. Returning to menu...");
        Invoke(nameof(ReturnToMainMenu), 3f);
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ShowError($"Failed to create room: {message}");
        UpdateStatus("Creation failed. Returning to menu...");
        Invoke(nameof(ReturnToMainMenu), 3f);
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected: {cause}");
        ShowError($"Connection lost: {cause}");
        UpdateStatus("Disconnected. Returning to menu...");
        Invoke(nameof(ReturnToMainMenu), 3f);
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined");
        UpdatePlayerList();
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left");
        UpdatePlayerList();
    }
    
    public override void OnLeftRoom()
    {
        ReturnToMainMenu();
    }
    
    #endregion
    
    #region UI Management
    
    private void UpdatePlayerList()
    {
        // Null checks
        if (playerListContent == null || playerListItemPrefab == null)
        {
            Debug.LogError("CustomRoomManager: PlayerListContent or PlayerListItemPrefab is not assigned!");
            return;
        }
        
        // Clear existing list
        foreach (GameObject item in playerListItems)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        // Create new list
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            GameObject itemPrefab = isHost ? playerListItemWithKickPrefab : playerListItemPrefab;
            GameObject item = Instantiate(itemPrefab, playerListContent);
            
            TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = player.NickName + (player.IsMasterClient ? " (Host)" : "");
            }
            
            // Setup kick button if host
            if (isHost && !player.IsMasterClient)
            {
                Button kickButton = item.GetComponentInChildren<Button>();
                if (kickButton != null)
                {
                    Player targetPlayer = player;
                    kickButton.onClick.AddListener(() => KickPlayer(targetPlayer));
                }
            }
            
            playerListItems.Add(item);
        }
    }
    
    private void KickPlayer(Player player)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CloseConnection(player);
        }
    }
    
    private void OnCopyRoomID()
    {
        GUIUtility.systemCopyBuffer = currentRoomID;
        ShowError($"Room ID copied: {currentRoomID}");
    }
    
    private void OnStartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            ShowError("Only the host can start the game!");
            return;
        }
        
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            ShowError("Need at least 2 players to start!");
            return;
        }
        
        UpdateStatus("Starting game...");
        
        // Close the room
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        
        // Load game scene for everyone
        PhotonNetwork.LoadLevel("GameScene"); // Replace with your actual game scene
    }
    
    private void OnLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }
    
    private void ReturnToMainMenu()
    {
        PhotonNetwork.LoadLevel("MainMenu");
    }
    
    private void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        CancelInvoke(nameof(ClearError));
        Invoke(nameof(ClearError), 5f);
    }
    
    private void ClearError()
    {
        errorText.text = "";
        errorText.gameObject.SetActive(false);
    }
    
    private void EnsurePingUI()
    {
        if (pingText != null) return;

        // Auto-find ping text UI in canvas if unassigned
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        TextMeshProUGUI[] tmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name.ToLower();
            if (n.Contains("ping") || n.Contains("speed") || n.Contains("internet") || n.Contains("latency"))
            {
                pingText = tmp;
                break;
            }
        }

        // Dynamically instantiate PingText UI if not present in scene
        if (pingText == null)
        {
            GameObject pingGO = new GameObject("PingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pingGO.transform.SetParent(canvas.transform, false);

            RectTransform rect = pingGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-25f, 25f);
            rect.sizeDelta = new Vector2(320f, 40f);

            pingText = pingGO.GetComponent<TextMeshProUGUI>();
            pingText.fontSize = 20;
            pingText.alignment = TextAlignmentOptions.BottomRight;
            pingText.raycastTarget = false;
            pingText.fontStyle = FontStyles.Bold;
        }
    }

    private void UpdatePingDisplay()
    {
        EnsurePingUI();
        if (pingText == null) return;

        int pingMs = GetCurrentPingMs();

        if (pingMs <= 0)
        {
            pingText.text = "Internet Speed: <color=#CCCCCC>Measuring...</color>";
            return;
        }

        string qualityStr;
        string colorHex;

        if (pingMs < 80)
        {
            qualityStr = "Strong";
            colorHex = "#00FF66"; // Green
        }
        else if (pingMs < 200)
        {
            qualityStr = "Good";
            colorHex = "#FFCC00"; // Yellow
        }
        else if (pingMs < 400)
        {
            qualityStr = "Weak";
            colorHex = "#FF8800"; // Orange
        }
        else
        {
            qualityStr = "Poor";
            colorHex = "#FF3333"; // Red
        }

        pingText.text = $"Internet Speed: <color={colorHex}>{pingMs}ms ({qualityStr})</color>";
    }

    private int GetCurrentPingMs()
    {
        if (PhotonNetwork.IsConnected)
        {
            return PhotonNetwork.GetPing();
        }

        // System ping fallback
        if (systemPingFallback == null)
        {
            systemPingFallback = new UnityEngine.Ping("8.8.8.8");
        }
        else if (systemPingFallback.isDone)
        {
            cachedPingMs = systemPingFallback.time;
            systemPingFallback = new UnityEngine.Ping("8.8.8.8");
        }

        return cachedPingMs;
    }
    
    #endregion
}
