using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Manages the player's bag inventory: ammo, grenades, consumables, and scope counts.
/// Weapon slot state is owned exclusively by WeaponController; this class delegates to it.
/// </summary>
public class BagManager : NetworkBehaviour
{
    public static BagManager Instance;

    // ─── Fields ──────────────────────────────────────────────────────────────

    [Header("Capacity")]
    public int maxWeight     = 100;
    public int currentWeight = 0;

    [Header("Drop Settings")]
    public Transform dropPoint;          // Child transform on Player used as drop origin
    public float     dropRadius = 0.5f;  // Spread radius for dropped items

    [Header("Inventory Data Registry")]
    public List<InventoryItemData> allItemData; // All ammo and grenade ScriptableObjects

    [Header("Inventory Counts")]
    public Dictionary<AmmoType, int>    ammoInventory    = new Dictionary<AmmoType, int>();
    public Dictionary<GrenadeType, int> grenadeInventory = new Dictionary<GrenadeType, int>();
    public int scopeCount        = 0;
    public int medikitCount      = 0;
    public int proteinShakeCount = 0;
    
    [Header("Active Grenade Type")]
    public GrenadeType activeGrenadeType = GrenadeType.Explosive;

    [Header("References")]
    [SerializeField] private WeaponController weaponController; // Optional — falls back to WeaponController.Instance

    // ─── Events ──────────────────────────────────────────────────────────────

    public System.Action<AmmoType, int>    OnAmmoUpdated;
    public System.Action<GrenadeType, int> OnGrenadeUpdated;
    public System.Action<int>              OnScopeUpdated;
    public System.Action<int>              OnMedikitUpdated;
    public System.Action<int>              OnProteinShakeUpdated;
    public System.Action                   OnBagUpdated;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
#if UNITY_EDITOR
        PopulateAllItemDataInEditor();
#endif

        foreach (AmmoType type in System.Enum.GetValues(typeof(AmmoType)))
        {
            if (type != AmmoType.None) ammoInventory[type] = 0;
        }

        foreach (GrenadeType type in System.Enum.GetValues(typeof(GrenadeType)))
        {
            if (type != GrenadeType.None) grenadeInventory[type] = 0;
        }
    }

    private void Start()
    {
        // Only set the static Instance if this is the local player!
        bool isLocal = false;
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null)
        {
            if (netObj.IsLocalPlayer) isLocal = true;
        }
        else
        {
            var photonView = GetComponent<Photon.Pun.PhotonView>();
            if (photonView != null)
            {
                if (photonView.IsMine) isLocal = true;
            }
            else
            {
                isLocal = true; // Offline fallback
            }
        }

        if (isLocal)
        {
            Instance = this;
        }

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();
        bool isLobbyScene = activeScene.Contains("lobby");

        if (isLobbyScene)
        {
            grenadeInventory[GrenadeType.Smoke] = 20;
            activeGrenadeType = GrenadeType.Smoke;
            OnGrenadeUpdated?.Invoke(GrenadeType.Smoke, 20);
            OnBagUpdated?.Invoke();
            Debug.Log("[BagManager] Initialized Lobby loadout: 20 Smoke Grenades, 0 Guns.");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        PopulateAllItemDataInEditor();
    }

    private void PopulateAllItemDataInEditor()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InventoryItemData");
        if (guids != null && guids.Length > 0)
        {
            if (allItemData == null) allItemData = new List<InventoryItemData>();
            allItemData.Clear();
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                InventoryItemData asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryItemData>(path);
                if (asset != null)
                {
                    allItemData.Add(asset);
                }
            }
        }
    }
#endif


    // ─── Weapon Slot Delegation ──────────────────────────────────────────────
    // WeaponController owns weaponSlots[]. BagManager delegates to it so other
    // systems (HUDManager, BagUI) have a single consistent access point.

    /// <summary>Returns the weapon currently in a specific slot (null if empty).</summary>
    public HandheldWeapon GetWeaponInSlot(int slot)
        => WeaponController.Instance != null ? WeaponController.Instance.GetWeaponInSlot(slot) : null;

    /// <summary>Returns the index of the currently active weapon slot.</summary>
    public int GetCurrentWeaponIndex()
        => WeaponController.Instance != null ? WeaponController.Instance.GetCurrentSlot() : -1;

    // ─── Capacity ────────────────────────────────────────────────────────────

    public bool CanAddItem(int weight)
    {
        if (maxWeight <= 0)
        {
            Debug.LogError($"[BagManager] maxWeight is {maxWeight}! Capacity is not set or was initialized to zero in Inspector.");
        }
        bool allowed = currentWeight + weight <= maxWeight;
        Debug.Log($"[BagManager] CanAddItem check: adding weight={weight}, currentWeight={currentWeight}, maxWeight={maxWeight}. Allowed={allowed}");
        return allowed;
    }


    // ─── Ammo ────────────────────────────────────────────────────────────────

    public bool AddAmmo(AmmoType type, int amount, int weight)
    {
        if (!CanAddItem(weight)) return false;
        ammoInventory[type] += amount;
        currentWeight       += weight;
        OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
        OnBagUpdated?.Invoke();
        return true;
    }

    public void ConsumeAmmo(AmmoType type, int amount)
    {
        if (!ammoInventory.ContainsKey(type)) return;
        ammoInventory[type] = Mathf.Max(0, ammoInventory[type] - amount);
        OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
        OnBagUpdated?.Invoke();
    }

    public void DropAmmo(AmmoType type, InventoryItemData data, int amount)
    {
        if (!ammoInventory.ContainsKey(type) || ammoInventory[type] < amount) return;
        ammoInventory[type] -= amount;
        if (data != null) currentWeight = Mathf.Max(0, currentWeight - data.weight * amount);
        SpawnPickup(data, amount);
        OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
        OnBagUpdated?.Invoke();
    }

    public int GetAmmo(AmmoType type) => ammoInventory.ContainsKey(type) ? ammoInventory[type] : 0;

    // ─── Grenades ────────────────────────────────────────────────────────────

    public bool AddGrenade(GrenadeType type, int amount, int weight)
    {
        if (!CanAddItem(weight)) return false;
        if (!grenadeInventory.ContainsKey(type)) grenadeInventory[type] = 0;
        grenadeInventory[type] += amount;
        currentWeight          += weight;
        
        // Auto-equip the newly picked up grenade if we currently have 0 of our active grenade type
        if (GetGrenadeCount(activeGrenadeType) <= 0)
        {
            activeGrenadeType = type;
            Debug.Log($"[BagManager] Auto-equipping grenade of type: {type} (previous active was empty).");
        }

        OnGrenadeUpdated?.Invoke(type, grenadeInventory[type]);
        OnBagUpdated?.Invoke();
        return true;
    }

    public void ConsumeGrenade(GrenadeType type)
    {
        if (!grenadeInventory.ContainsKey(type) || grenadeInventory[type] <= 0) return;
        grenadeInventory[type]--;
        currentWeight = Mathf.Max(0, currentWeight - 5);
        OnGrenadeUpdated?.Invoke(type, grenadeInventory[type]);
        OnBagUpdated?.Invoke();
    }

    public void DropGrenade(GrenadeType type, InventoryItemData data)
    {
        if (!grenadeInventory.ContainsKey(type) || grenadeInventory[type] <= 0) return;
        grenadeInventory[type]--;
        if (data != null) currentWeight = Mathf.Max(0, currentWeight - data.weight);
        SpawnPickup(data, 1);
        OnGrenadeUpdated?.Invoke(type, grenadeInventory[type]);
        OnBagUpdated?.Invoke();
    }

    public int GetGrenadeCount(GrenadeType type)
    {
        return grenadeInventory.ContainsKey(type) ? grenadeInventory[type] : 0;
    }

    public void EquipGrenade(GrenadeType type)
    {
        activeGrenadeType = type;
        OnGrenadeUpdated?.Invoke(type, GetGrenadeCount(type));
        OnBagUpdated?.Invoke();
        Debug.Log($"[BagManager] Active grenade set to: {type}");
    }

    public GameObject GetActiveGrenadePrefab()
    {
        Debug.Log($"[BagManager] GetActiveGrenadePrefab called. Active Grenade Type: {activeGrenadeType}");
        if (allItemData == null || allItemData.Count == 0)
        {
            Debug.LogError("[BagManager] allItemData list is empty or null! Please populate it in the Inspector.");
            return null;
        }

        var data = allItemData.Find(x => x.itemType == ItemType.Grenade && x.grenadeType == activeGrenadeType);
        if (data != null)
        {
            GameObject result = data.projectilePrefab != null ? data.projectilePrefab : data.prefab;
            Debug.Log($"[BagManager] Found ItemData '{data.itemName}'. Prefab: {(data.prefab != null ? data.prefab.name : "null")}, ProjectilePrefab: {(data.projectilePrefab != null ? data.projectilePrefab.name : "null")}. Returning: {(result != null ? result.name : "null")}");
            return result;
        }

        Debug.LogWarning($"[BagManager] No matching InventoryItemData found for itemType=Grenade and grenadeType={activeGrenadeType} inside allItemData. Current registry contents:");
        foreach (var item in allItemData)
        {
            Debug.Log($" - '{item.itemName}' (Type: {item.itemType}, GrenadeType: {item.grenadeType})");
        }
        return null;
    }

    public GameObject GetGrenadePrefabByType(GrenadeType type)
    {
        if (allItemData == null) return null;
        var data = allItemData.Find(x => x.itemType == ItemType.Grenade && x.grenadeType == type);
        if (data != null)
        {
            return data.projectilePrefab != null ? data.projectilePrefab : data.prefab;
        }
        return null;
    }

    public void DropMedikit(InventoryItemData data)
    {
        if (medikitCount <= 0) return;
        medikitCount--;
        if (data != null) currentWeight = Mathf.Max(0, currentWeight - data.weight);
        SpawnPickup(data, 1);
        OnMedikitUpdated?.Invoke(medikitCount);
        OnBagUpdated?.Invoke();
    }

    public void DropProteinShake(InventoryItemData data)
    {
        if (proteinShakeCount <= 0) return;
        proteinShakeCount--;
        if (data != null) currentWeight = Mathf.Max(0, currentWeight - data.weight);
        SpawnPickup(data, 1);
        OnProteinShakeUpdated?.Invoke(proteinShakeCount);
        OnBagUpdated?.Invoke();
    }

    public void DropScope(InventoryItemData data)
    {
        if (scopeCount <= 0) return;
        scopeCount--;
        if (data != null) currentWeight = Mathf.Max(0, currentWeight - data.weight);
        SpawnPickup(data, 1);
        OnScopeUpdated?.Invoke(scopeCount);
        OnBagUpdated?.Invoke();
    }

    // ─── Scopes ──────────────────────────────────────────────────────────────

    private WeaponController WC => weaponController != null ? weaponController : WeaponController.Instance;

    public bool AddScope(int amount, int weight)
    {
        // First try to auto-equip to the current weapon
        var wc = WC;
        if (wc != null)
        {
            var currentWep = GetWeaponInSlot(GetCurrentWeaponIndex());
            if (currentWep != null && currentWep.supportsScope && !currentWep.hasScope)
            {
                currentWep.hasScope = true;
                currentWep.AttachScope();
                wc.CheckZoom();
                Debug.Log($"[BagManager] Auto-equipped Scope to {currentWep.weaponName}");
                amount--;
                if (amount <= 0) return true;
            }
        }

        // Remaining scopes go into the bag
        if (amount > 0 && CanAddItem(weight))
        {
            scopeCount    += amount;
            currentWeight += weight;
            OnScopeUpdated?.Invoke(scopeCount);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }

    public bool TryGetScopeFromBag()
    {
        if (scopeCount <= 0) return false;
        scopeCount--;
        currentWeight = Mathf.Max(0, currentWeight - 2);
        OnScopeUpdated?.Invoke(scopeCount);
        OnBagUpdated?.Invoke();
        return true;
    }

    // ─── Consumables ─────────────────────────────────────────────────────────

    public bool AddMedikit(int amount, int weight)
    {
        if (!CanAddItem(weight)) return false;
        medikitCount  += amount;
        currentWeight += weight;
        OnMedikitUpdated?.Invoke(medikitCount);
        OnBagUpdated?.Invoke();
        return true;
    }

    public bool AddProteinShake(int amount, int weight)
    {
        if (!CanAddItem(weight)) return false;
        proteinShakeCount += amount;
        currentWeight     += weight;
        OnProteinShakeUpdated?.Invoke(proteinShakeCount);
        OnBagUpdated?.Invoke();
        return true;
    }

    public void UseMedikit()
    {
        if (medikitCount <= 0) return;
        var health = PlayerHealth.Instance;
        if (health == null || health.GetCurrentHealth() >= health.GetMaxHealth()) return;
        medikitCount--;
        health.Heal(25);
        OnMedikitUpdated?.Invoke(medikitCount);
        OnBagUpdated?.Invoke();
    }

    public void UseProteinShake()
    {
        if (proteinShakeCount <= 0) return;
        var player = FindObjectOfType<PlayerController>();
        if (player == null) return;
        proteinShakeCount--;
        player.ApplySpeedBoost(1.5f, 5f);
        OnProteinShakeUpdated?.Invoke(proteinShakeCount);
        OnBagUpdated?.Invoke();
    }

    // ─── Weapons ─────────────────────────────────────────────────────────────

    public bool TryAddWeapon(GameObject weaponPrefab, InventoryItemData data)
    {
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[BagManager] Cannot add weapon: Prefab is null for item {data?.itemName}");
            return false;
        }

        var wc = WC;
        if (wc == null)
        {
            Debug.LogError("[BagManager] WeaponController not found — cannot add weapon.");
            return false;
        }

        int weaponWeight = data?.weight ?? 0;
        if (!CanAddItem(weaponWeight))
        {
            Debug.Log($"[BagManager] Bag too heavy for '{data?.itemName}' (weight {weaponWeight}).");
            return false;
        }

        // Find the first empty slot
        for (int i = 0; i < 2; i++)
        {
            if (GetWeaponInSlot(i) == null)
            {
                wc.EquipWeaponToSlot(i, weaponPrefab);
                currentWeight += weaponWeight;
                Debug.Log($"[BagManager] ✅ Equipped '{data?.itemName}' to slot {i} (weight +{weaponWeight} = {currentWeight}).");
                OnBagUpdated?.Invoke();
                return true;
            }
        }
        Debug.Log($"[BagManager] Both slots full — '{data?.itemName}' needs manual swap.");
        return false;
    }

    public void SwapCurrentWeapon(GameObject newWeaponPrefab, InventoryItemData data)
    {
        // Capture which slot is currently active BEFORE dropping clears/changes it.
        // ClearWeaponSlot will switch currentSlot to the OTHER slot if the active one is cleared.
        int targetSlot = GetCurrentWeaponIndex();
        if (targetSlot < 0) targetSlot = 0;

        Debug.Log($"[BagManager] SwapCurrentWeapon: dropping slot {targetSlot}, equipping new weapon.");
        DropWeapon(targetSlot);

        // After ClearWeaponSlot, currentSlot may have changed to the other slot.
        // We always equip the new gun to the slot we just cleared (targetSlot),
        // then explicitly switch back to it.
        WC?.EquipWeaponToSlot(targetSlot, newWeaponPrefab);
        WC?.SwitchToSlot(targetSlot);

        // Add the weight of the new weapon
        int weaponWeight = data != null ? data.weight : 0;
        currentWeight += weaponWeight;

        Debug.Log($"[BagManager] SwapCurrentWeapon: new weapon placed in slot {targetSlot} and activated. Weight +{weaponWeight} = {currentWeight}.");
        OnBagUpdated?.Invoke();
    }


    public void DropWeapon(int slotIndex)
    {
        HandheldWeapon weapon = GetWeaponInSlot(slotIndex);
        if (weapon == null) return;

        InventoryItemData data = weapon.itemData;
        int weaponWeight = data?.weight ?? 0;
        if (data != null) SpawnPickup(data, 1);

        // Deduct weight before clearing the slot
        currentWeight = Mathf.Max(0, currentWeight - weaponWeight);

        // ClearWeaponSlot properly destroys the GameObject and frees the slot
        WC?.ClearWeaponSlot(slotIndex);
        OnBagUpdated?.Invoke();
    }

    // ─── Data Lookups ─────────────────────────────────────────────────────────

    public InventoryItemData GetItemData(AmmoType type)
        => allItemData?.Find(x => x.itemType == ItemType.Ammo && x.ammoType == type);

    public InventoryItemData GetGrenadeData()
        => allItemData?.Find(x => x.itemType == ItemType.Grenade);

    public InventoryItemData GetMedikitData()
        => allItemData?.Find(x => x.itemType == ItemType.Medikit);

    public InventoryItemData GetProteinShakeData()
        => allItemData?.Find(x => x.itemType == ItemType.ProteinShake);

    public InventoryItemData GetScopeData()
        => allItemData?.Find(x => x.itemType == ItemType.Scope);

    public int GetCurrentWeight() => currentWeight;

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private void SpawnPickup(InventoryItemData data, int amount)
    {
        if (data == null)
        {
            Debug.LogError("[BagManager] Cannot spawn pickup: InventoryItemData is null.");
            return;
        }
        if (data.prefab == null)
        {
            Debug.LogError($"[BagManager] Cannot spawn pickup for '{data.itemName}': Prefab is not assigned.");
            return;
        }

        Vector3 spawnPos;
        if (dropPoint != null)
        {
            Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
            spawnPos = dropPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
        }
        else
        {
            Debug.LogWarning("[BagManager] DropPoint not assigned! Using fallback position.");
            float randomX = Random.Range(-dropRadius, dropRadius);
            spawnPos   = transform.position + new Vector3(randomX, -0.5f, 0f);
            spawnPos.z = 0f;
        }

        if (IsSpawned)
        {
            RequestSpawnPickupServerRpc(data.itemName, amount, spawnPos);
        }
        else
        {
            SpawnPickupLocalOnly(data, amount, spawnPos);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnPickupServerRpc(string itemName, int amount, Vector3 position)
    {
        if (allItemData == null) return;
        var data = allItemData.Find(x => x.itemName == itemName);
        if (data == null || data.prefab == null) return;

        GameObject pickupObj = Instantiate(data.prefab, position, Quaternion.identity);

        if (pickupObj.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = pickupObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.5f;
        }

        ItemPickup pickup = pickupObj.GetComponent<ItemPickup>();
        if (pickup == null) pickup = pickupObj.AddComponent<ItemPickup>();
        pickup.itemData   = data;
        pickup.SetNetworkState(amount, true, itemName);

        var netObj = pickupObj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
            Debug.Log($"[BagManager] Server spawned networked pickup: {pickupObj.name} × {amount} at {position}");
        }
    }

    private void SpawnPickupLocalOnly(InventoryItemData data, int amount, Vector3 position)
    {
        GameObject pickupObj = Instantiate(data.prefab, position, Quaternion.identity);

        if (pickupObj.GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = pickupObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.5f;
        }

        ItemPickup pickup = pickupObj.GetComponent<ItemPickup>();
        if (pickup == null) pickup = pickupObj.AddComponent<ItemPickup>();
        pickup.itemData   = data;
        pickup.amount     = amount;
        pickup.wasDropped = true;

        Debug.Log($"[BagManager] Spawned local-only pickup: {pickupObj.name} × {amount} at {position}");
    }
}
