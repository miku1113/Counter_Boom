using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Floor Item Spawner")]
    [Tooltip("Drag in WalkableFloorZone GameObjects that cover only the walkable floor tiles.")]
    public List<WalkableFloorZone> walkableZones = new List<WalkableFloorZone>();

    [Tooltip("Physics layers for walls / obstacles — spawn will never land here.")]
    public LayerMask obstacleLayerMask;

    [Tooltip("Item prefabs to randomly spawn on the floor.")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    [SerializeField] private int  spawnCount          = 10;
    [SerializeField] private float overlapCheckRadius  = 0.25f;
    [SerializeField] private int  maxAttemptsPerItem   = 100;

    // ── Room Tracking ─────────────────────────────────────────────────────────

    /// <summary>The room the local player is currently inside. Null = world/open area.</summary>
    public static RoomController CurrentRoom  { get; private set; }

    /// <summary>Short string ID of the current room (e.g. "kitchen"). Empty = world.</summary>
    public static string CurrentRoomId => CurrentRoom != null ? CurrentRoom.roomId : string.Empty;

    /// <summary>
    /// Called by DoorController when the player enters or exits a room.
    /// Pass null to indicate the player is back in the open world.
    /// </summary>
    public void SetCurrentRoom(RoomController room)
    {
        CurrentRoom = room;
        string label = room != null ? $"'{room.roomDisplayName}' ({room.roomId})" : "world (no room)";
        Debug.Log($"[GameManager] Local player is now in: {label}");
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        EnsureMatchObjects();

        // Only the server/host should spawn world items
        if (!IsServerAuthority()) return;

        // Auto-discover all WalkableFloorZones in scene (includes both corridor and room zones)
        WalkableFloorZone[] foundZones = FindObjectsOfType<WalkableFloorZone>();
        if (foundZones != null && foundZones.Length > 0)
        {
            walkableZones = new List<WalkableFloorZone>(foundZones);
            Debug.Log($"[GameManager] Registered {walkableZones.Count} WalkableFloorZone(s) (corridors & rooms) for item spawning.");
        }
        else if (walkableZones == null || walkableZones.Count == 0)
        {
            Debug.LogWarning("[GameManager] No WalkableFloorZone found in scene — items won't spawn. " +
                             "Create a GameObject with WalkableFloorZone + Collider2D over the floor or room.");
        }

        SpawnItemsOnFloor();
    }

    [Header("Door & Room Central UI Buttons")]
    [Tooltip("Drag the central Enter UI Canvas Button here in the Inspector.")]
    public UnityEngine.UI.Button enterButton;

    [Tooltip("Drag the central Exit UI Canvas Button here in the Inspector.")]
    public UnityEngine.UI.Button exitButton;

    [Header("Map Location References (Drag & Drop in Inspector)")]
    public Transform groundHallTransform;      // Ground Hall / Ground Floor
    public Transform mainGateTransform;        // Main Gate
    public Transform liftTransform;            // Lift
    public Transform[] roomTransforms;         // Multiple Rooms
    public Transform[] floorTransforms;        // Multiple Floors
    public Transform[] stairTransforms;        // Multiple Stairs

    [Header("Testing & Role Debug Override")]
    [Tooltip("Select ForceThief or ForceHostage to test specific roles during singleplayer testing!")]
    public MatchRoleManager.DebugRoleOverride debugSingleplayerRole = MatchRoleManager.DebugRoleOverride.AutoRandom;

    [Header("Key Objective Settings")]
    [Tooltip("Drag and drop your custom Key Prefab here in the Inspector!")]
    public GameObject customKeyPrefab;

    [Header("Safe Objectives & Manual Placement")]
    [Tooltip("Manually define a list of Safe objects in the scene/prefabs. Only 1 safe from this list will be chosen & active in game!")]
    public List<SafeController> manualSafes = new List<SafeController>();

    [Tooltip("Manually define a list of transform spawn points for the Safe. If manualSafes is empty, 1 safe will be spawned at a random point from this list.")]
    public List<Transform> safeSpawnPoints = new List<Transform>();

    private void EnsureMatchObjects()
    {
        if (MatchRoleManager.Instance == null)
        {
            new GameObject("MatchRoleManager", typeof(MatchRoleManager));
        }

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.debugSingleplayerRole = debugSingleplayerRole;
            if (groundHallTransform != null) MatchRoleManager.Instance.groundHallTransform = groundHallTransform;
            if (mainGateTransform != null) MatchRoleManager.Instance.mainGateTransform = mainGateTransform;
            if (liftTransform != null) MatchRoleManager.Instance.liftTransform = liftTransform;
            if (roomTransforms != null && roomTransforms.Length > 0) MatchRoleManager.Instance.roomTransforms = roomTransforms;
            if (floorTransforms != null && floorTransforms.Length > 0) MatchRoleManager.Instance.floorTransforms = floorTransforms;
            if (stairTransforms != null && stairTransforms.Length > 0) MatchRoleManager.Instance.stairTransforms = stairTransforms;
        }

        if (MainGateController.Instance == null && FindObjectOfType<MainGateController>() == null)
        {
            GameObject gateGO = new GameObject("GroundFloorMainGate", typeof(MainGateController));
            if (mainGateTransform != null) gateGO.transform.position = mainGateTransform.position;
            Debug.Log("[GameManager] Spawned GroundFloorMainGate in scene.");
        }
        else if (MainGateController.Instance != null && mainGateTransform != null)
        {
            MainGateController.Instance.transform.position = mainGateTransform.position;
        }

        if (FindObjectsOfType<KeyItemPickup>().Length == 0 && IsServerAuthority())
        {
            Vector3 pos1, pos2;
            GetRandomRoomKeySpawnPositions(out pos1, out pos2);

            GameObject k1, k2;
            if (customKeyPrefab != null)
            {
                k1 = Instantiate(customKeyPrefab, pos1, Quaternion.identity);
                k1.name = "Key_1";
                k2 = Instantiate(customKeyPrefab, pos2, Quaternion.identity);
                k2.name = "Key_2";
            }
            else
            {
                k1 = new GameObject("Key_1", typeof(KeyItemPickup));
                k1.transform.position = pos1;
                k1.transform.localScale = new Vector3(0.4f, 0.4f, 1f); // Smaller, clean realistic key size

                k2 = new GameObject("Key_2", typeof(KeyItemPickup));
                k2.transform.position = pos2;
                k2.transform.localScale = new Vector3(0.4f, 0.4f, 1f); // Smaller, clean realistic key size
            }

            var key1Comp = k1.GetComponent<KeyItemPickup>();
            if (key1Comp == null) key1Comp = k1.AddComponent<KeyItemPickup>();
            key1Comp.keyIndex = 1;

            var key2Comp = k2.GetComponent<KeyItemPickup>();
            if (key2Comp == null) key2Comp = k2.AddComponent<KeyItemPickup>();
            key2Comp.keyIndex = 2;

            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                var no1 = k1.GetComponent<Unity.Netcode.NetworkObject>(); if (no1 != null) no1.Spawn(true);
                var no2 = k2.GetComponent<Unity.Netcode.NetworkObject>(); if (no2 != null) no2.Spawn(true);
            }
            Debug.Log($"[GameManager] Spawned Key 1 at {pos1} and Key 2 at {pos2} in distinct room walk spaces.");
        }

        // ── Safe ("seaf") Selection Logic ───────────────────────────────────────────
        // Select ONLY 1 Safe from manual list / pre-placed scene safes / spawn points
        if (IsServerAuthority())
        {
            List<SafeController> candidateSafes = new List<SafeController>();

            // 1. Check inspector manualSafes list
            if (manualSafes != null && manualSafes.Count > 0)
            {
                foreach (var s in manualSafes)
                {
                    if (s != null && !candidateSafes.Contains(s)) candidateSafes.Add(s);
                }
            }

            // 2. Auto-discover all pre-placed SafeController objects in scene (including inactive)
            SafeController[] sceneSafes = FindObjectsOfType<SafeController>(true);
            foreach (var s in sceneSafes)
            {
                if (s != null && !candidateSafes.Contains(s)) candidateSafes.Add(s);
            }

            if (candidateSafes.Count > 0)
            {
                // Randomly pick ONLY 1 safe to activate and keep in game
                int chosenIndex = Random.Range(0, candidateSafes.Count);
                for (int i = 0; i < candidateSafes.Count; i++)
                {
                    bool isChosen = (i == chosenIndex);
                    candidateSafes[i].gameObject.SetActive(isChosen);
                    if (isChosen)
                    {
                        Debug.Log($"[GameManager] Picked 1 Safe ('{candidateSafes[i].name}') from list of {candidateSafes.Count} candidate safes in scene.");
                    }
                }
            }
            else if (safeSpawnPoints != null && safeSpawnPoints.Count > 0)
            {
                List<Transform> validPoints = safeSpawnPoints.FindAll(p => p != null);
                if (validPoints.Count > 0)
                {
                    Transform chosenPoint = validPoints[Random.Range(0, validPoints.Count)];
                    GameObject safeGO = new GameObject("Safe_Seaf", typeof(SafeController));
                    safeGO.transform.position = chosenPoint.position;
                    if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                    {
                        var netObj = safeGO.GetComponent<Unity.Netcode.NetworkObject>();
                        if (netObj != null) netObj.Spawn(true);
                    }
                    Debug.Log($"[GameManager] Spawned 1 Safe at manual spawn point '{chosenPoint.name}' ({chosenPoint.position}) from list of {validPoints.Count} points.");
                }
            }
            else
            {
                // Fallback: spawn 1 safe at a random room position if no manual list or spawn points defined
                Vector3 safePos = GetRandomRoomPosition();
                GameObject safeGO = new GameObject("Safe_Seaf", typeof(SafeController));
                safeGO.transform.position = safePos;
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    var netObj = safeGO.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj != null) netObj.Spawn(true);
                }
                Debug.Log($"[GameManager] Fallback: Spawned 1 Safe at random room position {safePos}");
            }
        }

        if (GameIntroCutsceneManager.Instance == null && FindObjectOfType<GameIntroCutsceneManager>() == null)
        {
            GameObject cm = new GameObject("GameIntroCutsceneManager", typeof(Unity.Netcode.NetworkObject), typeof(GameIntroCutsceneManager));
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                var netObj = cm.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && !netObj.IsSpawned) netObj.Spawn(true);
            }
        }
    }

    // ── Floor Item Spawner ────────────────────────────────────────────────────

    /// <summary>
    /// Spawns items only on confirmed walkable, obstacle-free floor positions.
    /// Dual-check: must be INSIDE a WalkableFloorZone AND NOT on an obstacle layer.
    /// </summary>
    [ContextMenu("Spawn Items On Floor Now")]
    public void SpawnItemsOnFloor()
    {
        if (!IsServerAuthority())
        {
            Debug.Log("[GameManager] Item spawning skipped — not server authority.");
            return;
        }

        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogWarning("[GameManager] No item prefabs assigned in Inspector!");
            return;
        }

        if (walkableZones == null || walkableZones.Count == 0)
        {
            Debug.LogWarning("[GameManager] No WalkableFloorZone assigned — cannot spawn items safely.");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            if (TryFindValidSpawnPoint(out Vector3 pos))
            {
                SpawnItemAtPosition(pos);
                spawned++;
            }
            else
            {
                Debug.LogWarning($"[GameManager] Item {i + 1}: no valid empty floor spot found " +
                                 $"after {maxAttemptsPerItem} attempts — skipped.");
            }
        }

        Debug.Log($"[GameManager] Spawned {spawned}/{spawnCount} items on walkable floor.");
    }

    /// <summary>
    /// Tries up to maxAttemptsPerItem times to find a point that:
    ///   1. Is inside a WalkableFloorZone  (room or open corridor)
    ///   2. Does NOT overlap an obstacle   (no walls)
    /// Balances items between room walk spaces (isRoom = true) and open corridors.
    /// </summary>
    private bool TryFindValidSpawnPoint(out Vector3 result)
    {
        // Re-sync all active zones if list is empty
        if (walkableZones == null || walkableZones.Count == 0)
        {
            walkableZones = new List<WalkableFloorZone>(FindObjectsOfType<WalkableFloorZone>());
        }

        if (walkableZones == null || walkableZones.Count == 0)
        {
            result = Vector3.zero;
            return false;
        }

        // Separate into room zones and open corridor zones for balanced distribution
        List<WalkableFloorZone> roomZones = new List<WalkableFloorZone>();
        List<WalkableFloorZone> openZones = new List<WalkableFloorZone>();

        foreach (var z in walkableZones)
        {
            if (z == null || z.zoneCollider == null) continue;
            if (z.isRoom) roomZones.Add(z);
            else openZones.Add(z);
        }

        // 50% chance to pick a room zone, 50% to pick an open corridor zone (if both exist)
        WalkableFloorZone chosenZone = null;
        if (roomZones.Count > 0 && openZones.Count > 0)
        {
            chosenZone = (Random.value < 0.5f)
                ? roomZones[Random.Range(0, roomZones.Count)]
                : openZones[Random.Range(0, openZones.Count)];
        }
        else if (roomZones.Count > 0)
        {
            chosenZone = roomZones[Random.Range(0, roomZones.Count)];
        }
        else if (openZones.Count > 0)
        {
            chosenZone = openZones[Random.Range(0, openZones.Count)];
        }
        else
        {
            chosenZone = walkableZones[Random.Range(0, walkableZones.Count)];
        }

        if (chosenZone == null || chosenZone.zoneCollider == null)
        {
            result = Vector3.zero;
            return false;
        }

        Bounds b = chosenZone.zoneCollider.bounds;

        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            Vector2 candidate = new Vector2(x, y);

            // 1. WHITELIST — must be inside the exact collider shape
            if (!chosenZone.ContainsPoint(candidate)) continue;

            // 2. BLACKLIST — must not overlap any wall/obstacle collider
            if (Physics2D.OverlapCircle(candidate, overlapCheckRadius, obstacleLayerMask) != null) continue;

            result = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Finds a valid random spawn position inside a room's WalkableFloorZone.
    /// Falls back to GetRandomSpawnPosition() if no room zone is found.
    /// </summary>
    public Vector3 GetRandomRoomPosition()
    {
        WalkableFloorZone[] allZones = FindObjectsOfType<WalkableFloorZone>();
        List<WalkableFloorZone> roomZones = new List<WalkableFloorZone>();

        foreach (var zone in allZones)
        {
            if (zone != null && zone.isRoom && zone.zoneCollider != null)
            {
                roomZones.Add(zone);
            }
        }

        if (roomZones.Count > 0)
        {
            WalkableFloorZone picked = roomZones[Random.Range(0, roomZones.Count)];
            if (TryFindPointInZone(picked, out Vector3 pos))
            {
                return pos;
            }
        }

        if (TryFindValidSpawnPoint(out Vector3 fallback))
        {
            return fallback;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Finds 2 random spawn positions in 2 DIFFERENT room walk spaces (WalkableFloorZone where isRoom = true).
    /// Both keys will never spawn in the same room.
    /// Falls back to default positions if room zones are missing.
    /// </summary>
    private bool GetRandomRoomKeySpawnPositions(out Vector3 pos1, out Vector3 pos2)
    {
        WalkableFloorZone[] allZones = FindObjectsOfType<WalkableFloorZone>();
        List<WalkableFloorZone> roomZones = new List<WalkableFloorZone>();

        foreach (var zone in allZones)
        {
            if (zone != null && zone.isRoom && zone.zoneCollider != null)
            {
                roomZones.Add(zone);
            }
        }

        // Group room zones by roomId (if roomId is empty, use instance ID so distinct zones are treated separately)
        Dictionary<string, List<WalkableFloorZone>> roomGroups = new Dictionary<string, List<WalkableFloorZone>>();
        for (int i = 0; i < roomZones.Count; i++)
        {
            var zone = roomZones[i];
            string key = string.IsNullOrEmpty(zone.roomId) ? $"zone_{zone.GetInstanceID()}" : zone.roomId;
            if (!roomGroups.ContainsKey(key)) roomGroups[key] = new List<WalkableFloorZone>();
            roomGroups[key].Add(zone);
        }

        List<string> roomKeys = new List<string>(roomGroups.Keys);

        if (roomKeys.Count >= 2)
        {
            // Pick 2 distinct rooms
            int idx1 = Random.Range(0, roomKeys.Count);
            string roomKey1 = roomKeys[idx1];
            roomKeys.RemoveAt(idx1);

            int idx2 = Random.Range(0, roomKeys.Count);
            string roomKey2 = roomKeys[idx2];

            WalkableFloorZone zone1 = roomGroups[roomKey1][Random.Range(0, roomGroups[roomKey1].Count)];
            WalkableFloorZone zone2 = roomGroups[roomKey2][Random.Range(0, roomGroups[roomKey2].Count)];

            bool found1 = TryFindPointInZone(zone1, out pos1);
            bool found2 = TryFindPointInZone(zone2, out pos2);

            if (found1 && found2)
            {
                Debug.Log($"[GameManager] Picked 2 distinct rooms for key spawning: '{roomKey1}' and '{roomKey2}'.");
                return true;
            }
        }
        else if (roomKeys.Count == 1)
        {
            string roomKey = roomKeys[0];
            WalkableFloorZone zone = roomGroups[roomKey][0];
            Debug.LogWarning($"[GameManager] Only 1 room zone found ('{roomKey}'). Key 1 & Key 2 should be in 2 DIFFERENT rooms! Please add another room zone with isRoom = true and a different roomId.");
            bool found1 = TryFindPointInZone(zone, out pos1);
            bool found2 = TryFindPointInZone(zone, out pos2);
            if (found1 && found2) return true;
        }

        Debug.LogWarning("[GameManager] No room zones (isRoom = true) found for key spawning! Check 'Is Room' on WalkableFloorZone in Inspector. Falling back to default positions.");
        pos1 = new Vector3(-4.5f, -7.5f, 0f);
        pos2 = new Vector3(4.5f, -5.5f, 0f);
        return false;
    }

    private bool TryFindPointInZone(WalkableFloorZone zone, out Vector3 result, int maxAttempts = 50)
    {
        if (zone == null || zone.zoneCollider == null) { result = Vector3.zero; return false; }
        Bounds b = zone.zoneCollider.bounds;
        int obstacleMask = obstacleLayerMask != 0 ? (int)obstacleLayerMask : LayerMask.GetMask("Obstacle", "Wall");

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = Random.Range(b.min.x, b.max.x);
            float y = Random.Range(b.min.y, b.max.y);
            Vector2 candidate = new Vector2(x, y);

            if (!zone.ContainsPoint(candidate)) continue;
            if (Physics2D.OverlapCircle(candidate, overlapCheckRadius > 0 ? overlapCheckRadius : 0.25f, obstacleMask) != null) continue;

            result = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        result = zone.zoneCollider.bounds.center;
        return true;
    }

    private void SpawnItemAtPosition(Vector3 position)
    {
        GameObject prefab   = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
        GameObject spawnObj = Instantiate(prefab, position, Quaternion.identity);

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            var netObj = spawnObj.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                ItemPickup pickup = spawnObj.GetComponent<ItemPickup>();
                if (pickup != null)
                {
                    string nameStr = pickup.itemData != null ? pickup.itemData.itemName : "";
                    pickup.SetNetworkState(pickup.amount, pickup.wasDropped, nameStr);
                }
                netObj.Spawn(true);
            }
        }
    }

    /// <summary>
    /// Returns true if this instance is allowed to spawn authoritative game objects.
    /// Supports Photon PUN, Unity NGO, and offline (single-player) modes.
    /// </summary>
    private bool IsServerAuthority()
    {
        // Unity Netcode for GameObjects
        if (Unity.Netcode.NetworkManager.Singleton != null)
            return Unity.Netcode.NetworkManager.Singleton.IsServer;

        // Photon PUN2
        if (Photon.Pun.PhotonNetwork.IsConnected)
            return Photon.Pun.PhotonNetwork.IsMasterClient;

        // Offline / single-player — always authoritative
        return true;
    }

    /// <summary>
    /// Restores the local player's position, health, and weapon state following a Host Migration.
    /// </summary>
    public void RestorePlayerFromSnapshot()
    {
        if (!RelayNetworkManager.HasSnapshot || !RelayNetworkManager.LastPlayerSnapshot.HasValue) return;

        var snapshot = RelayNetworkManager.LastPlayerSnapshot.Value;
        Debug.Log($"[GameManager] Restoring player from snapshot: Position={snapshot.position}, HP={snapshot.health}");

        GameObject pObj = null;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient != null && Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            pObj = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        }
        if (pObj == null)
        {
            // Search all player objects in scene for the owned controller
            PlayerController[] controllers = FindObjectsOfType<PlayerController>();
            foreach (var pc in controllers)
            {
                if (pc != null && pc.IsOwner)
                {
                    pObj = pc.gameObject;
                    break;
                }
            }
        }
        if (pObj == null)
        {
            pObj = GameObject.FindGameObjectWithTag("Player");
        }

        // If pObj is STILL null and we are server/host, manually spawn player prefab for host
        if (pObj == null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            if (Unity.Netcode.NetworkManager.Singleton.NetworkConfig != null && Unity.Netcode.NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
            {
                GameObject spawned = Instantiate(Unity.Netcode.NetworkManager.Singleton.NetworkConfig.PlayerPrefab, snapshot.position, snapshot.rotation);
                var netObj = spawned.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null)
                {
                    netObj.SpawnWithOwnership(Unity.Netcode.NetworkManager.Singleton.LocalClientId, true);
                    pObj = spawned;
                    Debug.Log("[GameManager] Server manually spawned player object for Host migration!");
                }
            }
        }

        if (pObj != null)
        {
            pObj.transform.position = snapshot.position;
            pObj.transform.rotation = snapshot.rotation;

            // Restore character facing direction (sprite flip)
            var assembler = pObj.GetComponentInChildren<CharacterAssembler>();
            if (assembler != null)
            {
                assembler.SetFacingDirection(snapshot.facingRight);
            }

            // Target camera onto local player
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(pObj.transform);
            }


            var health = pObj.GetComponent<PlayerHealth>();
            if (health == null) health = PlayerHealth.Instance;
            if (health != null && snapshot.health > 0)
            {
                health.RestoreHealthFromSnapshot(snapshot.health);
            }

            var weaponCtrl = pObj.GetComponent<WeaponController>();
            if (weaponCtrl == null) weaponCtrl = WeaponController.Instance;
            if (weaponCtrl != null)
            {
                // Re-equip weapons from snapshot (slot names) then switch to the active slot
                if (snapshot.weaponSlotNames != null)
                {
                    for (int i = 0; i < snapshot.weaponSlotNames.Length; i++)
                    {
                        string wName = snapshot.weaponSlotNames[i];
                        if (!string.IsNullOrEmpty(wName))
                        {
                            GameObject prefab = weaponCtrl.FindWeaponPrefabByNamePublic(wName);
                            if (prefab != null)
                                weaponCtrl.EquipWeaponToSlot(i, prefab);
                        }
                    }
                }
                weaponCtrl.SwitchToSlot(snapshot.currentWeaponIndex);
            }

            var bag = pObj.GetComponent<BagManager>();
            if (bag == null) bag = BagManager.Instance;
            if (bag != null)
            {
                bag.RestoreFromSnapshot(snapshot);
            }

            if (snapshot.isGhost)
            {
                var pc = pObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.EnableGhostMode();
                }

                if (MobileInputManager.Instance != null)
                {
                    MobileInputManager.Instance.SetGhostUI(true);
                }
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.SetGhostUI(true);
                }
                Debug.Log("[GameManager] Restored ghost mode & ghost UI controls on local player!");
            }

            Debug.Log($"[GameManager] Player state restored successfully at position {snapshot.position}, IsGhost: {snapshot.isGhost}");
        }
        else
        {
            Debug.LogWarning("[GameManager] RestorePlayerFromSnapshot failed to locate or spawn local player object!");
        }
    }

    /// <summary>
    /// Restores world item pickups from a migration snapshot at their exact original positions
    /// instead of randomly re-spawning new items. Called by the new host after host migration.
    /// </summary>
    public void RestoreWorldItemsFromSnapshot(System.Collections.Generic.List<RelayNetworkManager.WorldItemState> worldItems)
    {
        if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[GameManager] RestoreWorldItemsFromSnapshot skipped — not server.");
            return;
        }

        if (worldItems == null || worldItems.Count == 0)
        {
            Debug.Log("[GameManager] No world items in snapshot — falling back to fresh spawn.");
            SpawnItemsOnFloor();
            return;
        }

        int spawned = 0;
        foreach (var state in worldItems)
        {
            // Find the matching prefab by itemName from the itemPrefabs list
            GameObject matchedPrefab = null;
            foreach (var prefab in itemPrefabs)
            {
                if (prefab == null) continue;
                // Match by prefab name or by ItemPickup.itemData.itemName
                string pName = prefab.name.Replace("(Clone)", "").Trim();
                if (pName == state.itemName)
                {
                    matchedPrefab = prefab;
                    break;
                }
                // Also check via ItemPickup component's itemData name
                var pickup = prefab.GetComponent<ItemPickup>();
                if (pickup != null && pickup.itemData != null && pickup.itemData.itemName == state.itemName)
                {
                    matchedPrefab = prefab;
                    break;
                }
            }

            if (matchedPrefab == null)
            {
                Debug.LogWarning($"[GameManager] RestoreWorldItems: no prefab found for item '{state.itemName}' — skipping.");
                continue;
            }

            GameObject spawnObj = Instantiate(matchedPrefab, state.position, Quaternion.identity);
            var netObj = spawnObj.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                var itemPickup = spawnObj.GetComponent<ItemPickup>();
                if (itemPickup != null)
                {
                    string nameStr = itemPickup.itemData != null ? itemPickup.itemData.itemName : state.itemName;
                    itemPickup.SetNetworkState(state.amount, state.wasDropped, nameStr);
                }
                netObj.Spawn(true);
                spawned++;
            }
        }

        Debug.Log($"[GameManager] Restored {spawned}/{worldItems.Count} world items from migration snapshot.");
    }
}
