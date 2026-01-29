using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;

public class OfflineNetworkManager : MonoBehaviour
{
    private NetworkManager networkManager;
    private UnityTransport transport;
    private LocalNetworkDiscovery discovery;
    
    public bool IsHost { get; private set; }
    public List<string> ConnectedPlayers { get; private set; } = new List<string>();
    
    public event System.Action OnServerStarted;
    public event System.Action OnClientConnected;
    public event System.Action OnConnectionFailed;
    public event System.Action<List<string>> OnPlayerListChanged;
    
    private void Awake()
    {
        Debug.Log("[OfflineNetwork] Awake - Initializing components...");
        
        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();
        discovery = GetComponent<LocalNetworkDiscovery>();
        
        if (networkManager == null)
        {
            Debug.Log("[OfflineNetwork] Creating NetworkManager component");
            networkManager = gameObject.AddComponent<NetworkManager>();
        }
        
        if (transport == null)
        {
            Debug.Log("[OfflineNetwork] Creating UnityTransport component");
            transport = gameObject.AddComponent<UnityTransport>();
        }
        
        if (discovery == null)
        {
            Debug.Log("[OfflineNetwork] Creating LocalNetworkDiscovery component");
            discovery = gameObject.AddComponent<LocalNetworkDiscovery>();
        }
        
        // Initialize NetworkManager config BEFORE assigning transport
        if (networkManager.NetworkConfig == null)
        {
            Debug.Log("[OfflineNetwork] Creating NetworkConfig");
            networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig();
        }
        
        // Now assign the transport
        networkManager.NetworkConfig.NetworkTransport = transport;
        
        Debug.Log("[OfflineNetwork] ✅ All components initialized successfully");
    }
    
    public void StartHost()
    {
        IsHost = true;
        
        Debug.Log("[OfflineNetwork] Starting host...");
        Debug.Log($"[OfflineNetwork] NetworkManager: {(networkManager != null ? "Found" : "NULL")}");
        Debug.Log($"[OfflineNetwork] Transport: {(transport != null ? "Found" : "NULL")}");
        Debug.Log($"[OfflineNetwork] Discovery: {(discovery != null ? "Found" : "NULL")}");
        
        // Set transport to listen on all interfaces
        transport.SetConnectionData("0.0.0.0", 7777);
        Debug.Log("[OfflineNetwork] Transport configured: 0.0.0.0:7777");
        
        // Start server
        bool started = networkManager.StartHost();
        
        if (started)
        {
            Debug.Log("[OfflineNetwork] ✅ Host started successfully!");
            discovery.StartServer();
            OnServerStarted?.Invoke();
            
            // Subscribe to connection events
            networkManager.OnClientConnectedCallback += OnClientConnectedToServer;
            networkManager.OnClientDisconnectCallback += OnClientDisconnectedFromServer;
            
            Debug.Log("[OfflineNetwork] Listening for client connections...");
        }
        else
        {
            Debug.LogError("[OfflineNetwork] ❌ Failed to start host!");
            OnConnectionFailed?.Invoke();
        }
    }
    
    public void StartClient(string serverIP)
    {
        IsHost = false;
        
        Debug.Log($"[OfflineNetwork] Starting client, connecting to {serverIP}:7777");
        
        // Set server IP
        transport.SetConnectionData(serverIP, 7777);
        
        // Start client
        bool started = networkManager.StartClient();
        
        if (started)
        {
            Debug.Log($"[OfflineNetwork] ✅ Client started, connecting to {serverIP}...");
            
            // Subscribe to connection events
            networkManager.OnClientConnectedCallback += OnClientConnectedToServerAsClient;
            networkManager.OnClientDisconnectCallback += OnClientDisconnectedAsClient;
        }
        else
        {
            Debug.LogError("[OfflineNetwork] ❌ Failed to start client!");
            OnConnectionFailed?.Invoke();
        }
    }
    
    public void SearchForServers()
    {
        Debug.Log("[OfflineNetwork] Searching for servers on local network...");
        discovery.OnServerFound += OnServerDiscovered;
        discovery.StartClient();
    }
    
    private void OnServerDiscovered(string serverIP)
    {
        Debug.Log($"[OfflineNetwork] ✅ Server discovered at {serverIP}, attempting to connect...");
        discovery.OnServerFound -= OnServerDiscovered;
        StartClient(serverIP);
    }
    
    private void OnClientConnectedToServer(ulong clientId)
    {
        if (IsHost)
        {
            Debug.Log($"[OfflineNetwork] ✅ Client {clientId} connected to host");
            UpdatePlayerList();
        }
    }
    
    private void OnClientDisconnectedFromServer(ulong clientId)
    {
        if (IsHost)
        {
            Debug.Log($"[OfflineNetwork] Client {clientId} disconnected from host");
            UpdatePlayerList();
        }
    }
    
    private void OnClientConnectedToServerAsClient(ulong clientId)
    {
        Debug.Log($"[OfflineNetwork] ✅ Successfully connected to server! (Client ID: {clientId})");
        OnClientConnected?.Invoke();
        UpdatePlayerList();
    }
    
    private void OnClientDisconnectedAsClient(ulong clientId)
    {
        Debug.LogWarning($"[OfflineNetwork] ❌ Disconnected from server (Client ID: {clientId})");
        OnConnectionFailed?.Invoke();
    }
    
    private void UpdatePlayerList()
    {
        ConnectedPlayers.Clear();
        
        // Get saved player name
        string myPlayerName = PlayerPrefs.GetString("PlayerName", "Player");
        
        if (networkManager.IsServer)
        {
            Debug.Log($"[OfflineNetwork] Updating player list. Connected clients: {networkManager.ConnectedClients.Count}");
            
            foreach (var client in networkManager.ConnectedClients)
            {
                // Use saved name for host (client ID 0), generic for others
                string playerName = client.Key == networkManager.LocalClientId 
                    ? $"{myPlayerName} (Host)" 
                    : $"Player {client.Key}";
                    
                ConnectedPlayers.Add(playerName);
                Debug.Log($"[OfflineNetwork] Added player: {playerName}");
            }
        }
        else if (networkManager.IsClient)
        {
            // Client view: show all connected players
            Debug.Log($"[OfflineNetwork] Client updating player list");
            
            // Add self
            ConnectedPlayers.Add(myPlayerName);
            
            // In a real implementation, this would sync from server
            // For now, show that we're connected
            ConnectedPlayers.Add("Host");
            
            Debug.Log($"[OfflineNetwork] Client sees {ConnectedPlayers.Count} players");
        }
        
        Debug.Log($"[OfflineNetwork] Total players in list: {ConnectedPlayers.Count}");
        OnPlayerListChanged?.Invoke(ConnectedPlayers);
    }
    
    public void Disconnect()
    {
        if (networkManager.IsHost)
        {
            networkManager.Shutdown();
        }
        else if (networkManager.IsClient)
        {
            networkManager.Shutdown();
        }
    }
    
    /// <summary>
    /// Get the underlying Unity Netcode NetworkManager
    /// </summary>
    public Unity.Netcode.NetworkManager GetNetworkManager()
    {
        return networkManager;
    }
    
    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnectedToServer;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
            networkManager.OnClientConnectedCallback -= OnClientConnectedToServerAsClient;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnectedAsClient;
        }
    }
}
