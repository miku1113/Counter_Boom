using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class RelayNetworkManager : MonoBehaviour
{
    public static RelayNetworkManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private int maxConnections = 3; // 3 connections = 4 players max (1 Host + 3 Clients)
    [SerializeField] private string gameplaySceneName = "GameScene";

    public string CurrentJoinCode { get; private set; }
    public string CurrentLobbyId => currentLobby != null ? currentLobby.Id : null;

    private bool isServicesInitialized = false;
    private Lobby currentLobby = null;
    private Coroutine heartbeatCoroutine = null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // Automatically initialize services and sign in anonymously on start
        await InitializeUnityServicesAsync();

        // Subscribe to transport failures (e.g. Relay timeouts or socket drops)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }
    }

    private void OnTransportFailure()
    {
        Debug.LogWarning("[RelayManager] Network transport failure detected! Disconnecting cleanly.");
        Disconnect();
    }

    /// <summary>
    /// Initializes Core Unity Services and logs the player in anonymously.
    /// </summary>
    public async Task InitializeUnityServicesAsync()
    {
        if (isServicesInitialized) return;

        try
        {
            Debug.Log("[RelayManager] Initializing Unity Services...");
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[RelayManager] Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[RelayManager] Signed in successfully! Player ID: {AuthenticationService.Instance.PlayerId}");
            }

            isServicesInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] Services initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// Attempts to quick-join a public Lobby. If none exist, automatically hosts a new game.
    /// </summary>
    public async Task<bool> QuickPlayMatchmaking()
    {
        if (!isServicesInitialized)
        {
            await InitializeUnityServicesAsync();
        }

        try
        {
            Debug.Log("[RelayManager] Querying for open lobbies...");
            
            // Query for open public lobbies that haven't started yet and have available slots
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 10,
                Filters = new System.Collections.Generic.List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0"
                    ),
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.IsLocked,
                        op: QueryFilter.OpOptions.EQ,
                        value: "0"
                    )
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            if (queryResponse.Results != null && queryResponse.Results.Count > 0)
            {
                Lobby targetLobby = null;
                foreach (var lobby in queryResponse.Results)
                {
                    bool started = false;
                    if (lobby.Data != null && lobby.Data.ContainsKey("MatchStarted"))
                    {
                        started = lobby.Data["MatchStarted"].Value == "true";
                    }

                    if (!started && lobby.Data != null && lobby.Data.ContainsKey("JoinCode"))
                    {
                        targetLobby = lobby;
                        break;
                    }
                }

                if (targetLobby != null)
                {
                    Debug.Log($"[RelayManager] Found active lobby '{targetLobby.Name}'. Joining...");
                    Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(targetLobby.Id);
                    currentLobby = joinedLobby;

                    string joinCode = joinedLobby.Data["JoinCode"].Value;
                    Debug.Log($"[RelayManager] Quick Joined Lobby '{joinedLobby.Name}'. Relay Join Code: {joinCode}");
                    
                    return await StartClientWithRelay(joinCode);
                }
            }

            // No suitable lobbies found: host a new match
            Debug.Log("[RelayManager] No open lobbies found via Query. Hosting a new match...");
            return await HostAndPublishLobby();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[RelayManager] Lobby Service Exception during matchmaking: {e.Message} (Code: {e.ErrorCode})");
            // Fallback: try to host
            return await HostAndPublishLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] General matchmaking exception: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts hosting a Relay game, and creates a matching public Lobby to broadcast the Join Code.
    /// </summary>
    private async Task<bool> HostAndPublishLobby()
    {
        try
        {
            // 1. Allocate Relay and start NGO Host
            string joinCode = await StartHostWithRelay();
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("[RelayManager] Failed to allocate Relay to host match.");
                return false;
            }

            // 2. Register the Lobby on Unity Services
            string lobbyName = $"Lobby_{Random.Range(1000, 9999)}";
            int maxPlayers = maxConnections + 1; // maxConnections + host

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "JoinCode", new DataObject(
                            visibility: DataObject.VisibilityOptions.Public,
                            value: joinCode
                        )
                    }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            currentLobby = lobby;
            Debug.Log($"[RelayManager] Created public Lobby '{lobbyName}' (ID: {lobby.Id}) for room code: {joinCode}");

            // 3. Start Lobby Heartbeat to keep it active
            if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = StartCoroutine(LobbyHeartbeatRoutine(lobby.Id, 15f));

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] Failed to host and publish lobby: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Coroutine that sends a heartbeat ping to Unity Lobbies every 15s to keep the lobby alive.
    /// </summary>
    private System.Collections.IEnumerator LobbyHeartbeatRoutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (currentLobby != null && currentLobby.Id == lobbyId)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    /// <summary>
    /// Allocates a new Relay server slot, gets a join code, sets transport data, and starts NGO Host.
    /// </summary>
    public async Task<string> StartHostWithRelay()
    {
        if (!isServicesInitialized)
        {
            await InitializeUnityServicesAsync();
        }

        try
        {
            Debug.Log($"[RelayManager] Requesting Relay allocation for {maxConnections} connections...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[RelayManager] Relay Allocation created successfully. Join Code: {joinCode}");

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[RelayManager] UnityTransport component is missing on the NetworkManager GameObject!");
                return null;
            }

            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[RelayManager] NGO Host started successfully over Unity Relay.");
                CurrentJoinCode = joinCode;

                // Load the gameplay scene. Netcode automatically syncs and loads this scene for joining clients!
                NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                return joinCode;
            }
            else
            {
                Debug.LogError("[RelayManager] Failed to start NetworkManager Host.");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] General Exception starting Host: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the Client Relay allocation using a Join Code, sets transport, and starts NGO Client.
    /// </summary>
    public async Task<bool> StartClientWithRelay(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogWarning("[RelayManager] Cannot join: Join Code is empty or null.");
            return false;
        }

        if (!isServicesInitialized)
        {
            await InitializeUnityServicesAsync();
        }

        try
        {
            Debug.Log($"[RelayManager] Joining Relay allocation using Join Code: {joinCode}");
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Debug.Log("[RelayManager] Joined Relay allocation successfully.");

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[RelayManager] UnityTransport component is missing on the NetworkManager GameObject!");
                return false;
            }

            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[RelayManager] NGO Client connecting over Unity Relay...");
                CurrentJoinCode = joinCode;
                return true;
            }
            else
            {
                Debug.LogError("[RelayManager] Failed to start NetworkManager Client.");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RelayManager] General Exception starting Client: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Shuts down the current active session and deletes any hosted lobbies.
    /// </summary>
    public async void Disconnect()
    {
        CurrentJoinCode = "";

        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }

        // Clean up Lobbies
        if (currentLobby != null)
        {
            try
            {
                // If we are the lobby host, delete the lobby from the backend
                if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    Debug.Log($"[RelayManager] Deleting Lobby '{currentLobby.Name}'...");
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RelayManager] Failed to delete Lobby: {e.Message}");
            }
            currentLobby = null;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[RelayManager] Shut down active Netcode connection.");
        }
    }
}
