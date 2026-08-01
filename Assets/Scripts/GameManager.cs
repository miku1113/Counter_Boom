using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Item Spawner Settings")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();
    [SerializeField] private float spawnRadius = 5f;
    [SerializeField] private int   spawnCount  = 10;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Only the server/host should spawn world items
        if (!IsServerAuthority()) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        SpawnItemsAroundPlayer();
    }

    [ContextMenu("Spawn Items Now")]
    public void SpawnItemsAroundPlayer()
    {
        if (!IsServerAuthority())
        {
            Debug.Log("[GameManager] Item spawning skipped — not server authority.");
            return;
        }

        if (playerTransform == null || itemPrefabs.Count == 0) return;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos    = playerTransform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

            GameObject randomPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
            GameObject spawnObj = Instantiate(randomPrefab, spawnPos, Quaternion.identity);

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

        Debug.Log($"[GameManager] Spawned {spawnCount} items around player.");
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
        if (!RelayNetworkManager.HasSnapshot) return;

        var snapshot = RelayNetworkManager.LastPlayerSnapshot;
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
            SpawnItemsAroundPlayer();
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
