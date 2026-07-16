using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;

public class OfflineNetworkManager : MonoBehaviour
{
    private NetworkManager    networkManager;
    private UnityTransport    transport;
    private LocalNetworkDiscovery discovery;

    public bool          IsHost           { get; private set; }
    public List<string>  ConnectedPlayers { get; private set; } = new List<string>();

    public event System.Action               OnServerStarted;
    public event System.Action               OnClientConnected;
    public event System.Action               OnConnectionFailed;
    public event System.Action<List<string>> OnPlayerListChanged;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        Debug.Log("[OfflineNetwork] Initialising components...");

        networkManager = GetComponent<NetworkManager>()        ?? gameObject.AddComponent<NetworkManager>();
        transport      = GetComponent<UnityTransport>()        ?? gameObject.AddComponent<UnityTransport>();
        discovery      = GetComponent<LocalNetworkDiscovery>() ?? gameObject.AddComponent<LocalNetworkDiscovery>();

        if (networkManager.NetworkConfig == null)
            networkManager.NetworkConfig = new Unity.Netcode.NetworkConfig();

        networkManager.NetworkConfig.NetworkTransport = transport;

        Debug.Log("[OfflineNetwork] ✅ Components ready.");
    }

    private void OnDestroy()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback  -= OnClientConnectedToServer;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnectedFromServer;
        networkManager.OnClientConnectedCallback  -= OnClientConnectedAsClient;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnectedAsClient;
    }

    // ─── Host ─────────────────────────────────────────────────────────────────

    public void StartHost()
    {
        IsHost = true;
        transport.SetConnectionData("0.0.0.0", 7777);
        Debug.Log("[OfflineNetwork] Starting host on 0.0.0.0:7777...");

        bool started = networkManager.StartHost();
        if (started)
        {
            Debug.Log("[OfflineNetwork] ✅ Host started.");
            discovery.StartServer();
            OnServerStarted?.Invoke();
            networkManager.OnClientConnectedCallback  += OnClientConnectedToServer;
            networkManager.OnClientDisconnectCallback += OnClientDisconnectedFromServer;
        }
        else
        {
            Debug.LogError("[OfflineNetwork] ❌ Failed to start host.");
            OnConnectionFailed?.Invoke();
        }
    }

    // ─── Client ───────────────────────────────────────────────────────────────

    public void StartClient(string serverIP)
    {
        IsHost = false;
        transport.SetConnectionData(serverIP, 7777);
        Debug.Log($"[OfflineNetwork] Connecting to {serverIP}:7777...");

        bool started = networkManager.StartClient();
        if (started)
        {
            Debug.Log($"[OfflineNetwork] ✅ Client started.");
            networkManager.OnClientConnectedCallback  += OnClientConnectedAsClient;
            networkManager.OnClientDisconnectCallback += OnClientDisconnectedAsClient;
        }
        else
        {
            Debug.LogError("[OfflineNetwork] ❌ Failed to start client.");
            OnConnectionFailed?.Invoke();
        }
    }

    public void SearchForServers()
    {
        Debug.Log("[OfflineNetwork] Scanning local network for servers...");
        discovery.OnServerFound += OnServerDiscovered;
        discovery.StartClient();
    }

    // ─── Discovery Callback ───────────────────────────────────────────────────

    private void OnServerDiscovered(string ip)
    {
        Debug.Log($"[OfflineNetwork] Server found at {ip}, connecting...");
        discovery.OnServerFound -= OnServerDiscovered;
        StartClient(ip);
    }

    // ─── Network Callbacks ────────────────────────────────────────────────────

    private void OnClientConnectedToServer(ulong clientId)
    {
        if (!IsHost) return;
        Debug.Log($"[OfflineNetwork] Client {clientId} connected.");
        BroadcastPlayerList();
    }

    private void OnClientDisconnectedFromServer(ulong clientId)
    {
        if (!IsHost) return;
        Debug.Log($"[OfflineNetwork] Client {clientId} disconnected.");
        BroadcastPlayerList();
    }

    private void OnClientConnectedAsClient(ulong clientId)
    {
        Debug.Log($"[OfflineNetwork] ✅ Connected to server (local ID: {clientId}).");
        OnClientConnected?.Invoke();
        // Build a local view while we wait for the host to broadcast the full list
        UpdateLocalClientView();
    }

    private void OnClientDisconnectedAsClient(ulong clientId)
    {
        Debug.LogWarning($"[OfflineNetwork] ❌ Disconnected from server (ID: {clientId}).");
        OnConnectionFailed?.Invoke();
    }

    // ─── Player List ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the host: builds the real connected-client list from NGO
    /// and fires OnPlayerListChanged so all UI can update.
    /// </summary>
    private void BroadcastPlayerList()
    {
        if (!networkManager.IsServer) return;

        ConnectedPlayers.Clear();
        string myName = PlayerPrefs.GetString("PlayerName", "Player");

        foreach (var pair in networkManager.ConnectedClients)
        {
            ulong  id   = pair.Key;
            string name = (id == networkManager.LocalClientId)
                ? $"{myName} (Host)"
                : $"Player {id}";
            ConnectedPlayers.Add(name);
        }

        Debug.Log($"[OfflineNetwork] Player list updated: {ConnectedPlayers.Count} player(s).");
        OnPlayerListChanged?.Invoke(ConnectedPlayers);
    }

    /// <summary>
    /// Called on the client side: shows the local player and a placeholder
    /// until the host sends a full sync (full sync requires NGO RPCs — future work).
    /// </summary>
    private void UpdateLocalClientView()
    {
        ConnectedPlayers.Clear();
        string myName = PlayerPrefs.GetString("PlayerName", "Player");
        ConnectedPlayers.Add(myName);
        ConnectedPlayers.Add("Host (connecting...)");
        OnPlayerListChanged?.Invoke(ConnectedPlayers);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void Disconnect()
    {
        networkManager?.Shutdown();
    }

    /// <summary>Returns the underlying Unity Netcode NetworkManager.</summary>
    public NetworkManager GetNetworkManager() => networkManager;
}
