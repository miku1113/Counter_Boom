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
}
