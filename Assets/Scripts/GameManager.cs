using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Item Spawner Settings")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();
    [SerializeField] private float spawnRadius = 5f;
    [SerializeField] private int spawnCount = 10;
    
    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
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
        if (playerTransform == null || itemPrefabs.Count == 0) return;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = playerTransform.position + new Vector3(randomPoint.x, randomPoint.y, 0f);

            GameObject randomPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Count)];
            Instantiate(randomPrefab, spawnPos, Quaternion.identity);
        }
        
        Debug.Log($"Spawned {spawnCount} items around player.");
    }
}
