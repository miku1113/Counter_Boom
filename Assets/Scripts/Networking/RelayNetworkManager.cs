using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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

    // Holds the position, name and amount of a single world item pickup
    [System.Serializable]
    public struct WorldItemState
    {
        public Vector3 position;
        public string  itemName;   // matches InventoryItemData.itemName / ItemPickup prefab name
        public int     amount;
        public bool    wasDropped;
    }

    [System.Serializable]
    public struct PlayerMigrationSnapshot
    {
        public Vector3    position;
        public Quaternion rotation;
        public int        health;
        public bool       isGhost;
        public int        currentWeaponIndex;
        public int        medikitCount;
        public int        proteinShakeCount;
        public int        scopeCount;
        public bool       facingRight;        // CharacterAssembler sprite flip direction
        public Dictionary<AmmoType, int>    ammoCounts;
        public Dictionary<GrenadeType, int> grenadeCounts;
        // Equipped weapon prefab names per slot (index 0 and 1)
        public string[]             weaponSlotNames;
        // All live world item pickups at time of snapshot
        public List<WorldItemState> worldItems;
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
    private string lastValidJoinCode = "";

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
        if (!Application.isPlaying || NetworkManager.Singleton == null || NetworkManager.Singleton.ShutdownInProgress) return;

        Debug.Log($"[RelayManager] Client disconnected callback: clientId={clientId}, IsServer={NetworkManager.Singleton.IsServer}");
        
        // If we are a client in an active lobby and our host connection drops, start Host Migration!
        if (!NetworkManager.Singleton.IsServer && !IsMigrating && currentLobby != null)
        {
            Debug.Log("[RelayManager] Host connection lost! Triggering Host Migration...");
            StartHostMigration();
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
                foreach (var lobby in queryResponse.Results)
                {
                    bool started = false;
                    if (lobby.Data != null && lobby.Data.ContainsKey("MatchStarted"))
                    {
                        started = lobby.Data["MatchStarted"].Value == "true";
                    }

                    if (!started && lobby.Data != null && lobby.Data.ContainsKey("JoinCode"))
                    {
                        string joinCode = lobby.Data["JoinCode"].Value;
                        if (!string.IsNullOrEmpty(joinCode))
                        {
                            try
                            {
                                Debug.Log($"[RelayManager] Found active lobby '{lobby.Name}'. Joining UGS Lobby & Relay ({joinCode})...");
                                Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
                                currentLobby = joinedLobby;

                                bool clientStarted = await StartClientWithRelay(joinCode);
                                if (clientStarted)
                                {
                                    Debug.Log($"[RelayManager] Successfully quick-joined match '{lobby.Name}'!");
                                    return true;
                                }
                                else
                                {
                                    Debug.LogWarning($"[RelayManager] Join Code '{joinCode}' for lobby '{lobby.Name}' failed/expired. Trying next lobby...");
                                    currentLobby = null;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"[RelayManager] Failed to join candidate lobby '{lobby.Name}': {ex.Message}. Trying next...");
                                currentLobby = null;
                            }
                        }
                    }
                }
            }

            // No suitable active lobbies found (or all candidates were stale): host a new match
            Debug.Log("[RelayManager] No working open lobbies found. Hosting a new match...");
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
    /// Creates a private Relay host room and registers a matching UGS Lobby for room code joining & migration.
    /// </summary>
    public async Task<string> StartPrivateHostWithRelay()
    {
        string joinCode = await StartHostWithRelay();
        if (!string.IsNullOrEmpty(joinCode))
        {
            try
            {
                string lobbyName = $"PrivateLobby_{Random.Range(1000, 9999)}";
                int maxPlayers = maxConnections + 1;
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
                Debug.Log($"[RelayManager] Created UGS Lobby '{lobbyName}' (ID: {lobby.Id}) for room code: {joinCode}");
                if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = StartCoroutine(LobbyHeartbeatRoutine(lobby.Id, 15f));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RelayManager] Could not register UGS Lobby for private host: {ex.Message}");
            }
        }
        return joinCode;
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

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[RelayManager] Shutting down existing active Netcode connection before starting host...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(200);
            }

            DestroyUnspawnedPreviewPlayers();

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[RelayManager] NGO Host started successfully over Unity Relay.");
                CurrentJoinCode = joinCode;
                lastValidJoinCode = joinCode;

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
                        Data = new Dictionary<string, DataObject>
                        {
                            { "MatchStarted", new DataObject(DataObject.VisibilityOptions.Public, "true") }
                        }
                    });
                    Debug.Log("[RelayManager] Lobby marked MatchStarted = true successfully.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RelayManager] Failed to update lobby on match start: {e.Message}");
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

            // Bind UGS Lobby on joining client so Host Migration & Room tracking function properly
            if (currentLobby == null)
            {
                try
                {
                    QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
                    {
                        Count = 20
                    };
                    QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                    if (queryResponse != null && queryResponse.Results != null)
                    {
                        foreach (var l in queryResponse.Results)
                        {
                            if (l != null && l.Data != null && l.Data.ContainsKey("JoinCode") && l.Data["JoinCode"].Value == joinCode)
                            {
                                currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(l.Id);
                                Debug.Log($"[RelayManager] Joined matching UGS Lobby '{currentLobby.Name}' (ID: {currentLobby.Id}) for room code {joinCode}");
                                break;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[RelayManager] Could not bind UGS Lobby on client join: {ex.Message}");
                }
            }

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

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[RelayManager] Shutting down existing active Netcode connection before starting client...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(200);
            }

            DestroyUnspawnedPreviewPlayers();

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[RelayManager] NGO Client connecting over Unity Relay...");
                CurrentJoinCode = joinCode;
                lastValidJoinCode = joinCode;
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

    public static void DestroyUnspawnedPreviewPlayers()
    {
        PlayerController[] pcs = Object.FindObjectsOfType<PlayerController>();
        foreach (var pc in pcs)
        {
            if (pc == null) continue;
            var netObj = pc.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
            {
                Debug.Log($"[RelayManager] Destroying unspawned preview Player object: {pc.gameObject.name}");
                Object.Destroy(pc.gameObject);
            }
        }

        GameObject[] scenePlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in scenePlayers)
        {
            if (p == null) continue;
            var netObj = p.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
            {
                Debug.Log($"[RelayManager] Destroying unspawned preview player gameobject: {p.name}");
                Object.Destroy(p);
            }
        }
    }

    /// <summary>
    /// Shuts down the current active session gracefully.
    /// </summary>
    public async void Disconnect()
    {
        await LeaveMatchGracefully();
    }

    /// <summary>
    /// Gracefully leaves the match. If the player is the host, transfers host status to next player so the UGS Lobby stays alive for host migration.
    /// </summary>
    public async Task LeaveMatchGracefully()
    {
        if (currentLobby != null)
        {
            try
            {
                string myId = AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn 
                    ? AuthenticationService.Instance.PlayerId 
                    : "";

                if (!string.IsNullOrEmpty(myId) && !string.IsNullOrEmpty(currentLobby.Id))
                {
                    // Fetch fresh lobby data from UGS to get exact current player list
                    try
                    {
                        Lobby freshLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                        if (freshLobby != null) currentLobby = freshLobby;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[RelayManager] Could not refresh lobby state before leaving: {ex.Message}");
                    }

                    Player nextPlayer = currentLobby.Players != null 
                        ? currentLobby.Players.FirstOrDefault(p => p.Id != myId) 
                        : null;

                    bool isHost = currentLobby.HostId == myId;

                    if (isHost)
                    {
                        if (nextPlayer != null)
                        {
                            Debug.Log($"[RelayManager] Host leaving active match: transferring HostId to next player '{nextPlayer.Id}'...");
                            try
                            {
                                await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                                {
                                    HostId = nextPlayer.Id,
                                    Data = new Dictionary<string, DataObject>
                                    {
                                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, "") }
                                    }
                                });
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"[RelayManager] Failed to transfer HostId before leave: {ex.Message}");
                            }

                            Debug.Log($"[RelayManager] Removing host '{myId}' from Lobby '{currentLobby.Id}'...");
                            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, myId);
                        }
                        else
                        {
                            Debug.Log($"[RelayManager] Host alone: Deleting empty Lobby '{currentLobby.Id}'...");
                            await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                        }
                    }
                    else
                    {
                        Debug.Log($"[RelayManager] Client '{myId}' leaving Lobby '{currentLobby.Id}'...");
                        await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, myId);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RelayManager] Failed to update Lobby on leave: {e.Message}");
            }
            currentLobby = null;
        }

        CurrentJoinCode = "";

        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[RelayManager] Shut down active Netcode connection.");
        }
    }

    /// <summary>
    /// Saves the current local player stats (position, health, weapons, bag items) prior to host migration.
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
            var pController = localPlayerObj.GetComponent<PlayerController>();
            if (pController != null)
            {
                SaveCurrentPlayerState(pController);
                return;
            }
        }

        // Fallback: If player object was destroyed right before disconnect, read last persisted device state from PlayerPrefs
        if (!HasSnapshot && PlayerPrefs.HasKey("Snapshot_PosX"))
        {
            float px = PlayerPrefs.GetFloat("Snapshot_PosX", 0f);
            float py = PlayerPrefs.GetFloat("Snapshot_PosY", 0f);
            float pz = PlayerPrefs.GetFloat("Snapshot_PosZ", 0f);
            int hp = PlayerPrefs.GetInt("Snapshot_Health", 100);
            bool ghost = PlayerPrefs.GetInt("Snapshot_IsGhost", 0) == 1;
            int slot = PlayerPrefs.GetInt("Snapshot_Slot", 0);

            LastPlayerSnapshot = new PlayerMigrationSnapshot
            {
                position = new Vector3(px, py, pz),
                rotation = Quaternion.identity,
                health = hp,
                isGhost = ghost,
                currentWeaponIndex = slot
            };
            HasSnapshot = true;
            Debug.Log($"[RelayManager] Restored local player snapshot from PlayerPrefs storage at ({px}, {py}, {pz}), IsGhost: {ghost}");
        }
    }

    /// <summary>
    /// Continuously updates player state in memory and device local storage (PlayerPrefs) while playing.
    /// </summary>
    public static void SaveCurrentPlayerState(PlayerController player)
    {
        if (player == null) return;

        Vector3 pos    = player.transform.position;
        Quaternion rot = player.transform.rotation;
        int hp         = PlayerHealth.Instance != null ? PlayerHealth.Instance.GetCurrentHealth() : 100;
        bool ghost     = player.IsGhost;
        int slot       = WeaponController.Instance != null ? WeaponController.Instance.GetCurrentSlot() : 0;

        Dictionary<AmmoType, int>    ammo     = null;
        Dictionary<GrenadeType, int> grenades = null;
        int medikits = 0;
        int shakes   = 0;
        int scopes   = 0;

        if (BagManager.Instance != null)
        {
            ammo     = new Dictionary<AmmoType, int>(BagManager.Instance.ammoInventory);
            grenades = new Dictionary<GrenadeType, int>(BagManager.Instance.grenadeInventory);
            medikits = BagManager.Instance.medikitCount;
            shakes   = BagManager.Instance.proteinShakeCount;
            scopes   = BagManager.Instance.scopeCount;
        }

        // ── Snapshot equipped weapons (slot 0 and slot 1 prefab names) ──────────
        string[] weaponNames = new string[2];
        if (WeaponController.Instance != null)
        {
            string[] names = WeaponController.Instance.GetEquippedWeaponNames();
            if (names != null)
                System.Array.Copy(names, weaponNames, Mathf.Min(names.Length, 2));
        }

        // ── Snapshot all live world item pickups ─────────────────────────────────
        var worldItems = new List<WorldItemState>();
        foreach (var pickup in GameObject.FindObjectsOfType<ItemPickup>())
        {
            if (pickup == null) continue;
            string itemName = pickup.itemData != null
                ? pickup.itemData.itemName
                : pickup.gameObject.name.Replace("(Clone)", "").Trim();

            worldItems.Add(new WorldItemState
            {
                position   = pickup.transform.position,
                itemName   = itemName,
                amount     = pickup.amount,
                wasDropped = pickup.wasDropped
            });
        }

        // ── Snapshot facing direction ───────────────────────────────────────
        bool facingRight = false;
        var assembler = player.GetComponentInChildren<CharacterAssembler>();
        if (assembler != null) facingRight = assembler.IsFacingRight();

        LastPlayerSnapshot = new PlayerMigrationSnapshot
        {
            position           = pos,
            rotation           = rot,
            health             = hp,
            isGhost            = ghost,
            currentWeaponIndex = slot,
            ammoCounts         = ammo,
            grenadeCounts      = grenades,
            medikitCount       = medikits,
            proteinShakeCount  = shakes,
            scopeCount         = scopes,
            weaponSlotNames    = weaponNames,
            worldItems         = worldItems,
            facingRight        = facingRight
        };
        HasSnapshot = true;

        // Persist core state to PlayerPrefs (position, health, ghost, slot) for resilience
        PlayerPrefs.SetFloat("Snapshot_PosX", pos.x);
        PlayerPrefs.SetFloat("Snapshot_PosY", pos.y);
        PlayerPrefs.SetFloat("Snapshot_PosZ", pos.z);
        PlayerPrefs.SetInt("Snapshot_Health", hp);
        PlayerPrefs.SetInt("Snapshot_IsGhost", ghost ? 1 : 0);
        PlayerPrefs.SetInt("Snapshot_Slot", slot);
        PlayerPrefs.Save();
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

        string oldJoinCode = !string.IsNullOrEmpty(CurrentJoinCode) ? CurrentJoinCode : lastValidJoinCode;

        // Recovery check: If currentLobby is null or broken, attempt to recover by querying active lobbies
        if (currentLobby == null && !string.IsNullOrEmpty(oldJoinCode))
        {
            OnMigrationStatusChanged?.Invoke("Locating room for Host Migration...");
            var queryTask = LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 20
            });
            while (!queryTask.IsCompleted) yield return null;

            if (queryTask.Status == TaskStatus.RanToCompletion && queryTask.Result != null && queryTask.Result.Results != null)
            {
                foreach (var l in queryTask.Result.Results)
                {
                    if (l != null && l.Data != null && l.Data.ContainsKey("JoinCode") && (l.Data["JoinCode"].Value == oldJoinCode || string.IsNullOrEmpty(l.Data["JoinCode"].Value)))
                    {
                        string targetId = l.Id;
                        var joinTask = LobbyService.Instance.JoinLobbyByIdAsync(targetId);
                        while (!joinTask.IsCompleted) yield return null;

                        if (joinTask.Status == TaskStatus.RanToCompletion && joinTask.Result != null)
                        {
                            currentLobby = joinTask.Result;
                            Debug.Log($"[HostMigration] Successfully recovered active lobby '{currentLobby.Name}' (ID: {currentLobby.Id})!");
                            break;
                        }
                    }
                }
            }
        }

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

        string myPlayerId = AuthenticationService.Instance != null ? AuthenticationService.Instance.PlayerId : "";
        string updatedHostId = "";
        Lobby updatedLobby = null;

        int retries = 0;
        bool isNewHost = false;

        while (retries < 12)
        {
            retries++;
            OnMigrationStatusChanged?.Invoke($"Connecting to new Host ({retries}/12)...");

            Lobby tempLobby = null;
            Task<Lobby> getLobbyTask = null;
            try
            {
                getLobbyTask = LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HostMigration] GetLobbyAsync call warning: {ex.Message}");
            }

            if (getLobbyTask != null)
            {
                while (!getLobbyTask.IsCompleted) yield return null;
                if (getLobbyTask.Status == TaskStatus.RanToCompletion && getLobbyTask.Result != null)
                {
                    tempLobby = getLobbyTask.Result;
                }
            }

            if (tempLobby != null)
            {
                updatedLobby = tempLobby;
                currentLobby = updatedLobby;
                updatedHostId = updatedLobby.HostId;

                Debug.Log($"[HostMigration] Lobby Host ID: {updatedHostId}, My Player ID: {myPlayerId}");

                // Scenario 1: UGS promoted us to Host
                if (updatedHostId == myPlayerId)
                {
                    isNewHost = true;
                    break;
                }

                // Scenario 2: New Host has already posted a new JoinCode
                if (updatedLobby.Data != null && updatedLobby.Data.ContainsKey("JoinCode"))
                {
                    string latestJoinCode = updatedLobby.Data["JoinCode"].Value;
                    if (!string.IsNullOrEmpty(latestJoinCode) && latestJoinCode != oldJoinCode)
                    {
                        Debug.Log($"[HostMigration] New Join Code detected from Host: {latestJoinCode}");
                        break;
                    }
                }

                // Scenario 3: Un-graceful host drop (crash/network cut). If HostId hasn't changed after 2 retries (~2s), oldest remaining player claims Host!
                if (retries >= 2 && updatedLobby.Players != null && updatedLobby.Players.Count > 0)
                {
                    string firstPlayerId = updatedLobby.Players[0].Id;
                    if (firstPlayerId == myPlayerId)
                    {
                        Debug.Log("[HostMigration] Claiming Host leadership after host disconnect timeout...");
                        var claimTask = LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
                        {
                            HostId = myPlayerId,
                            Data = new Dictionary<string, DataObject>
                            {
                                { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, "") }
                            }
                        });
                        while (!claimTask.IsCompleted) yield return null;
                        if (claimTask.Status == TaskStatus.RanToCompletion)
                        {
                            isNewHost = true;
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
                lastValidJoinCode = newJoinCode;
                Debug.Log("[HostMigration] Started NGO Host successfully!");

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedDuringMigration;
                SpawnHostPlayerForMigration();
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

        float timeout = 10.0f;
        float elapsed = 0f;
        while ((NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // ── Wait one extra frame so all MonoBehaviour Start() methods run first ──
        // WeaponController.Start() calls ClearAttachPointChildren() and
        // BagManager.Start() calls ClearInventory() — both on the first frame.
        // RestorePlayerFromSnapshot must run AFTER those clears, not before.
        yield return null;

        // Only the host restores world pickups — using the snapshot so exact positions and
        // item types are preserved, rather than randomly re-spawning new items.
        if (NetworkManager.Singleton.IsServer && GameManager.Instance != null)
        {
            if (HasSnapshot && LastPlayerSnapshot.worldItems != null && LastPlayerSnapshot.worldItems.Count > 0)
            {
                GameManager.Instance.RestoreWorldItemsFromSnapshot(LastPlayerSnapshot.worldItems);
            }
            else
            {
                // Fallback: no world item snapshot available — spawn fresh items
                GameManager.Instance.SpawnItemsAroundPlayer();
            }
        }

        // Notify GameManager to restore snapshot & set camera target & spawn player object if needed
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

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedDuringMigration;
        }

        OnMigrationStatusChanged?.Invoke("Game Resumed!");
        yield return new WaitForSecondsRealtime(0.5f);

        IsMigrating = false;
        OnMigrationStateChanged?.Invoke(false);
        Debug.Log("[HostMigration] Host migration successfully completed!");
    }

    private GameObject GetPlayerPrefab()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null && NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
        {
            return NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        }
        GameObject loaded = Resources.Load<GameObject>("Player");
        if (loaded == null) loaded = Resources.Load<GameObject>("Prefabs/Player");
        return loaded;
    }

    private void SpawnHostPlayerForMigration()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            Debug.Log("[HostMigration] Host player object already exists!");
            return;
        }

        GameObject playerPrefab = GetPlayerPrefab();
        if (playerPrefab != null)
        {
            Vector3 spawnPos = HasSnapshot ? LastPlayerSnapshot.position : Vector3.zero;
            Quaternion spawnRot = HasSnapshot ? LastPlayerSnapshot.rotation : Quaternion.identity;

            GameObject playerObj = Instantiate(playerPrefab, spawnPos, spawnRot);
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId, true);
                Debug.Log($"[HostMigration] Server manually spawned Host player object ({netObj.NetworkObjectId}) at {spawnPos}");
            }
        }
        else
        {
            Debug.LogError("[HostMigration] Failed to spawn host player: PlayerPrefab not found in NetworkConfig or Resources!");
        }
    }

    private void OnClientConnectedDuringMigration(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var clientData) && clientData.PlayerObject != null)
        {
            Debug.Log($"[HostMigration] Client {clientId} player object already exists.");
            return;
        }

        GameObject playerPrefab = GetPlayerPrefab();
        if (playerPrefab != null)
        {
            Vector3 spawnPos = Vector3.zero;
            GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnWithOwnership(clientId, true);
                Debug.Log($"[HostMigration] Server spawned player object for reconnected client {clientId}");
            }
        }
    }
}
