using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public enum PlayerRole
{
    Hostage,
    Thief
}

public class MatchRoleManager : NetworkBehaviour
{
    public static MatchRoleManager Instance { get; private set; }

    [Header("Role Ratios")]
    [Tooltip("Target ratio: 1 Thief per 3 Hostages (approx 25% Thieves, 75% Hostages)")]
    public float thiefRatio = 0.25f;

    [Header("Ground Floor Hostage Spawn Settings")]
    public Vector3 groundFloorCenter = new Vector3(0f, -6f, 0f);
    public float groundFloorSpawnRadius = 3.5f;

    [Header("Map Thief Spawn Settings")]
    public float mapThiefSpawnRadius = 12f;

    // Synced key collection count for hostages objective
    public NetworkVariable<int> KeysCollected = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Random index (1 to 7) of the key that actually opens the Main Gate
    public NetworkVariable<int> MasterGateKeyIndex = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Synchronized state whether Main Gate is unlocked
    public NetworkVariable<bool> IsGateUnlocked = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Network variables tracking the Safe Key & Treasure quest lifecycle
    public NetworkVariable<ulong> SafeKeyHolderClientId = new NetworkVariable<ulong>(
        999999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Network variable tracking the Main Gate Key Holder (Thief / Tagger)
    public NetworkVariable<ulong> GateKeyHolderClientId = new NetworkVariable<ulong>(
        999999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> SafeKeyCollectedByThief = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsSafeOpened = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> TreasureStolen = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    // Synchronized match outcome state
    public NetworkVariable<bool> IsMatchEnded = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<PlayerRole> WinningRole = new NetworkVariable<PlayerRole>(
        PlayerRole.Hostage, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private HashSet<ulong> escapedHostageClientIds = new HashSet<ulong>();

    [Header("Assignable Map Locations (Drag & Drop in Inspector)")]
    public Transform groundHallTransform;      // Ground Hall / Ground Floor
    public Transform mainGateTransform;        // Main Gate
    public Transform liftTransform;            // Lift
    public Transform[] roomTransforms;         // Multiple Rooms
    public Transform[] floorTransforms;        // Multiple Floors
    public Transform[] stairTransforms;        // Multiple Stairs

    public enum DebugRoleOverride { AutoRandom, ForceHostage, ForceThief }

    [Header("Testing & Debug Role Override")]
    [Tooltip("Force singleplayer role for testing in Unity Editor!")]
    public DebugRoleOverride debugSingleplayerRole = DebugRoleOverride.AutoRandom;

    // Dictionary tracking assigned role per ClientId (Server side)
    private Dictionary<ulong, PlayerRole> assignedRoles = new Dictionary<ulong, PlayerRole>();

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

    private void Update()
    {
#if UNITY_EDITOR
        // Press 'T' key in Editor to dynamically toggle role for testing!
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleLocalPlayerRoleForTesting();
        }
#endif
    }

    public void ToggleLocalPlayerRoleForTesting()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && p.IsLocal)
            {
                PlayerRole newRole = (p.playerRole.Value == PlayerRole.Hostage) ? PlayerRole.Thief : PlayerRole.Hostage;
                assignedRoles[p.OwnerClientId] = newRole;
                if (IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                {
                    p.playerRole.Value = newRole;
                }
                Vector3 newPos = GetSpawnPositionForRole(newRole);
                p.transform.position = newPos;

                string roleStr = (newRole == PlayerRole.Thief) ? "<color=red>[THIEF]</color>" : "<color=cyan>[HOSTAGE]</color>";
                Debug.Log($"[MatchRoleManager] Toggled local player role to: {newRole}");

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification($"🎮 Toggled Role for Testing: {roleStr}");
                    HUDManager.Instance.UpdateRoleBadgeDisplay();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Resets all match quest network variables, keys, safe states, and roles for a new match.
    /// </summary>
    public void ResetMatchState()
    {
        if (IsServerAuthority())
        {
            KeysCollected.Value = 0;
            MasterGateKeyIndex.Value = Random.Range(1, 8); // Pick 1 of 7 keys as Master Key
            IsGateUnlocked.Value = false;
            SafeKeyHolderClientId.Value = 999999;
            GateKeyHolderClientId.Value = 999999;
            SafeKeyCollectedByThief.Value = false;
            IsSafeOpened.Value = false;
            TreasureStolen.Value = false;
            IsMatchEnded.Value = false;
            escapedHostageClientIds.Clear();
            assignedRoles.Clear();
            Debug.Log($"[MatchRoleManager] Successfully reset match quest state. MasterGateKeyIndex={MasterGateKeyIndex.Value}");
        }
    }

    public void EnsureSafeKeyHolderAssigned()
    {
        if (!IsServerAuthority()) return;

        List<ulong> hostageClientIds = new List<ulong>();
        foreach (var pair in assignedRoles)
        {
            if (pair.Value == PlayerRole.Hostage) hostageClientIds.Add(pair.Key);
        }

        // If current holder is valid and still assigned as hostage, keep them
        if (SafeKeyHolderClientId.Value != 999999 && hostageClientIds.Contains(SafeKeyHolderClientId.Value))
        {
            return;
        }

        if (hostageClientIds.Count > 0)
        {
            ulong chosenSafeKeyHolder = hostageClientIds[Random.Range(0, hostageClientIds.Count)];
            SafeKeyHolderClientId.Value = chosenSafeKeyHolder;
            Debug.Log($"[MatchRoleManager] 🔑 Assigned Safe Key to Hostage ClientId: {chosenSafeKeyHolder} (out of {hostageClientIds.Count} hostages)");
            if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NotifySafeKeyHolderClientRpc(chosenSafeKeyHolder);
            }
            else if (chosenSafeKeyHolder == 0 && BagManager.Instance != null)
            {
                BagManager.Instance.AddSafeKey();
            }
        }
    }

    private bool IsServerAuthority()
    {
        return NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && !RelayNetworkManager.IsMigrating)
        {
            ResetMatchState();
            AssignRolesForConnectedPlayers();
        }
    }

    public void SetRoleForClient(ulong clientId, PlayerRole role)
    {
        assignedRoles[clientId] = role;
        Debug.Log($"[MatchRoleManager] Authoritatively registered ClientId {clientId} as {role}");
        EnsureSafeKeyHolderAssigned();
    }

    /// <summary>
    /// Calculates Thief vs Hostage roles with 1:3 ratio for connected players.
    /// Singleplayer testing respects debugSingleplayerRole override or rolls 50/50.
    /// </summary>
    public void AssignRolesForConnectedPlayers()
    {
        // Reset match quest network variables prior to assigning roles
        ResetMatchState();

        // If Netcode server is running, only server assigns. If offline/singleplayer, allow local assignment!
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
        {
            return;
        }

        List<ulong> clientIds = new List<ulong>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                clientIds.Add(clientId);
            }
        }
        else
        {
            clientIds.Add(0); // Singleplayer / Local fallback
        }

        foreach (var clientId in clientIds)
        {
            PlayerRole role = GetRoleForClient(clientId);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                if (client != null && client.PlayerObject != null)
                {
                    var pc = client.PlayerObject.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.playerRole.Value = role;
                        Debug.Log($"[MatchRoleManager] Synced playerRole.Value for ClientId {clientId} -> {role}");
                    }
                }
            }
        }

        // Collect all Hostage and Thief ClientIds
        List<ulong> hostageClientIds = new List<ulong>();
        List<ulong> thiefClientIds   = new List<ulong>();

        foreach (var pair in assignedRoles)
        {
            if (pair.Value == PlayerRole.Hostage) hostageClientIds.Add(pair.Key);
            else if (pair.Value == PlayerRole.Thief) thiefClientIds.Add(pair.Key);
        }

        // 1. Exactly 1 Hostage gets the Safe Key (which Thieves want to take to open the safe)
        if (hostageClientIds.Count > 0)
        {
            ulong chosenSafeKeyHolder = hostageClientIds[Random.Range(0, hostageClientIds.Count)];
            SafeKeyHolderClientId.Value = chosenSafeKeyHolder;
            Debug.Log($"[MatchRoleManager] 🔑 Assigned Safe Key to Hostage ClientId: {chosenSafeKeyHolder} (out of {hostageClientIds.Count} hostages)");
        }
        else
        {
            SafeKeyHolderClientId.Value = 999999;
        }

        // 2. Exactly 1 Thief (Tagger) gets the Main Gate Key (which Hostages want to take to escape)
        if (thiefClientIds.Count > 0)
        {
            ulong chosenGateKeyHolder = thiefClientIds[Random.Range(0, thiefClientIds.Count)];
            GateKeyHolderClientId.Value = chosenGateKeyHolder;
            Debug.Log($"[MatchRoleManager] 🔑 Assigned Main Gate Key to Thief (Tagger) ClientId: {chosenGateKeyHolder} (out of {thiefClientIds.Count} thieves)");
        }
        else
        {
            GateKeyHolderClientId.Value = 999999;
        }
    }

    public bool IsSafeKeyHolder(ulong clientId)
    {
        return SafeKeyHolderClientId.Value == clientId;
    }

    public bool IsGateKeyHolder(ulong clientId)
    {
        return GateKeyHolderClientId.Value == clientId;
    }

    public void DropSafeKey(Vector3 dropPosition)
    {
        if (SafeKeyCollectedByThief.Value) return;

        Debug.Log($"[MatchRoleManager] DropSafeKey called at {dropPosition}. NetworkManager.IsListening={NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening}, IsServer={IsServer}");

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SpawnSafeKeyLocal(dropPosition);
            return;
        }

        if (IsServer)
        {
            SpawnSafeKeyClientRpc(dropPosition);
        }
        else
        {
            RequestDropSafeKeyServerRpc(dropPosition);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropSafeKeyServerRpc(Vector3 dropPosition)
    {
        if (!SafeKeyCollectedByThief.Value)
        {
            SpawnSafeKeyClientRpc(dropPosition);
        }
    }

    [ClientRpc]
    public void SpawnSafeKeyClientRpc(Vector3 dropPosition)
    {
        SpawnSafeKeyLocal(dropPosition);
    }

    private void SpawnSafeKeyLocal(Vector3 dropPosition)
    {
        if (FindObjectOfType<SafeKeyItemPickup>() != null) return;

        GameObject keyGO = new GameObject("Dropped_SafeKey", typeof(SafeKeyItemPickup));
        keyGO.transform.position = dropPosition;

        Debug.Log($"[MatchRoleManager] 🔑 Safe Key dropped at {dropPosition}!");

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("<color=yellow>🔑 SAFE KEY DROPPED! The Hostage holding the Safe Key was eliminated!</color>");
        }
    }

    public void HandleSafeKeyHolderDeath(Vector3 dropPosition)
    {
        DropSafeKey(dropPosition);
    }

    public void HandleGateKeyHolderDeath(Vector3 dropPosition)
    {
        Debug.Log($"[MatchRoleManager] Main Gate Key Holder (Thief/Tagger) died at {dropPosition}! Spawning KeyItemPickup for Hostages...");

        GameObject keyGO = new GameObject("Dropped_GateKey", typeof(KeyItemPickup));
        keyGO.transform.position = dropPosition;

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("<color=yellow>🔑 MAIN GATE KEY DROPPED! Tagger holding Main Gate Key was eliminated!</color>");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CollectSafeKeyServerRpc()
    {
        SafeKeyCollectedByThief.Value = true;
        Debug.Log("[MatchRoleManager] SafeKeyCollectedByThief set to TRUE via ServerRpc!");
        DespawnSafeKeyClientRpc();
    }

    [ClientRpc]
    public void DespawnSafeKeyClientRpc()
    {
        SafeKeyItemPickup[] all = FindObjectsOfType<SafeKeyItemPickup>();
        foreach (var k in all)
        {
            if (k != null) Destroy(k.gameObject);
        }
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("<color=gold>🔑 SAFE KEY COLLECTED! Locate and unlock the Safe to steal the Treasure!</color>");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void UnlockMainGateServerRpc()
    {
        IsGateUnlocked.Value = true;
        Debug.Log("[MatchRoleManager] Main Gate unlocked via ServerRpc!");
        UnlockMainGateClientRpc();
    }

    [ClientRpc]
    public void UnlockMainGateClientRpc()
    {
        if (MainGateController.Instance != null)
        {
            MainGateController.Instance.SetGateUnlocked();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnKeyServerRpc(int keyIndex)
    {
        DespawnKeyClientRpc(keyIndex);
    }

    [ClientRpc]
    public void DespawnKeyClientRpc(int keyIndex)
    {
        KeyItemPickup[] all = FindObjectsOfType<KeyItemPickup>();
        foreach (var k in all)
        {
            if (k != null && k.keyIndex == keyIndex)
            {
                Destroy(k.gameObject);
            }
        }
    }

    [ClientRpc]
    private void NotifySafeKeyHolderClientRpc(ulong holderClientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == holderClientId)
        {
            if (BagManager.Instance != null) BagManager.Instance.AddSafeKey();
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("<color=yellow>🔑 YOU ARE CARRYING THE SAFE KEY! Stay alive - if you die, Thieves will get it!</color>");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void OpenSafeServerRpc()
    {
        IsSafeOpened.Value = true;
        Debug.Log("[MatchRoleManager] IsSafeOpened set to TRUE via ServerRpc!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void StealTreasureServerRpc()
    {
        TreasureStolen.Value = true;
        Debug.Log("[MatchRoleManager] TreasureStolen set to TRUE via ServerRpc!");
    }

    public PlayerRole GetRoleForClient(ulong clientId)
    {
        if (assignedRoles.TryGetValue(clientId, out PlayerRole existingRole))
        {
            return existingRole;
        }

        // Calculate count of existing thieves vs hostages
        int currentThieves = 0;
        int currentHostages = 0;
        foreach (var pair in assignedRoles)
        {
            if (pair.Value == PlayerRole.Thief) currentThieves++;
            else if (pair.Value == PlayerRole.Hostage) currentHostages++;
        }

        PlayerRole assigned;
        if (currentThieves == 0 && currentHostages > 0)
        {
            // At least 1 Hostage already exists -> Must assign Thief!
            assigned = PlayerRole.Thief;
        }
        else if (currentHostages == 0 && currentThieves > 0)
        {
            // At least 1 Thief already exists -> Must assign Hostage!
            assigned = PlayerRole.Hostage;
        }
        else
        {
            // Singleplayer override check
            if (assignedRoles.Count == 0)
            {
                if (debugSingleplayerRole == DebugRoleOverride.ForceThief) return PlayerRole.Thief;
                if (debugSingleplayerRole == DebugRoleOverride.ForceHostage) return PlayerRole.Hostage;
            }

            assigned = (currentThieves <= currentHostages) ? PlayerRole.Thief : PlayerRole.Hostage;
        }

        assignedRoles[clientId] = assigned;
        Debug.Log($"[MatchRoleManager] Assigned role for ClientId {clientId} -> {assigned} (Thieves: {currentThieves + (assigned == PlayerRole.Thief ? 1 : 0)}, Hostages: {currentHostages + (assigned == PlayerRole.Hostage ? 1 : 0)})");
        return assigned;
    }

    public void SyncLocationsFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            if (groundHallTransform == null && GameManager.Instance.groundHallTransform != null)
                groundHallTransform = GameManager.Instance.groundHallTransform;
            if (mainGateTransform == null && GameManager.Instance.mainGateTransform != null)
                mainGateTransform = GameManager.Instance.mainGateTransform;
            if (liftTransform == null && GameManager.Instance.liftTransform != null)
                liftTransform = GameManager.Instance.liftTransform;
            if ((roomTransforms == null || roomTransforms.Length == 0) && GameManager.Instance.roomTransforms != null && GameManager.Instance.roomTransforms.Length > 0)
                roomTransforms = GameManager.Instance.roomTransforms;
            if ((floorTransforms == null || floorTransforms.Length == 0) && GameManager.Instance.floorTransforms != null && GameManager.Instance.floorTransforms.Length > 0)
                floorTransforms = GameManager.Instance.floorTransforms;
            if ((stairTransforms == null || stairTransforms.Length == 0) && GameManager.Instance.stairTransforms != null && GameManager.Instance.stairTransforms.Length > 0)
                stairTransforms = GameManager.Instance.stairTransforms;
        }

        // Auto-find GroundHallArea in scene if groundHallTransform is still null
        if (groundHallTransform == null)
        {
            GroundHallArea hallArea = FindObjectOfType<GroundHallArea>();
            if (hallArea != null)
            {
                groundHallTransform = hallArea.transform;
                groundFloorCenter = hallArea.transform.position;
            }
        }
    }

    /// <summary>
    /// Returns a spawn position based on player role using WalkableFloorZone areas.
    /// Hostages  → random valid point inside a ground floor zone  (isGroundFloor = true)
    /// Thieves   → random valid point inside an upper floor zone   (isGroundFloor = false)
    /// Falls back to the old radius-circle method if no matching zones are found.
    /// </summary>
    public Vector3 GetSpawnPositionForRole(PlayerRole role, int indexOffset = 0)
    {
        SyncLocationsFromGameManager();

        // Gather all WalkableFloorZone in scene
        WalkableFloorZone[] allZones = FindObjectsOfType<WalkableFloorZone>();

        bool wantGroundFloor = (role == PlayerRole.Hostage);

        // Filter to matching zones
        System.Collections.Generic.List<WalkableFloorZone> matchedZones =
            new System.Collections.Generic.List<WalkableFloorZone>();

        foreach (var z in allZones)
        {
            if (z != null && z.zoneCollider != null && z.isGroundFloor == wantGroundFloor)
                matchedZones.Add(z);
        }

        // Try zone-based spawn
        if (matchedZones.Count > 0)
        {
            Vector3 pos;
            if (TryFindValidPointInZones(matchedZones, out pos))
            {
                Debug.Log($"[MatchRoleManager] Spawning {role} at zone-based position {pos}");
                return pos;
            }
        }

        // ── Fallback to old radius-circle method ─────────────────────────────
        Debug.LogWarning($"[MatchRoleManager] No WalkableFloorZone found for role '{role}' " +
                         "— falling back to radius-circle spawn.");

        if (role == PlayerRole.Hostage)
        {
            Vector3 center = (groundHallTransform != null) ? groundHallTransform.position : groundFloorCenter;
            Vector2 circle = Random.insideUnitCircle * groundFloorSpawnRadius;
            Vector3 fallback = center + new Vector3(circle.x, circle.y, 0f);
            return PlayerController.GetRandomNonColliderSpawnPosition(fallback, 2.5f);
        }
        else
        {
            Vector3 center = Vector3.zero;
            if (roomTransforms != null && roomTransforms.Length > 0)
            {
                Transform validRoom = GetRandomRoomTransform();
                if (validRoom != null) center = validRoom.position;
            }
            Vector2 circle = Random.insideUnitCircle * 3.0f;
            Vector3 fallback = center + new Vector3(circle.x, circle.y, 0f);
            return PlayerController.GetRandomNonColliderSpawnPosition(fallback, mapThiefSpawnRadius);
        }
    }

    /// <summary>
    /// Picks a random valid point inside one of the provided WalkableFloorZones.
    /// Uses the same dual-check as FloorItemSpawner:
    ///   1. Point must be inside the zone's collider shape
    ///   2. Point must not overlap any wall/obstacle (layer: Obstacle)
    /// </summary>
    private bool TryFindValidPointInZones(
        System.Collections.Generic.List<WalkableFloorZone> zones,
        out Vector3 result,
        int maxAttempts = 100)
    {
        // Obstacle layer mask — matches physics layer named "Obstacle"
        int obstacleLayer = LayerMask.GetMask("Obstacle", "Wall");

        WalkableFloorZone zone = zones[Random.Range(0, zones.Count)];
        if (zone == null || zone.zoneCollider == null) { result = Vector3.zero; return false; }

        Bounds b = zone.zoneCollider.bounds;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            Vector2 candidate = new Vector2(x, y);

            // Must be inside the exact collider shape
            if (!zone.ContainsPoint(candidate)) continue;

            // Must not be on a wall/obstacle
            if (Physics2D.OverlapCircle(candidate, 0.3f, obstacleLayer) != null) continue;

            result = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    public Transform GetRandomRoomTransform()
    {
        if (roomTransforms != null && roomTransforms.Length > 0)
        {
            List<Transform> validRooms = new List<Transform>();
            foreach (var r in roomTransforms)
            {
                if (r != null) validRooms.Add(r);
            }
            if (validRooms.Count > 0)
            {
                return validRooms[Random.Range(0, validRooms.Count)];
            }
        }
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CollectKeyServerRpc()
    {
        if (KeysCollected.Value < 2)
        {
            KeysCollected.Value++;
            Debug.Log($"[MatchRoleManager] Key collected! Total keys: {KeysCollected.Value}/2");
        }
    }

    public void ReportHostageEscaped(ulong clientId)
    {
        if (IsMatchEnded.Value) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            escapedHostageClientIds.Add(clientId);
            HideEscapedPlayer(clientId);
            CheckIfAllHostagesEscaped();
            return;
        }

        if (IsServer)
        {
            escapedHostageClientIds.Add(clientId);
            HideEscapedPlayerClientRpc(clientId);
            CheckIfAllHostagesEscaped();
        }
        else
        {
            ReportHostageEscapedServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportHostageEscapedServerRpc(ulong clientId)
    {
        if (IsMatchEnded.Value) return;
        escapedHostageClientIds.Add(clientId);
        HideEscapedPlayerClientRpc(clientId);
        CheckIfAllHostagesEscaped();
    }

    [ClientRpc]
    public void HideEscapedPlayerClientRpc(ulong clientId)
    {
        HideEscapedPlayer(clientId);
    }

    private void HideEscapedPlayer(ulong clientId)
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p != null && p.OwnerClientId == clientId)
            {
                if (p.IsLocal)
                {
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.ShowNotification("<color=green>🚪 YOU ESCAPED SAFELY! Waiting for remaining teammates...</color>");
                    }
                }
                p.enabled = false;
                var col = p.GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
                var srs = p.GetComponentsInChildren<SpriteRenderer>();
                foreach (var sr in srs)
                {
                    if (sr != null) sr.enabled = false;
                }
                break;
            }
        }
    }

    public void CheckIfAllHostagesEscaped()
    {
        if (IsMatchEnded.Value) return;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        int aliveUnescapedHostages = 0;
        int totalHostages = 0;

        foreach (var p in allPlayers)
        {
            if (p == null) continue;
            if (p.playerRole.Value == PlayerRole.Hostage)
            {
                totalHostages++;
                var ph = p.GetComponent<PlayerHealth>();
                bool isDead = p.IsGhost || (ph != null && ph.IsDead);
                bool hasEscaped = escapedHostageClientIds.Contains(p.OwnerClientId);
                if (!isDead && !hasEscaped)
                {
                    aliveUnescapedHostages++;
                }
            }
        }

        Debug.Log($"[MatchRoleManager] Hostage escape check: Alive & Unescaped={aliveUnescapedHostages}, Total={totalHostages}, Escaped={escapedHostageClientIds.Count}");

        // If at least one hostage escaped, and all living hostages are out -> HOSTAGES WIN!
        if (escapedHostageClientIds.Count > 0 && aliveUnescapedHostages == 0)
        {
            EndMatchWithWinner(PlayerRole.Hostage);
        }
        else if (totalHostages > 0 && aliveUnescapedHostages == 0 && escapedHostageClientIds.Count == 0)
        {
            // All hostages died without escaping -> THIEVES WIN!
            EndMatchWithWinner(PlayerRole.Thief);
        }
    }

    public void OnPlayerDied()
    {
        if (IsMatchEnded.Value) return;
        if (IsServerAuthority())
        {
            CheckIfAllHostagesEscaped();
        }
    }

    public void ReportThiefEscapedWithTreasure(ulong clientId)
    {
        if (IsMatchEnded.Value) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            EndMatchWithWinner(PlayerRole.Thief);
            return;
        }

        if (IsServer)
        {
            EndMatchWithWinner(PlayerRole.Thief);
        }
        else
        {
            ReportThiefEscapedServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportThiefEscapedServerRpc(ulong clientId)
    {
        if (IsMatchEnded.Value) return;
        EndMatchWithWinner(PlayerRole.Thief);
    }

    public void EndMatchWithWinner(PlayerRole winner)
    {
        if (IsMatchEnded.Value) return;
        if (IsServerAuthority())
        {
            IsMatchEnded.Value = true;
            WinningRole.Value = winner;

            Debug.Log($"[MatchRoleManager] 🏆 MATCH OVER! Winner: {winner}");

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                BroadcastMatchEndedClientRpc(winner);
            }
            else
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowAnimatedWinnerModal(winner);
                }
            }
        }
    }

    [ClientRpc]
    public void BroadcastMatchEndedClientRpc(PlayerRole winner)
    {
        Debug.Log($"[MatchRoleManager] BroadcastMatchEndedClientRpc received. Winner: {winner}");
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowAnimatedWinnerModal(winner);
        }
    }
}

