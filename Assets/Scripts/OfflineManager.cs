using UnityEngine;
using System.Collections.Generic;

public class OfflineManager : MonoBehaviour
{
    public static OfflineManager Instance { get; private set; }

    [Header("Player Settings")]
    [Tooltip("Drag and drop your Player Prefab here. If left empty, it will auto-load from Resources or Assets.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Walkable Floor Zone Spawning")]
    [Tooltip("WalkableFloorZones where the player can spawn. Leave empty to auto-discover all WalkableFloorZones in the scene.")]
    public List<WalkableFloorZone> walkableZones = new List<WalkableFloorZone>();

    [Tooltip("If checked, prioritizes WalkableFloorZones that have isGroundFloor = true. If none are found, all available WalkableFloorZones will be used.")]
    public bool preferGroundFloor = true;

    [Tooltip("If false, excludes WalkableFloorZones that have isRoom = true (keeps player in hallways/main floor).")]
    public bool allowSpawnInRooms = true;

    [Header("Obstacle Blacklist & Collision Checks")]
    [Tooltip("Physics layers for obstacles and walls to prevent spawning inside walls/furniture.")]
    public LayerMask obstacleLayerMask;

    [Tooltip("Circle radius for checking wall/obstacle collisions around the spawn position.")]
    public float overlapCheckRadius = 0.35f;

    [Tooltip("Maximum random spawn attempts per zone before falling back to zone center.")]
    public int maxSpawnAttempts = 100;

    [Header("Ground Hall / Transform Fallback (Optional)")]
    [Tooltip("Ground Hall / Ground Floor transform fallback if no WalkableFloorZones are present.")]
    public Transform groundHallTransform;

    [Header("UI References (Optional)")]
    [Tooltip("Central Enter UI Button for doors.")]
    public UnityEngine.UI.Button enterButton;

    [Tooltip("Central Exit UI Button for rooms.")]
    public UnityEngine.UI.Button exitButton;

    [Header("Item & Weapon Spawning")]
    [Tooltip("Number of items and weapons to spawn across walkable areas.")]
    public int itemSpawnCount = 16;

    [Tooltip("List of item/weapon prefabs to spawn. If empty, automatically discovers and loads all weapons and items from the project.")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    [Header("AI Bot Spawning")]
    [Tooltip("Prefab for AI Bots. If empty, auto-loads Assets/Prefab/AiBot.prefab")]
    public GameObject botPrefab;

    [Tooltip("Number of AI Bots to spawn across the map in offline mode.")]
    public int botSpawnCount = 3;

    [Header("Safe Spawning")]
    [Tooltip("Drag your custom Safe Prefab here. If left empty, GameManager's safePrefab or procedural Safe is used.")]
    public GameObject safePrefab;

    public GameObject SpawnedPlayer { get; private set; }
    public List<GameObject> SpawnedBots { get; private set; } = new List<GameObject>();

    // ── Room Tracking ─────────────────────────────────────────────────────────
    public static RoomController CurrentRoom { get; private set; }
    public static string CurrentRoomId => CurrentRoom != null ? CurrentRoom.roomId : string.Empty;

    public void SetCurrentRoom(RoomController room)
    {
        CurrentRoom = room;
        string label = room != null ? $"'{room.roomDisplayName}' ({room.roomId})" : "world (no room)";
        Debug.Log($"[OfflineManager] Local player is now in: {label}");
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureObstacleLayerMask();
    }

    private void Start()
    {
        EnsureMatchRoleManager();
        SpawnPlayerInWalkableArea();
        SpawnItemsAcrossWalkableZones();
        SpawnBotsAcrossWalkableZones();
        SpawnOfflineSafe();
    }

    private void EnsureMatchRoleManager()
    {
        if (MatchRoleManager.Instance == null && FindObjectOfType<MatchRoleManager>() == null)
        {
            new GameObject("MatchRoleManager", typeof(MatchRoleManager));
        }

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.ResetMatchState();
        }
    }

    public void SpawnOfflineSafe()
    {
        SafeController existing = FindObjectOfType<SafeController>();
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.ResetToClosedState();
            Debug.Log($"[OfflineManager] Safe already exists at {existing.transform.position}. Enabled and reset to Closed.");
            return;
        }

        Vector3 safePos = Vector3.zero;
        bool foundSafePos = false;

        // 1. Prioritize WalkableFloorZones marked as a room
        if (walkableZones == null || walkableZones.Count == 0)
        {
            RefreshWalkableZones();
        }

        List<WalkableFloorZone> roomZones = walkableZones.FindAll(z => z != null && (z.isRoom || z.gameObject.name.ToLower().Contains("room")));
        if (roomZones.Count > 0)
        {
            foundSafePos = TryFindValidPointInZones(roomZones, out safePos);
            if (foundSafePos)
            {
                Debug.Log($"[OfflineManager] 🏠 Placed Safe inside Room WalkableFloorZone at {safePos}");
            }
        }

        // 2. Try inside RoomControllers' exitBoxCollider / walkable bounds
        if (!foundSafePos)
        {
            RoomController[] allRooms = FindObjectsOfType<RoomController>();
            if (allRooms != null && allRooms.Length > 0)
            {
                List<RoomController> shuffledRooms = new List<RoomController>(allRooms);
                for (int r = 0; r < shuffledRooms.Count; r++)
                {
                    int rnd = Random.Range(r, shuffledRooms.Count);
                    var tmp = shuffledRooms[r];
                    shuffledRooms[r] = shuffledRooms[rnd];
                    shuffledRooms[rnd] = tmp;
                }

                foreach (var room in shuffledRooms)
                {
                    if (room == null) continue;
                    BoxCollider2D roomCol = room.exitBoxCollider != null ? room.exitBoxCollider : room.GetComponent<BoxCollider2D>();
                    if (roomCol != null)
                    {
                        Bounds b = roomCol.bounds;
                        for (int attempt = 0; attempt < 35; attempt++)
                        {
                            Vector2 cand = new Vector2(Random.Range(b.min.x + 0.3f, b.max.x - 0.3f), Random.Range(b.min.y + 0.3f, b.max.y - 0.3f));
                            if (roomCol.OverlapPoint(cand) && !IsPointBlockedByObstacle(cand, roomCol))
                            {
                                safePos = new Vector3(cand.x, cand.y, 0f);
                                foundSafePos = true;
                                Debug.Log($"[OfflineManager] 🏠 Placed Safe in '{room.roomDisplayName}' walk area at {safePos}");
                                break;
                            }
                        }
                    }
                    if (foundSafePos) break;
                }

                if (!foundSafePos && shuffledRooms.Count > 0 && shuffledRooms[0] != null)
                {
                    safePos = shuffledRooms[0].GetSpawnPosition() + (Vector3)(Random.insideUnitCircle * 0.4f);
                    safePos.z = 0f;
                    foundSafePos = true;
                }
            }
        }

        // 3. Fallback to any walkable floor zone
        if (!foundSafePos && walkableZones != null && walkableZones.Count > 0)
        {
            foundSafePos = TryFindValidPointInZones(walkableZones, out safePos);
        }

        GameObject prefabToUse = safePrefab;
        if (prefabToUse == null && GameManager.Instance != null)
        {
            prefabToUse = GameManager.Instance.customSafePrefab != null ? GameManager.Instance.customSafePrefab : GameManager.Instance.safePrefab;
        }

        GameObject safeObj = null;
        if (prefabToUse != null)
        {
            safeObj = Instantiate(prefabToUse, safePos, Quaternion.identity);
            safeObj.name = "Safe_Seaf";
        }
        else
        {
            safeObj = new GameObject("Safe_Seaf", typeof(SafeController));
            safeObj.transform.position = safePos;
        }

        SafeController sc = safeObj.GetComponent<SafeController>();
        if (sc == null) sc = safeObj.AddComponent<SafeController>();
        sc.ResetToClosedState();
        safeObj.SetActive(true);

        Debug.Log($"[OfflineManager] 🔒 Successfully spawned Safe ('{safeObj.name}') inside Room Walk Area at {safePos}!");
    }

    private void EnsureObstacleLayerMask()
    {
        if (obstacleLayerMask.value == 0)
        {
            int mask = LayerMask.GetMask("Obstacle", "Wall");
            if (mask != 0)
            {
                obstacleLayerMask = mask;
            }
        }
    }

    /// <summary>
    /// Auto-discovers all WalkableFloorZones in the current scene.
    /// </summary>
    public void RefreshWalkableZones()
    {
        WalkableFloorZone[] found = FindObjectsOfType<WalkableFloorZone>();
        if (found != null && found.Length > 0)
        {
            walkableZones = new List<WalkableFloorZone>(found);
            Debug.Log($"[OfflineManager] Found {walkableZones.Count} WalkableFloorZone(s) in scene: " +
                      $"{string.Join(", ", walkableZones.ConvertAll(z => z != null ? z.gameObject.name : "null"))}");
        }
        else
        {
            Debug.LogWarning("[OfflineManager] No WalkableFloorZone found in scene! " +
                             "Make sure GameObjects have the WalkableFloorZone script and a Collider2D attached.");
        }
    }

    /// <summary>
    /// Spawns the player in a valid WalkableFloorZone in the scene.
    /// </summary>
    [ContextMenu("Spawn / Respawn Player")]
    public void SpawnPlayerInWalkableArea()
    {
        GameObject prefab = GetPlayerPrefab();
        if (prefab == null)
        {
            Debug.LogError("[OfflineManager] Failed to spawn player: Player Prefab not assigned and not found in Resources/Prefabs!");
            return;
        }

        // Clean up previous instance if respawning
        if (SpawnedPlayer != null)
        {
            Destroy(SpawnedPlayer);
        }

        // Always refresh zones if list is empty or has null entries
        if (walkableZones == null || walkableZones.Count == 0 || walkableZones.Exists(z => z == null))
        {
            RefreshWalkableZones();
        }

        Vector3 spawnPosition = DetermineSpawnPosition();

        GameObject playerInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        playerInstance.name = "Player_Offline";

        // Explicitly set transform & Rigidbody2D position
        playerInstance.transform.position = spawnPosition;
        Rigidbody2D rb = playerInstance.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = (Vector2)spawnPosition;
            rb.velocity = Vector2.zero;
        }

        // In offline mode (without NGO server/client listening), remove NetworkTransform and NetworkAnimator
        // so unspawned Netcode interpolation ticks cannot pull transform.position back towards (0, 0, 0)
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            var netTransforms = playerInstance.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
            foreach (var nt in netTransforms)
            {
                if (nt != null) DestroyImmediate(nt);
            }

            var clientNetTransforms = playerInstance.GetComponentsInChildren<ClientNetworkTransform>(true);
            foreach (var cnt in clientNetTransforms)
            {
                if (cnt != null) DestroyImmediate(cnt);
            }

            var netAnimators = playerInstance.GetComponentsInChildren<Unity.Netcode.Components.NetworkAnimator>(true);
            foreach (var na in netAnimators)
            {
                if (na != null) DestroyImmediate(na);
            }
        }

        // Ensure Animator does not overwrite position with root motion
        var animators = playerInstance.GetComponentsInChildren<Animator>(true);
        foreach (var a in animators)
        {
            a.applyRootMotion = false;
        }

        SpawnedPlayer = playerInstance;

        Debug.Log($"[OfflineManager] ✅ Player successfully spawned in Walkable Area at {spawnPosition}");

        // Attach Camera target
        SetupCamera(playerInstance.transform);

        // Keep position firmly fixed during physics / animation initialization frames
        StartCoroutine(EnforceSpawnPositionRoutine(playerInstance, spawnPosition));
    }

    private System.Collections.IEnumerator EnforceSpawnPositionRoutine(GameObject playerObj, Vector3 targetPos)
    {
        if (playerObj == null) yield break;
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();

        // Yield for first 2 frames to ensure all Start/Awake/Animators settle without overriding position
        for (int i = 0; i < 3; i++)
        {
            if (playerObj == null) yield break;
            playerObj.transform.position = targetPos;
            if (rb != null)
            {
                rb.position = (Vector2)targetPos;
                rb.velocity = Vector2.zero;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Resolves the player prefab from inspector or Resources.
    /// </summary>
    private GameObject GetPlayerPrefab()
    {
        if (playerPrefab != null) return playerPrefab;

        GameObject loaded = Resources.Load<GameObject>("Player");
        if (loaded == null) loaded = Resources.Load<GameObject>("Prefabs/Player");
        if (loaded == null) loaded = Resources.Load<GameObject>("Prefab/Player");

#if UNITY_EDITOR
        if (loaded == null)
        {
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
            if (loaded == null)
            {
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            }
        }
#endif
        return loaded;
    }

    /// <summary>
    /// Picks a valid WalkableFloorZone and finds a non-colliding point inside its collider.
    /// </summary>
    private Vector3 DetermineSpawnPosition()
    {
        List<WalkableFloorZone> validZones = new List<WalkableFloorZone>();

        if (walkableZones != null && walkableZones.Count > 0)
        {
            foreach (var z in walkableZones)
            {
                if (z == null) continue;

                // Check collider
                Collider2D col = z.zoneCollider != null ? z.zoneCollider : z.GetComponent<Collider2D>();
                if (col == null) continue;

                // Check room filtering
                if (!allowSpawnInRooms && z.isRoom) continue;

                validZones.Add(z);
            }
        }

        if (validZones.Count > 0)
        {
            // 1. Try Ground Floor zones first if preferGroundFloor is enabled
            if (preferGroundFloor)
            {
                List<WalkableFloorZone> groundZones = validZones.FindAll(z => z.isGroundFloor);
                if (groundZones.Count > 0)
                {
                    if (TryFindValidPointInZones(groundZones, out Vector3 groundPos))
                    {
                        return groundPos;
                    }
                }
            }

            // 2. Try all valid zones
            if (TryFindValidPointInZones(validZones, out Vector3 anyPos))
            {
                return anyPos;
            }
        }

        // 3. Fallback: Search for GroundHallArea in scene
        GroundHallArea hallArea = FindObjectOfType<GroundHallArea>();
        if (hallArea != null)
        {
            Debug.LogWarning("[OfflineManager] WalkableFloorZone sampling failed or none found — using GroundHallArea position.");
            return hallArea.transform.position;
        }

        // 4. Fallback: Search for GameObject named 'groundfloor' or 'ground'
        GameObject groundObj = GameObject.Find("groundfloor") ?? GameObject.Find("GroundFloor") ?? GameObject.Find("Ground");
        if (groundObj != null)
        {
            Debug.LogWarning("[OfflineManager] WalkableFloorZone sampling failed — using groundfloor GameObject position.");
            return groundObj.transform.position;
        }

        // 5. Absolute fallback
        Debug.LogWarning("[OfflineManager] No WalkableFloorZone or Ground location found in scene — spawning at (0,0,0).");
        return Vector3.zero;
    }

    /// <summary>
    /// Dual-check algorithm:
    /// 1. WHITELIST: Candidate point must be inside the zone's Collider2D (using OverlapPoint).
    /// 2. BLACKLIST: Candidate point must NOT overlap any solid non-trigger obstacle/wall.
    /// </summary>
    public bool TryFindValidPointInZones(List<WalkableFloorZone> zones, out Vector3 result)
    {
        EnsureObstacleLayerMask();

        // Shuffle candidate zones so spawn is distributed
        List<WalkableFloorZone> shuffledZones = new List<WalkableFloorZone>(zones);
        for (int i = 0; i < shuffledZones.Count; i++)
        {
            int rnd = Random.Range(i, shuffledZones.Count);
            var temp = shuffledZones[i];
            shuffledZones[i] = shuffledZones[rnd];
            shuffledZones[rnd] = temp;
        }

        foreach (var zone in shuffledZones)
        {
            if (zone == null) continue;

            Collider2D zoneCol = zone.zoneCollider != null ? zone.zoneCollider : zone.GetComponent<Collider2D>();
            if (zoneCol == null) continue;

            Bounds b = zoneCol.bounds;

            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                float x = Random.Range(b.min.x, b.max.x);
                float y = Random.Range(b.min.y, b.max.y);
                Vector2 candidate = new Vector2(x, y);

                // 1. Must be inside the exact polygon / box shape of the zone collider
                if (!zoneCol.OverlapPoint(candidate))
                    continue;

                // 2. Must not be inside any solid obstacle / wall (ignores triggers like WalkableFloorZone itself)
                if (IsPointBlockedByObstacle(candidate, zoneCol))
                    continue;

                result = new Vector3(candidate.x, candidate.y, 0f);
                Debug.Log($"[OfflineManager] Selected Walkable Zone '{zone.gameObject.name}' -> Spawn Position: {result}");
                return true;
            }

            // If random samples all hit obstacles, use zone center if it is inside the collider
            Vector2 center = (Vector2)zoneCol.bounds.center;
            if (zoneCol.OverlapPoint(center) && !IsPointBlockedByObstacle(center, zoneCol))
            {
                result = new Vector3(center.x, center.y, 0f);
                Debug.Log($"[OfflineManager] Using Walkable Zone center for '{zone.gameObject.name}' -> Spawn Position: {result}");
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Checks if a candidate point overlaps any solid (non-trigger) obstacle or wall.
    /// Excludes trigger colliders (such as WalkableFloorZone, doors, items) and the zone's own collider.
    /// </summary>
    private bool IsPointBlockedByObstacle(Vector2 point, Collider2D zoneCollider)
    {
        if (obstacleLayerMask.value == 0)
        {
            // If no obstacle layer mask is configured, point is not blocked
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(point, overlapCheckRadius, obstacleLayerMask);
        foreach (var hit in hits)
        {
            if (hit != null && !hit.isTrigger && hit != zoneCollider)
            {
                return true; // Hit a solid obstacle/wall
            }
        }

        return false;
    }

    private void SetupCamera(Transform playerTransform)
    {
        CameraController cam = CameraController.Instance != null ? CameraController.Instance : FindObjectOfType<CameraController>();
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.gameObject.AddComponent<CameraController>();
            Debug.Log("[OfflineManager] Auto-added CameraController to Camera.main.");
        }

        if (cam != null)
        {
            cam.SetTarget(playerTransform);
        }
    }

    /// <summary>
    /// Spawns weapons, grenades, scopes, ammo, and items across the map's walkable floor zones.
    /// </summary>
    public void SpawnItemsAcrossWalkableZones()
    {
        EnsureItemPrefabs();

        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogWarning("[OfflineManager] ⚠️ No item prefabs found to spawn!");
            return;
        }

        RefreshWalkableZones();

        if (walkableZones == null || walkableZones.Count == 0)
        {
            Debug.LogWarning("[OfflineManager] ⚠️ No WalkableFloorZones found to spawn items into.");
            return;
        }

        int spawned = 0;
        int targetCount = Mathf.Max(itemSpawnCount, itemPrefabs.Count);

        // 1. Guaranteed spawn for each available weapon/item type first
        for (int i = 0; i < itemPrefabs.Count; i++)
        {
            GameObject prefab = itemPrefabs[i];
            if (prefab == null) continue;

            if (TryFindValidPointInZones(walkableZones, out Vector3 spawnPos))
            {
                SpawnSingleItem(prefab, spawnPos);
                spawned++;
            }
        }

        // 2. Spawn remaining random items across walkable zones
        for (int i = spawned; i < targetCount; i++)
        {
            GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
            if (prefab == null) continue;

            if (TryFindValidPointInZones(walkableZones, out Vector3 spawnPos))
            {
                SpawnSingleItem(prefab, spawnPos);
                spawned++;
            }
        }

        Debug.Log($"[OfflineManager] ✅ Successfully spawned {spawned} weapons and items across {walkableZones.Count} Walkable Zones.");
    }

    private void SpawnSingleItem(GameObject prefab, Vector3 position)
    {
        GameObject itemInstance = Instantiate(prefab, position, Quaternion.identity);
        itemInstance.name = prefab.name;

        // Ensure proper layer sorting so item is always clearly visible on the floor
        SpriteRenderer sr = itemInstance.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 10;
        }

        // Strip NGO transform sync in offline mode so it doesn't fight singleplayer transforms
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            var netTransforms = itemInstance.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
            foreach (var nt in netTransforms)
            {
                if (nt != null) DestroyImmediate(nt);
            }
            var cnt = itemInstance.GetComponentsInChildren<ClientNetworkTransform>(true);
            foreach (var c in cnt)
            {
                if (c != null) DestroyImmediate(c);
            }
        }
    }

    private void EnsureItemPrefabs()
    {
        if (itemPrefabs != null && itemPrefabs.Count > 0) return;
        if (itemPrefabs == null) itemPrefabs = new List<GameObject>();

#if UNITY_EDITOR
        string[] prefabPaths = new string[]
        {
            "Assets/Prefab/Weapon/items/A625.prefab",
            "Assets/Prefab/Weapon/items/Pistol.prefab",
            "Assets/Prefab/Weapon/items/UZI.prefab",
            "Assets/Prefab/Weapon/items/scope.prefab",
            "Assets/Prefab/Weapon/items/Grenade.prefab",
            "Assets/Prefab/Weapon/items/smoke grenade.prefab",
            "Assets/Prefab/Weapon/items/stun grenade.prefab",
            "Assets/Prefab/Weapon/items/AmoType1.prefab",
            "Assets/Prefab/Weapon/items/AmoType2.prefab",
            "Assets/Prefab/Weapon/items/AmoType3.prefab",
            "Assets/Prefab/key.prefab"
        };

        foreach (string path in prefabPaths)
        {
            GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null && !itemPrefabs.Contains(p))
            {
                itemPrefabs.Add(p);
            }
        }
#endif

        if (itemPrefabs.Count == 0)
        {
            GameObject[] loaded = Resources.LoadAll<GameObject>("Weapon/items");
            if (loaded != null && loaded.Length > 0)
            {
                itemPrefabs.AddRange(loaded);
            }
        }
    }

    /// <summary>
    /// Spawns intelligent AI Bots across walkable zones in the map.
    /// </summary>
    public void SpawnBotsAcrossWalkableZones()
    {
        EnsureBotPrefab();

        if (botPrefab == null)
        {
            Debug.LogWarning("[OfflineManager] ⚠️ No Bot Prefab found to spawn!");
            return;
        }

        RefreshWalkableZones();

        if (walkableZones == null || walkableZones.Count == 0)
        {
            Debug.LogWarning("[OfflineManager] ⚠️ No WalkableFloorZones found for bot spawning.");
            return;
        }

        SpawnedBots.Clear();

        RoomController[] allRooms = FindObjectsOfType<RoomController>();
        List<RoomController> availableRooms = new List<RoomController>();
        foreach (var r in allRooms)
        {
            if (r != null) availableRooms.Add(r);
        }

        // Shuffle rooms for random distribution
        for (int r = 0; r < availableRooms.Count; r++)
        {
            int rnd = Random.Range(r, availableRooms.Count);
            var temp = availableRooms[r];
            availableRooms[r] = availableRooms[rnd];
            availableRooms[rnd] = temp;
        }

        List<WalkableFloorZone> preferredZones = walkableZones.FindAll(z => z != null && z.isGroundFloor);
        if (preferredZones.Count == 0 && SpawnedPlayer != null)
        {
            preferredZones = walkableZones.FindAll(z => z != null && Vector3.Distance(z.transform.position, SpawnedPlayer.transform.position) < 55f);
        }
        if (preferredZones.Count == 0) preferredZones = walkableZones;

        for (int i = 0; i < botSpawnCount; i++)
        {
            Vector3 spawnPos = Vector3.zero;
            bool foundPos = false;

            // Prioritize spawning inside a Room
            if (availableRooms.Count > 0)
            {
                RoomController chosenRoom = availableRooms[i % availableRooms.Count];
                spawnPos = chosenRoom.GetSpawnPosition() + (Vector3)(Random.insideUnitCircle * 0.6f);
                spawnPos.z = 0f;
                foundPos = true;
                Debug.Log($"[OfflineManager] 🏠 Spawning Bot {i + 1} inside Room '{chosenRoom.roomDisplayName}' at {spawnPos}");
            }
            else
            {
                foundPos = TryFindValidPointInZones(preferredZones, out spawnPos) || TryFindValidPointInZones(walkableZones, out spawnPos);
            }

            if (foundPos)
            {
                GameObject bot = Instantiate(botPrefab, spawnPos, Quaternion.identity);
                bot.name = $"AiBot_{i + 1}";
                bot.tag = "Bot";

                // Ensure AiBotController is present
                var ai = bot.GetComponent<AiBotController>();
                if (ai == null) ai = bot.AddComponent<AiBotController>();
                ai.botName = $"Bot {i + 1}";

                // Ensure Rigidbody2D is simulated and dynamic (no gravity, freeze rotation)
                var rb = bot.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0f;
                    rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    rb.simulated = true;
                }

                // Strip NGO network transform components in single player
                if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    var netTransforms = bot.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
                    foreach (var nt in netTransforms) { if (nt != null) DestroyImmediate(nt); }
                    var cnt = bot.GetComponentsInChildren<ClientNetworkTransform>(true);
                    foreach (var c in cnt) { if (c != null) DestroyImmediate(c); }
                }

                SpawnedBots.Add(bot);
            }
        }

        Debug.Log($"[OfflineManager] ✅ Successfully spawned {SpawnedBots.Count} AI Bots across the map.");
    }

    private void EnsureBotPrefab()
    {
        if (botPrefab != null) return;

#if UNITY_EDITOR
        botPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/AiBot.prefab");
#endif

        if (botPrefab == null)
        {
            botPrefab = Resources.Load<GameObject>("AiBot");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (walkableZones == null) return;
        foreach (var zone in walkableZones)
        {
            if (zone == null) continue;
            Collider2D col = zone.zoneCollider != null ? zone.zoneCollider : zone.GetComponent<Collider2D>();
            if (col == null) continue;

            Bounds b = col.bounds;
            Gizmos.color = zone.isGroundFloor ? new Color(0f, 0.7f, 1f, 0.2f) : new Color(0.2f, 1f, 0.2f, 0.2f);
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = zone.isGroundFloor ? new Color(0f, 0.7f, 1f, 0.9f) : new Color(0.2f, 1f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
