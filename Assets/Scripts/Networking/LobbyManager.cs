using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI errorText;
    
    [Header("Settings")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private float countdownTime = 30f;
    
    private float currentCountdown;
    private bool isCountingDown = false;
    private List<GameObject> playerListItems = new List<GameObject>();
    
    private void Start()
    {
        // Validate required references
        if (!ValidateReferences())
        {
            return;
        }
        
        leaveButton.onClick.AddListener(OnLeaveRoom);
        ClearError();
        
        // Don't join room here - wait for OnConnectedToMaster or OnJoinedLobby callback
        if (!PhotonNetwork.IsConnected)
        {
            ShowError("Connecting to servers...");
        }
        else if (!PhotonNetwork.InLobby)
        {
            titleText.text = "Joining lobby...";
            PhotonNetwork.JoinLobby();
        }
        else
        {
            // Already in lobby, try to join room
            JoinOrCreateRoom();
        }
    }
    
    private bool ValidateReferences()
    {
        bool isValid = true;
        
        if (titleText == null) { Debug.LogError("LobbyManager: TitleText is not assigned!"); isValid = false; }
        if (timerText == null) { Debug.LogError("LobbyManager: TimerText is not assigned!"); isValid = false; }
        if (playerListContent == null) { Debug.LogError("LobbyManager: PlayerListContent is not assigned!"); isValid = false; }
        if (playerListItemPrefab == null) { Debug.LogError("LobbyManager: PlayerListItemPrefab is not assigned!"); isValid = false; }
        if (leaveButton == null) { Debug.LogError("LobbyManager: LeaveButton is not assigned!"); isValid = false; }
        if (errorText == null) { Debug.LogError("LobbyManager: ErrorText is not assigned!"); isValid = false; }
        
        if (!isValid)
        {
            Debug.LogError("LobbyManager: Missing references! Please assign all fields in the Inspector.");
        }
        
        return isValid;
    }
    
    private void Update()
    {
        if (isCountingDown)
        {
            currentCountdown -= Time.deltaTime;
            
            if (currentCountdown <= 0)
            {
                StartGame();
            }
            else
            {
                UpdateTimerDisplay();
            }
        }
    }
    
    #region Photon Callbacks
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        titleText.text = "Joining lobby...";
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined lobby, finding/creating room...");
        JoinOrCreateRoom();
    }
    
    private void JoinOrCreateRoom()
    {
        if (!PhotonNetwork.InRoom)
        {
            titleText.text = "Finding players...";
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            OnJoinedRoom();
        }
    }
    
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("No rooms available, creating new room");
        // No rooms available, create one
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 10,
            IsVisible = true,
            IsOpen = true
        };
        
        PhotonNetwork.CreateRoom(null, roomOptions);
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room successfully");
        titleText.text = "Finding Players...";
        UpdatePlayerList();
        CheckStartConditions();
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined");
        UpdatePlayerList();
        CheckStartConditions();
    }
    
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left");
        UpdatePlayerList();
        CheckStartConditions();
    }
    
    public override void OnLeftRoom()
    {
        PhotonNetwork.LoadLevel("MainMenu");
    }
    
    #endregion
    
    #region Room Management
    
    private void CheckStartConditions()
    {
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        
        if (playerCount >= minPlayersToStart)
        {
            if (!isCountingDown)
            {
                StartCountdown();
            }
        }
        else
        {
            StopCountdown();
        }
    }
    
    private void StartCountdown()
    {
        isCountingDown = true;
        currentCountdown = countdownTime;
        titleText.text = "Get Ready!";
        UpdateTimerDisplay();
    }
    
    private void StopCountdown()
    {
        isCountingDown = false;
        timerText.text = $"Waiting for players... ({PhotonNetwork.CurrentRoom.PlayerCount}/{minPlayersToStart})";
    }
    
    private void UpdateTimerDisplay()
    {
        int seconds = Mathf.CeilToInt(currentCountdown);
        timerText.text = $"Starting in: {seconds}s";
    }
    
    private void StartGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Close the room so no one else can join
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            
            // Load the game scene for everyone
            PhotonNetwork.LoadLevel("GameScene"); // Replace with your actual game scene name
        }
    }
    
    #endregion
    
    #region UI Management
    
    private void UpdatePlayerList()
    {
        // Null check for required references
        if (playerListContent == null)
        {
            Debug.LogError("LobbyManager: PlayerListContent is not assigned! Please assign it in the Inspector.");
            return;
        }
        
        if (playerListItemPrefab == null)
        {
            Debug.LogError("LobbyManager: PlayerListItemPrefab is not assigned! Please assign it in the Inspector.");
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
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            
            // Use the PlayerListItem component if available
            PlayerListItem listItem = item.GetComponent<PlayerListItem>();
            if (listItem != null)
            {
                listItem.SetPlayerName(player.NickName);
            }
            else
            {
                // Fallback to direct TextMeshPro access
                TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = player.NickName;
                }
            }
            
            playerListItems.Add(item);
        }
    }
    
    private void OnLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }
    
    private void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
    }
    
    private void ClearError()
    {
        errorText.text = "";
        errorText.gameObject.SetActive(false);
    }
    
    #endregion
}
