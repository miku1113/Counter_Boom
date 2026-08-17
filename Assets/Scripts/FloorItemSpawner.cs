using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns inventory items only on walkable, empty floor space.
///
/// HOW IT WORKS — Dual-check system:
///   ✅ WHITELIST: Candidate point must be inside a WalkableFloorZone collider
///               (prevents spawning in void / black space / rooms)
///   ❌ BLACKLIST: Candidate point must NOT overlap any wall/obstacle collider
///               (prevents spawning inside walls or furniture)
///
/// SETUP (one-time, in Unity Editor):
///   1. Create a child GameObject under "groundfloor" called "WalkableFloorZone".
///   2. Add the WalkableFloorZone script to it.
///   3. Add a PolygonCollider2D and trace it over the visible walkable tiles only.
///   4. Drag that GameObject into this component's "Walkable Zones" list.
///   5. Set "Obstacle Layer Mask" to the Obstacle + Wall physics layers.
/// </summary>
public class FloorItemSpawner : MonoBehaviour
{
    public static FloorItemSpawner Instance;

    // ── Walkable zone whitelist ───────────────────────────────────────────────

    [Header("Walkable Zone Whitelist")]
    [Tooltip("Drag in one or more WalkableFloorZone GameObjects that cover ONLY the " +
             "actual walkable floor tiles (not void/rooms/walls). " +
             "A spawn point must be INSIDE one of these zones.")]
    public List<WalkableFloorZone> walkableZones = new List<WalkableFloorZone>();

    // ── Obstacle blacklist ────────────────────────────────────────────────────

    [Header("Obstacle Blacklist")]
    [Tooltip("Physics layers considered solid obstacles (walls, furniture, etc.). " +
             "Set to 'Obstacle' and/or 'Wall' layers in the Inspector. " +
             "A spawn point must NOT overlap any collider on these layers.")]
    public LayerMask obstacleLayerMask;

    [Tooltip("Circle radius for the obstacle overlap test. " +
             "Roughly equal to your item sprite's half-width (default 0.25).")]
    public float overlapCheckRadius = 0.25f;

    // ── Spawn settings ────────────────────────────────────────────────────────

    [Header("Spawn Settings")]
    [Tooltip("Number of items to spawn at game start.")]
    public int spawnCount = 10;

    [Tooltip("Item prefabs to randomly choose from when spawning.")]
    public List<GameObject> itemPrefabs = new List<GameObject>();

    [Tooltip("Maximum random attempts per item before giving up and skipping it.")]
    public int maxAttemptsPerItem = 100;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (!IsServerAuthority()) return;

        // Auto-discover WalkableFloorZones if list is empty
        if (walkableZones == null || walkableZones.Count == 0)
            AutoDiscoverZones();

        SpawnItemsOnFloor();
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-discovers all WalkableFloorZone components in the scene.
    /// Called automatically if the walkableZones list is empty.
    /// </summary>
    private void AutoDiscoverZones()
    {
        WalkableFloorZone[] found = FindObjectsOfType<WalkableFloorZone>();
        walkableZones = new List<WalkableFloorZone>(found);
        if (walkableZones.Count > 0)
            Debug.Log($"[FloorItemSpawner] Auto-discovered {walkableZones.Count} WalkableFloorZone(s).");
        else
            Debug.LogWarning("[FloorItemSpawner] No WalkableFloorZone found in scene! " +
                             "Create a GameObject with WalkableFloorZone + Collider2D over the floor.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Main spawn method. Call from code or right-click the component → "Spawn Items On Floor Now".
    /// </summary>
    [ContextMenu("Spawn Items On Floor Now")]
    public void SpawnItemsOnFloor()
    {
        if (!IsServerAuthority())
        {
            Debug.Log("[FloorItemSpawner] Skipped — not server authority.");
            return;
        }

        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogWarning("[FloorItemSpawner] No item prefabs assigned!");
            return;
        }

        if (walkableZones == null || walkableZones.Count == 0)
        {
            Debug.LogWarning("[FloorItemSpawner] No walkable zones defined. " +
                             "Add a WalkableFloorZone to the scene and assign it here.");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            if (TryFindValidSpawnPoint(out Vector3 pos))
            {
                SpawnItem(pos);
                spawned++;
            }
            else
            {
                Debug.LogWarning($"[FloorItemSpawner] Item {i + 1}: no valid empty floor spot found " +
                                 $"after {maxAttemptsPerItem} attempts — skipped.");
            }
        }

        Debug.Log($"[FloorItemSpawner] Done — spawned {spawned}/{spawnCount} items on walkable floor.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries up to maxAttemptsPerItem times to find a point that:
    ///   1. Is inside a WalkableFloorZone  (whitelist — on real floor)
    ///   2. Does not overlap an obstacle   (blacklist — not on walls)
    /// </summary>
    private bool TryFindValidSpawnPoint(out Vector3 result)
    {
        // Pick a random zone weighted by its collider area
        WalkableFloorZone zone = PickRandomZone();
        if (zone == null) { result = Vector3.zero; return false; }

        Bounds zoneBounds = zone.zoneCollider != null
            ? zone.zoneCollider.bounds
            : new Bounds(zone.transform.position, Vector3.one * 5f);

        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            // 1. Random point inside the zone's AABB
            float x = Random.Range(zoneBounds.min.x, zoneBounds.max.x);
            float y = Random.Range(zoneBounds.min.y, zoneBounds.max.y);
            Vector2 candidate = new Vector2(x, y);

            // 2. WHITELIST — must be inside the actual zone collider shape
            //    (OverlapPoint handles PolygonCollider2D concave shapes correctly)
            if (!zone.ContainsPoint(candidate))
                continue;   // Point is in the AABB but outside the polygon — reject

            // 3. BLACKLIST — must not be on a wall or obstacle
            Collider2D obstacleHit = Physics2D.OverlapCircle(candidate, overlapCheckRadius, obstacleLayerMask);
            if (obstacleHit != null)
                continue;   // Point is on or inside a wall — reject

            // ✅ Valid floor position — no void, no wall
            result = new Vector3(candidate.x, candidate.y, 0f);
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Picks a walkable zone at random. If multiple zones exist they all have
    /// equal probability (extend to area-weighted if needed).
    /// </summary>
    private WalkableFloorZone PickRandomZone()
    {
        if (walkableZones == null || walkableZones.Count == 0) return null;
        return walkableZones[Random.Range(0, walkableZones.Count)];
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void SpawnItem(Vector3 position)
    {
        GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
        GameObject spawnObj = Instantiate(prefab, position, Quaternion.identity);

        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsServer)
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

    // ─────────────────────────────────────────────────────────────────────────

    private bool IsServerAuthority()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
            return Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (Photon.Pun.PhotonNetwork.IsConnected)
            return Photon.Pun.PhotonNetwork.IsMasterClient;

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (walkableZones == null) return;
        foreach (var zone in walkableZones)
        {
            if (zone == null || zone.zoneCollider == null) continue;
            Bounds b = zone.zoneCollider.bounds;

            // Green fill = valid spawn area
            Gizmos.color = new Color(0f, 1f, 0f, 0.12f);
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(b.center, b.size);

            // Yellow sphere = overlap check radius sample
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(b.center, overlapCheckRadius);
        }
    }
}
