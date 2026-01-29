using UnityEngine;
using System.Collections.Generic;

public class BagManager : MonoBehaviour
{
    public static BagManager Instance;

    [Header("Capacity")]
    public int maxWeight = 100;

    public int currentWeight = 0;
    
    [Header("Drop Settings")]
    public Transform dropPoint; // Assign a child transform on Player for invalid drop location
    public float dropRadius = 0.5f; // Random circle radius for fallback/spread

    // ... (rest of fields)

    private void SpawnPickup(InventoryItemData data, int amount)
    {
        Debug.Log($"[BagManager] Attempting to spawn pickup: {data?.itemName} (Amount: {amount})");

        if (data == null)
        {
            Debug.LogError("[BagManager] Cannot spawn pickup. InventoryItemData passed is NULL!");
            return;
        }

        if (data.prefab == null)
        {
            Debug.LogError($"[BagManager] Cannot spawn pickup for '{data.itemName}'. The 'Prefab' field in its InventoryItemData is NOT assigned!");
            return;
        }

        Vector3 spawnPos;
        if (dropPoint != null)
        {
             // Use Drop Point position with random offset to prevent stacking
             Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
             spawnPos = dropPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
        }
        else
        {
             // Fallback to feet level logic
             Debug.LogWarning("[BagManager] DropPoint not assigned! Using fallback calculation.");
             float randomX = Random.Range(-dropRadius, dropRadius);
             float dropY = -0.5f; 
             spawnPos = transform.position + new Vector3(randomX, dropY, 0f);
             spawnPos.z = 0f;
        }

        Debug.Log($"[BagManager] Spawning at: {spawnPos}");

        GameObject pickupObj = Instantiate(data.prefab, spawnPos, Quaternion.identity);
            
        // Ensure physics setup for pickup
        if (pickupObj.GetComponent<Collider2D>() == null)
        {
            Debug.Log("[BagManager] Adding missing CircleCollider2D to pickup.");
            CircleCollider2D col = pickupObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f; 
        }
            
        ItemPickup pickup = pickupObj.GetComponent<ItemPickup>();
        if (pickup == null) pickup = pickupObj.AddComponent<ItemPickup>();
        pickup.itemData = data;
        pickup.amount = amount;
        pickup.wasDropped = true; // Mark as dropped so it requires manual pickup
        
        Debug.Log($"[BagManager] Successfully spawned {pickupObj.name} at {pickupObj.transform.position}");
    }

    [Header("Weapon Slots")]
    public HandheldWeapon[] weaponSlots = new HandheldWeapon[2];
    public int currentWeaponIndex = -1; // -1 means no weapon equipped

    [Header("Inventory Data Registry")]
    public List<InventoryItemData> allItemData; // Assign in Inspector: All Ammo and Grenade ScriptableObjects

    [Header("Inventory Data")]
    public Dictionary<AmmoType, int> ammoInventory = new Dictionary<AmmoType, int>();

    public int grenadeCount = 0;
    public int scopeCount = 0; // New Scope Inventory
    public int medikitCount = 0;
    public int proteinShakeCount = 0;

    [Header("References")]
    [SerializeField] private WeaponController weaponController;

    // Events
    public System.Action<AmmoType, int> OnAmmoUpdated;

    public System.Action<int> OnGrenadeUpdated;
    public System.Action<int> OnScopeUpdated; // New Event
    public System.Action<int> OnMedikitUpdated;
    public System.Action<int> OnProteinShakeUpdated;
    public System.Action OnBagUpdated;
    public System.Action<int, HandheldWeapon> OnWeaponSlotUpdated;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Initialize ammo dictionary
        foreach (AmmoType type in System.Enum.GetValues(typeof(AmmoType)))
        {
            if (type != AmmoType.None) ammoInventory[type] = 0;
        }
    }

    public bool CanAddItem(int weight)
    {
        return currentWeight + weight <= maxWeight;
    }
    
    // --- Scopes ---
    
    public bool AddScope(int amount, int weight)
    {
        // 1. Try to auto-equip to current weapon
        if (weaponController != null && weaponSlots[weaponController.currentSlot] != null) // Access via slots directly since index is public in BagManager logic for slots usually, or we trust WeaponController currentSlot sync
        {
            int slot = weaponController.currentSlot; // Assuming BagManager tracks via UI or sync. Wait, detailed logic:
            
            // Check current weapon
            var currentWep = (currentWeaponIndex >= 0 && currentWeaponIndex < weaponSlots.Length) ? weaponSlots[currentWeaponIndex] : null;

            if (currentWep != null && currentWep.supportsScope && !currentWep.hasScope)
            {
                currentWep.hasScope = true;
                currentWep.AttachScope(); // Optional visual method
                Debug.Log($"[BagManager] Auto-equipped Scope to {currentWep.weaponName}");
                
                // Trigger Zoom Update immediately
                weaponController.CheckZoom();
                
                // One used, check remaining
                amount--;
                if (amount <= 0) return true;
            }
        }

        // 2. Add remaining to bag
        if (amount > 0 && CanAddItem(weight))
        {
             scopeCount += amount;
             currentWeight += weight;
             OnScopeUpdated?.Invoke(scopeCount);
             OnBagUpdated?.Invoke();
             return true;
        }
        
        return false;
    }
    
    public bool TryGetScopeFromBag()
    {
        if (scopeCount > 0)
        {
            scopeCount--;
            // Weight reduction (assuming 1 scope = 2 weight, ideally get from Data but simplistic here)
            currentWeight = Mathf.Max(0, currentWeight - 2); 
            OnScopeUpdated?.Invoke(scopeCount);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }
    
    // --- End Scopes ---

    public bool AddAmmo(AmmoType type, int amount, int weight)
    {
        if (CanAddItem(weight))
        {
            ammoInventory[type] += amount;
            currentWeight += weight;
            OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }

    public bool AddGrenade(int amount, int weight)
    {
        if (CanAddItem(weight))
        {
            grenadeCount += amount;
            currentWeight += weight;
            OnGrenadeUpdated?.Invoke(grenadeCount);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }

    public void ConsumeAmmo(AmmoType type, int amount)
    {
        if (ammoInventory.ContainsKey(type))
        {
            ammoInventory[type] = Mathf.Max(0, ammoInventory[type] - amount);
            // In this implementation, we don't reduce weight when bullets are fired 
            // to keep it simple, as if weight is per box/bag.
            OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
            OnBagUpdated?.Invoke();
        }
    }

    public void ConsumeGrenade()
    {
        if (grenadeCount > 0)
        {
            grenadeCount--;
            // Weight reduction for grenade? Let's say yes since it's a big item.
            // (Need to know original weight per grenade, let's assume 5 for now)
            currentWeight = Mathf.Max(0, currentWeight - 5); 
            OnGrenadeUpdated?.Invoke(grenadeCount);
            OnBagUpdated?.Invoke();
        }
    }

    public bool TryAddWeapon(GameObject weaponPrefab, InventoryItemData data)
    {
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[BagManager] Cannot add weapon: Prefab is null for item {data?.itemName}");
            return false;
        }

        // 1. Check for empty slot
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null)
            {
                EquipToSlot(i, weaponPrefab);
                return true;
            }
        }

        // 2. If no empty slot, we need manual pickup (handled by UI button usually)
        // This method will be called directly by the Pickup Button later.
        return false;
    }

    public void SwapCurrentWeapon(GameObject newWeaponPrefab)
    {
        int slotToUse = currentWeaponIndex != -1 ? currentWeaponIndex : 0;
        
        // Drop current
        DropWeapon(slotToUse);
        
        // Equip new
        EquipToSlot(slotToUse, newWeaponPrefab);
    }

    private void EquipToSlot(int slotIndex, GameObject prefab)
    {
        // This would interact with WeaponController to actually instantiate the weapon
        if (weaponController != null)
        {
            weaponController.EquipWeaponToSlot(slotIndex, prefab);
            // WeaponController will call back or we set the slot reference here if it's external
        }
    }

    public void SetWeaponInSlot(int slotIndex, HandheldWeapon weapon)
    {
        weaponSlots[slotIndex] = weapon;
        
        // Check Auto-Equip Scope from Bag
        if (weapon != null && weapon.supportsScope && !weapon.hasScope && scopeCount > 0)
        {
            if (TryGetScopeFromBag())
            {
                weapon.hasScope = true;
                weapon.AttachScope(); // Optional visual
                Debug.Log($"[BagManager] Auto-equipped Scope from bag to new weapon {weapon.weaponName}");
                
                // If this is the active weapon, update zoom
                if (currentWeaponIndex == slotIndex && weaponController != null)
                {
                   weaponController.CheckZoom();
                }
            }
        }
        
        OnWeaponSlotUpdated?.Invoke(slotIndex, weapon);
    }

    public InventoryItemData GetItemData(AmmoType type)
    {
        if (allItemData == null) return null;
        return allItemData.Find(x => x.itemType == ItemType.Ammo && x.ammoType == type);
    }

    public InventoryItemData GetGrenadeData()
    {
        if (allItemData == null) return null;
        return allItemData.Find(x => x.itemType == ItemType.Grenade);
    }

    public int GetAmmo(AmmoType type)
    {
        return ammoInventory.ContainsKey(type) ? ammoInventory[type] : 0;
    }

    public void DropAmmo(AmmoType type, InventoryItemData data, int amount)
    {
        if (ammoInventory.ContainsKey(type) && ammoInventory[type] >= amount)
        {
            ammoInventory[type] -= amount;
            
            // Reduce weight
            if (data != null)
            {
                currentWeight = Mathf.Max(0, currentWeight - (data.weight * amount));
            }

            // Spawn in world
            SpawnPickup(data, amount);
            OnAmmoUpdated?.Invoke(type, ammoInventory[type]);
            OnBagUpdated?.Invoke();
        }
    }

    public void DropGrenade(InventoryItemData data)
    {
        if (grenadeCount > 0)
        {
            grenadeCount--;

            // Reduce weight
            if (data != null)
            {
                currentWeight = Mathf.Max(0, currentWeight - data.weight);
            }

            SpawnPickup(data, 1);
            OnGrenadeUpdated?.Invoke(grenadeCount);
            OnBagUpdated?.Invoke();
        }
    }

    public void DropWeapon(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < weaponSlots.Length && weaponSlots[slotIndex] != null)
        {
            InventoryItemData data = weaponSlots[slotIndex].itemData;
            if (data != null) SpawnPickup(data, 1);
            
            // Cleanup slot
            UnsubscribeFromWeapon(weaponSlots[slotIndex]);
            Destroy(weaponSlots[slotIndex].gameObject);
            weaponSlots[slotIndex] = null;
            
            OnWeaponSlotUpdated?.Invoke(slotIndex, null);
            OnBagUpdated?.Invoke();
        }
    }



    private void UnsubscribeFromWeapon(HandheldWeapon weapon)
    {
        // WeaponController handles this usually, but we need to make sure we don't have dangling events
        // (Better to have WeaponController handle the actual Destroy)
    }

    public bool AddMedikit(int amount, int weight)
    {
        if (CanAddItem(weight))
        {
            medikitCount += amount;
            currentWeight += weight;
            OnMedikitUpdated?.Invoke(medikitCount);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }

    public bool AddProteinShake(int amount, int weight)
    {
        if (CanAddItem(weight))
        {
            proteinShakeCount += amount;
            currentWeight += weight;
            OnProteinShakeUpdated?.Invoke(proteinShakeCount);
            OnBagUpdated?.Invoke();
            return true;
        }
        return false;
    }

    public void UseMedikit()
    {
        if (medikitCount > 0)
        {
            var health = PlayerHealth.Instance;
            if (health != null && health.GetCurrentHealth() < health.GetMaxHealth())
            {
                medikitCount--;
                // Simplification: ignore weight reduction on use for now or needs granular tracking
                // assuming 0 weight or handled simply
                health.Heal(25); 
                OnMedikitUpdated?.Invoke(medikitCount);
                OnBagUpdated?.Invoke();
            }
        }
    }

    public void UseProteinShake()
    {
        if (proteinShakeCount > 0)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                proteinShakeCount--;
                player.ApplySpeedBoost(1.5f, 5f);
                OnProteinShakeUpdated?.Invoke(proteinShakeCount);
                OnBagUpdated?.Invoke();
            }
        }
    }

    public int GetCurrentWeight() => currentWeight;
}
