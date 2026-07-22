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
    [SerializeField] private int maxConnections = 9; // 9 connections = 10 players max (1 Host + 9 Clients)
    [SerializeField] private string lobbySceneName = "CustomLobby";
    [SerializeField] private string gameplaySceneName = "GameScene";

    public int MaxPlayers => maxConnections + 1;
    public string CurrentJoinCode { get; private set; }
    public string CurrentLobbyId => currentLobby != null ? currentLobby.Id : null;

    [System.Serializable]
    public struct PlayerMigrationSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public int health;
        public int currentWeaponIndex;
    }

    public static PlayerMigrationSnapshot LastPlayerSnapshot;
    public static bool HasSnapshot = false;

    public bool IsMigrating { get; private set; } = false;

    public static event System.Action<bool> OnMigrationStateChanged;
    public static event System.Action<string> OnMigrationStatusChanged;

    private bool isServicesInitialized = false;
    private Lobby currentLobby = null;
    private Coroutine heartbeatCoroutine = null;
    private Coroutine migrationCoroutine = null;

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

        // Subscribe to transport failures and disconnect callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnTransportFailure()
    {
        Debug.LogWarning("[RelayManager] Network transport failure detected!");
        if (currentLobby != null && !IsMigrating && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            StartHostMigration();
        }
        else if (!IsMigrating)
        {
            Disconnect();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"[RelayManager] Client disconnected: {clientId}");
        // If server/host disconnected (clientId 0 or ServerClientId), and we are a client in an active lobby
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && !IsMigrating)
        {
            if (clientId == NetworkManager.ServerClientId || clientId == 0)
            {
                Debug.Log("[RelayManager] Host disconnected! Triggering Host Migration...");
                StartHostMigration();
            }
        }
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
    /// Creates a private Relay host room (not published to public Lobby search).
    /// Random Quick Play players cannot join. Only players with the Join Code can join manually.
    /// </summary>
    public async Task<string> StartPrivateHostWithRelay()
    {
        return await StartHostWithRelay();
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

                // Load the interactive lobby scene first. Netcode automatically syncs and loads this scene for joining clients!
                string targetScene = !string.IsNullOrEmpty(lobbySceneName) ? lobbySceneName : gameplaySceneName;
                NetworkManager.Singleton.SceneManager.LoadScene(targetScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
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
    /// Host-only method that transitions all connected clients from the pre-game lobby scene to the gameplay scene.
    /// Locks the UGS lobby so Quick Play creates a new room for subsequent players.
    /// </summary>
    public async void StartMatchFromLobby()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            Debug.Log($"[RelayManager] Host starting match! Locking lobby '{currentLobby?.Id}' and loading scene '{gameplaySceneName}'...");

            if (currentLobby != null)
            {
                try
                {
                    await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                    {
                        IsLocked = true,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "MatchStarted", new DataObject(DataObject.VisibilityOptions.Public, "true") }
                        }
                    });
                    Debug.Log("[RelayManager] Lobby locked & marked MatchStarted = true successfully.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RelayManager] Failed to lock lobby on match start: {e.Message}");
                }
            }

            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[RelayManager] Only the Host can start the match!");
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

    /// <summary>
    /// Saves the current local player stats (position, health, weapon) prior to host migration.
    /// </summary>
    public void SaveLocalPlayerSnapshot()
    {
        var localPlayerObj = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null 
            ? NetworkManager.Singleton.LocalClient.PlayerObject 
            : null;

        if (localPlayerObj == null)
        {
            GameObject pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null) localPlayerObj = pObj.GetComponent<NetworkObject>();
        }

        if (localPlayerObj != null)
        {
            LastPlayerSnapshot = new PlayerMigrationSnapshot
            {
                position = localPlayerObj.transform.position,
                rotation = localPlayerObj.transform.rotation,
                health = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetCurrentHealth() : 100,
                currentWeaponIndex = WeaponController.Instance != null ? WeaponController.Instance.GetCurrentSlot() : 0
            };
            HasSnapshot = true;
            Debug.Log($"[RelayManager] Saved local player snapshot at {LastPlayerSnapshot.position}, HP: {LastPlayerSnapshot.health}");
        }
    }

    /// <summary>
    /// Initiates Host Migration when the host drops.
    /// </summary>
    public void StartHostMigration()
    {
        if (IsMigrating) return;
        IsMigrating = true;

        SaveLocalPlayerSnapshot();

        OnMigrationStateChanged?.Invoke(true);
        OnMigrationStatusChanged?.Invoke("Host connection lost! Initiating Host Migration...");

        if (migrationCoroutine != null) StopCoroutine(migrationCoroutine);
        migrationCoroutine = StartCoroutine(HostMigrationRoutine());
    }

    private System.Collections.IEnumerator HostMigrationRoutine()
    {
        Debug.Log("[HostMigration] Starting Host Migration Routine...");

        // 1. Shutdown existing broken client connection
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        yield return new WaitForSecondsRealtime(1.0f);

        if (currentLobby == null)
        {
            Debug.LogError("[HostMigration] No active lobby found to migrate!");
            OnMigrationStatusChanged?.Invoke("Migration failed: No active lobby.");
            yield return new WaitForSecondsRealtime(2.0f);
            IsMigrating = false;
            OnMigrationStateChanged?.Invoke(false);
            Disconnect();
            yield break;
        }

        // 2. Poll Lobby to get updated HostId (UGS Lobby re-assigns host automatically)
        string myPlayerId = AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : "";
        string updatedHostId = "";
        string oldJoinCode = CurrentJoinCode;
        Lobby updatedLobby = null;

        int retries = 0;
        bool isNewHost = false;

        while (retries < 15)
        {
            retries++;
            OnMigrationStatusChanged?.Invoke($"Checking Lobby Host Status ({retries}/15)...");

            var getLobbyTask = LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            while (!getLobbyTask.IsCompleted) yield return null;

            if (getLobbyTask.Status == TaskStatus.RanToCompletion && getLobbyTask.Result != null)
            {
                updatedLobby = getLobbyTask.Result;
                currentLobby = updatedLobby;
                updatedHostId = updatedLobby.HostId;

                Debug.Log($"[HostMigration] Lobby Host ID: {updatedHostId}, My Player ID: {myPlayerId}");

                if (updatedHostId == myPlayerId)
                {
                    isNewHost = true;
                    break;
                }
                else
                {
                    // Check if new host has already posted a new JoinCode
                    if (updatedLobby.Data != null && updatedLobby.Data.ContainsKey("JoinCode"))
                    {
                        string latestJoinCode = updatedLobby.Data["JoinCode"].Value;
                        if (!string.IsNullOrEmpty(latestJoinCode) && latestJoinCode != oldJoinCode)
                        {
                            Debug.Log($"[HostMigration] New Join Code detected from Host: {latestJoinCode}");
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSecondsRealtime(1.0f);
        }

        if (isNewHost)
        {
            OnMigrationStatusChanged?.Invoke("You are promoted to NEW HOST! Allocating Relay...");
            Debug.Log("[HostMigration] Promoted to new Lobby Host. Allocating new Relay server...");

            var createRelayTask = RelayService.Instance.CreateAllocationAsync(maxConnections);
            while (!createRelayTask.IsCompleted) yield return null;

            if (createRelayTask.Status != TaskStatus.RanToCompletion || createRelayTask.Result == null)
            {
                Debug.LogError("[HostMigration] Failed to allocate Relay as new host.");
                OnMigrationStatusChanged?.Invoke("Failed to allocate Relay.");
                IsMigrating = false;
                OnMigrationStateChanged?.Invoke(false);
                Disconnect();
                yield break;
            }

            Allocation allocation = createRelayTask.Result;
            var getJoinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            while (!getJoinCodeTask.IsCompleted) yield return null;

            string newJoinCode = getJoinCodeTask.Result;
            Debug.Log($"[HostMigration] New Relay Join Code generated: {newJoinCode}");

            // Update Lobby with new Join Code
            var updateLobbyTask = LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, newJoinCode) }
                }
            });
            while (!updateLobbyTask.IsCompleted) yield return null;

            // Start NGO Host
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if (NetworkManager.Singleton.StartHost())
            {
                CurrentJoinCode = newJoinCode;
                Debug.Log("[HostMigration] Started NGO Host successfully!");
            }
        }
        else
        {
            // We are a client connecting to the newly allocated host
            OnMigrationStatusChanged?.Invoke("Connecting to the new Host...");
            string newJoinCode = updatedLobby != null && updatedLobby.Data != null && updatedLobby.Data.ContainsKey("JoinCode")
                ? updatedLobby.Data["JoinCode"].Value
                : "";

            if (string.IsNullOrEmpty(newJoinCode) || newJoinCode == oldJoinCode)
            {
                Debug.LogError("[HostMigration] Failed to retrieve valid new Join Code.");
                OnMigrationStatusChanged?.Invoke("Migration timed out.");
                IsMigrating = false;
                OnMigrationStateChanged?.Invoke(false);
                Disconnect();
                yield break;
            }

            var startClientTask = StartClientWithRelay(newJoinCode);
            while (!startClientTask.IsCompleted) yield return null;
        }

        OnMigrationStatusChanged?.Invoke("Reconnected! Spawning Player & Restoring State...");

        float timeout = 5.0f;
        float elapsed = 0f;
        while ((NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            GameObject localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(localPlayerObj.transform);
            }
        }

        // Only the host respawns world pickups
        if (NetworkManager.Singleton.IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.SpawnItemsAroundPlayer();
        }

        // Notify GameManager to restore snapshot
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestorePlayerFromSnapshot();
        }

        // Countdown to unpause
        for (int i = 3; i > 0; i--)
        {
            OnMigrationStatusChanged?.Invoke($"Resuming Game in {i}...");
            yield return new WaitForSecondsRealtime(1.0f);
        }

        OnMigrationStatusChanged?.Invoke("Game Resumed!");
        yield return new WaitForSecondsRealtime(0.5f);

        IsMigrating = false;
        OnMigrationStateChanged?.Invoke(false);
        Debug.Log("[HostMigration] Host migration successfully completed!");
    }
}
