using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class HotspotManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TextMeshProUGUI errorText;
    
    private OfflineNetworkManager networkManager;
    private List<GameObject> playerListItems = new List<GameObject>();
    private bool isHost = false;
    
    private void Start()
    {
        Debug.Log("[HotspotManager] Starting...");
        
        // Validate required references
        if (!ValidateReferences())
        {
            return;
        }
        
        // Create network manager
        Debug.Log("[HotspotManager] Creating OfflineNetworkManager...");
        GameObject networkObj = new GameObject("OfflineNetworkManager");
        networkManager = networkObj.AddComponent<OfflineNetworkManager>();
        DontDestroyOnLoad(networkObj);
        Debug.Log("[HotspotManager] OfflineNetworkManager created");
        
        // Subscribe to events
        networkManager.OnServerStarted += OnServerReady;
        networkManager.OnClientConnected += OnJoinedServer;
        networkManager.OnConnectionFailed += OnFailed;
        networkManager.OnPlayerListChanged += UpdatePlayerList;
        Debug.Log("[HotspotManager] Event listeners registered");
        
        // Setup UI
        ClearError();
        leaveButton.onClick.AddListener(OnLeave);
        
        // Check if we're hosting or joining
        isHost = PlayerPrefs.GetInt("IsHotspotHost", 1) == 1;
        Debug.Log($"[HotspotManager] Mode: {(isHost ? "HOST" : "CLIENT")}");
        
        if (isHost)
        {
            CreateHotspot();
            startGameButton.gameObject.SetActive(true);
            startGameButton.onClick.AddListener(OnStartGame);
        }
        else
        {
            JoinHotspot();
            startGameButton.gameObject.SetActive(false);
        }
        
        PlayerPrefs.DeleteKey("IsHotspotHost");
    }
    
    private bool ValidateReferences()
    {
        bool isValid = true;
        
        if (statusText == null) { Debug.LogError("HotspotManager: StatusText is not assigned!"); isValid = false; }
        if (playerListContent == null) { Debug.LogError("HotspotManager: PlayerListContent is not assigned!"); isValid = false; }
        if (playerListItemPrefab == null) { Debug.LogError("HotspotManager: PlayerListItemPrefab is not assigned!"); isValid = false; }
        if (startGameButton == null) { Debug.LogError("HotspotManager: StartGameButton is not assigned!"); isValid = false; }
        if (leaveButton == null) { Debug.LogError("HotspotManager: LeaveButton is not assigned!"); isValid = false; }
        if (errorText == null) { Debug.LogError("HotspotManager: ErrorText is not assigned!"); isValid = false; }
        
        if (!isValid)
        {
            Debug.LogError("HotspotManager: Missing references! Please assign all fields in the Inspector.");
        }
        
        return isValid;
    }
    
    private void CreateHotspot()
    {
        statusText.text = "Creating Hotspot...";
        networkManager.StartHost();
    }
    
    private void JoinHotspot()
    {
        statusText.text = "Searching for Hotspot...";
        networkManager.SearchForServers();
        
        // Timeout after 10 seconds
        Invoke(nameof(SearchTimeout), 10f);
    }
    
    private void SearchTimeout()
    {
        if (!networkManager.IsHost && playerListItems.Count == 0)
        {
            ShowError("No hotspot found. Make sure host has enabled hotspot.");
            Invoke(nameof(ReturnToMainMenu), 3f);
        }
    }
    
    private void OnServerReady()
    {
        statusText.text = "Hotspot Active - Waiting for players...";
        
        // Get saved player name
        string myPlayerName = PlayerPrefs.GetString("PlayerName", "You");
        UpdatePlayerList(new List<string> { $"{myPlayerName} (Host)" });
    }
    
    private void OnJoinedServer()
    {
        CancelInvoke(nameof(SearchTimeout));
        statusText.text = "Connected to Hotspot";
    }
    
    private void OnFailed()
    {
        ShowError("Connection failed. Please try again.");
        Invoke(nameof(ReturnToMainMenu), 3f);
    }
    
    private void UpdatePlayerList(List<string> players)
    {
        // Null checks
        if (playerListContent == null || playerListItemPrefab == null)
        {
            Debug.LogError("HotspotManager: PlayerListContent or PlayerListItemPrefab is not assigned!");
            return;
        }
        
        // Clear existing list
        foreach (GameObject item in playerListItems)
        {
            Destroy(item);
        }
        playerListItems.Clear();
        
        // Create new list
        foreach (string playerName in players)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContent);
            
            // Use PlayerListItem component if available
            PlayerListItem listItem = item.GetComponent<PlayerListItem>();
            if (listItem != null)
            {
                listItem.SetPlayerName(playerName);
            }
            else
            {
                // Fallback to direct TextMeshPro access
                TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = playerName;
                }
            }
            
            playerListItems.Add(item);
        }
        
        Debug.Log($"[HotspotManager] Updated player list: {players.Count} players");
    }
    
    private void OnStartGame()
    {
        if (isHost && networkManager != null)
        {
            Debug.Log("[HotspotManager] Host starting game for all players...");
            
            // Use NetworkManager's scene management to load scene for all clients
            var netManager = networkManager.GetNetworkManager();
            if (netManager != null && netManager.IsServer)
            {
                netManager.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                Debug.Log("[HotspotManager] Loading GameScene for all networked clients");
            }
            else
            {
                // Fallback if NetworkManager not ready
                Debug.LogWarning("[HotspotManager] NetworkManager not ready, loading locally only");
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            }
        }
    }
    
    private void OnLeave()
    {
        networkManager.Disconnect();
        ReturnToMainMenu();
    }
    
    private void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
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
    
    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerReady;
            networkManager.OnClientConnected -= OnJoinedServer;
            networkManager.OnConnectionFailed -= OnFailed;
            networkManager.OnPlayerListChanged -= UpdatePlayerList;
        }
    }
}
