using UnityEngine;
using Unity.Netcode;

public class ItemPickup : NetworkBehaviour
{
    public InventoryItemData itemData;
    public int  amount     = 1;
    public bool wasDropped = false;
    private float dropCooldown = 0.5f;
    private float spawnTime = 0f;

    private void Awake()
    {
        spawnTime = Time.time;
    }

    [Header("Audio Clips")]
    public AudioClip pickupSound;
    public AudioClip dropSound;

    private readonly NetworkVariable<int> netAmount = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<bool> netWasDropped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private readonly NetworkVariable<Unity.Collections.FixedString32Bytes> netItemName = new NetworkVariable<Unity.Collections.FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void SetNetworkState(int spawnAmount, bool dropped, string itemName)
    {
        if (IsServer)
        {
            netAmount.Value = spawnAmount;
            netWasDropped.Value = dropped;
            netItemName.Value = itemName;
        }
        amount = spawnAmount;
        wasDropped = dropped;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        amount = netAmount.Value;
        wasDropped = netWasDropped.Value;

        if (itemData == null && !string.IsNullOrEmpty(netItemName.Value.ToString()))
        {
            ResolveItemData(netItemName.Value.ToString());
        }

        netAmount.OnValueChanged += OnAmountChanged;
        netWasDropped.OnValueChanged += OnWasDroppedChanged;
        netItemName.OnValueChanged += OnItemNameChanged;
    }

    public override void OnNetworkDespawn()
    {
        netAmount.OnValueChanged -= OnAmountChanged;
        netWasDropped.OnValueChanged -= OnWasDroppedChanged;
        netItemName.OnValueChanged -= OnItemNameChanged;
        base.OnNetworkDespawn();
    }

    private void OnAmountChanged(int oldVal, int newVal) => amount = newVal;
    private void OnWasDroppedChanged(bool oldVal, bool newVal) => wasDropped = newVal;
    private void OnItemNameChanged(Unity.Collections.FixedString32Bytes oldVal, Unity.Collections.FixedString32Bytes newVal) => ResolveItemData(newVal.ToString());

    private void ResolveItemData(string name)
    {
        if (BagManager.Instance != null && BagManager.Instance.allItemData != null)
        {
            itemData = BagManager.Instance.allItemData.Find(x => x.itemName == name);
        }
    }
    
    // Static lists to track active pickups in player's range
    public static System.Collections.Generic.List<ItemPickup> PickupsInRange = new System.Collections.Generic.List<ItemPickup>();

    // Currently closest pickup to the player (read by HUDManager to show Pickup button)
    public static ItemPickup NearestPickup;

    // ─── Trigger ─────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isPlayer = other.CompareTag("Player");
        bool isBot = other.CompareTag("Bot") || other.GetComponent<AiBotController>() != null;

        if (!isPlayer && !isBot) return;

        // Ghosts & dead entities cannot pick up items
        var playerHealth = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead) return;

        var playerCtrl = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
        if (playerCtrl != null && playerCtrl.IsGhost) return;

        // For Human Player: check isLocal
        if (isPlayer)
        {
            bool isLocal = true;
            if (playerCtrl != null)
            {
                isLocal = playerCtrl.IsLocal;
            }
            else
            {
                var netObj = other.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    isLocal = netObj.IsLocalPlayer || netObj.IsOwner;
                }
            }
            if (!isLocal) return;

            if (!PickupsInRange.Contains(this))
            {
                PickupsInRange.Add(this);
                Debug.Log($"[ItemPickup] Player entered range of '{itemData?.itemName}'. Total in range: {PickupsInRange.Count}");
            }
        }

        if (itemData == null)
        {
            Debug.LogError($"[ItemPickup] ⚠️ ItemData is null on '{gameObject.name}'. " +
                           "Assign an InventoryItemData to the prefab or the spawned instance.");
            return;
        }

        // Retrieve the collecting entity's BagManager and WeaponController
        BagManager collectorBag = isPlayer ? BagManager.Instance : (other.GetComponent<BagManager>() ?? other.GetComponentInParent<BagManager>());
        WeaponController collectorWc = isPlayer ? WeaponController.Instance : (other.GetComponent<WeaponController>() ?? other.GetComponentInParent<WeaponController>());

        // If dropped less than 0.5s ago, prevent instant auto-vacuum on the drop frame
        if (wasDropped && isPlayer && (Time.time - spawnTime < dropCooldown))
        {
            NearestPickup = this;
            return;
        }

        bool success = false;
        switch (itemData.itemType)
        {
            case ItemType.Weapon:
                if (isBot)
                {
                    if (collectorWc != null && itemData.prefab != null)
                    {
                        int slot = collectorWc.GetWeaponInSlot(0) == null ? 0 : (collectorWc.GetWeaponInSlot(1) == null ? 1 : 0);
                        collectorWc.EquipWeaponToSlot(slot, itemData.prefab);
                        collectorWc.SwitchToSlot(slot);
                        if (collectorBag != null)
                        {
                            collectorBag.AddAmmo(itemData.ammoType, 90, 0);
                        }
                        success = true;
                    }
                }
                else
                {
                    success = TryAutoPickupWeapon(collectorBag);
                }
                break;

            case ItemType.Ammo:
                if (collectorBag != null)
                    success = collectorBag.AddAmmo(itemData.ammoType, amount, itemData.weight * amount);
                break;

            case ItemType.Grenade:
                if (collectorBag != null)
                    success = collectorBag.AddGrenade(itemData.grenadeType, amount, itemData.weight * amount);
                break;

            case ItemType.Medikit:
                if (collectorBag != null)
                    success = collectorBag.AddMedikit(amount, itemData.weight * amount);
                break;

            case ItemType.ProteinShake:
                if (collectorBag != null)
                    success = collectorBag.AddProteinShake(amount, itemData.weight * amount);
                break;

            case ItemType.Scope:
                if (collectorBag != null)
                    success = collectorBag.AddScope(amount, itemData.weight * amount);
                break;
        }

        if (success)
        {
            TriggerDespawn();
        }
    }

    private bool TryAutoPickupWeapon(BagManager targetBag = null)
    {
        Debug.Log($"[ItemPickup] Attempting to auto-pickup weapon: {itemData.itemName}");

        if (itemData.prefab == null)
        {
            Debug.LogError($"[ItemPickup] ⚠️ '{itemData.itemName}' has no Prefab assigned in its InventoryItemData. " +
                           "Cannot equip. Assign the weapon GameObject prefab.");
            return false;
        }

        if (targetBag == null) targetBag = BagManager.Instance;

        if (targetBag == null)
        {
            Debug.LogError("[ItemPickup] BagManager is null — cannot pick up weapon.");
            return false;
        }

        bool added = targetBag.TryAddWeapon(itemData.prefab, itemData);
        if (added)
        {
            Debug.Log($"[ItemPickup] ✅ Auto-equipped '{itemData.itemName}'.");
            return true;
        }
        else
        {
            // Slots full — show Pickup button so player can swap
            NearestPickup = this;
            Debug.Log($"[ItemPickup] Slots full for '{itemData.itemName}'. Stand here and press Pickup to swap.");
            return false;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var playerCtrl = other.GetComponent<PlayerController>();
        if (playerCtrl == null) playerCtrl = other.GetComponentInParent<PlayerController>();

        bool isLocal = true;
        if (playerCtrl != null)
        {
            isLocal = playerCtrl.IsLocal;
        }
        else
        {
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                isLocal = netObj.IsLocalPlayer || netObj.IsOwner;
            }
        }
        if (!isLocal) return;
        
        if (PickupsInRange.Contains(this))
        {
            PickupsInRange.Remove(this);
        }
        
        if (NearestPickup == this)
        {
            NearestPickup = null;
            Debug.Log($"[ItemPickup] Left '{itemData?.itemName}' — NearestPickup cleared.");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (PickupsInRange.Contains(this))
        {
            PickupsInRange.Remove(this);
        }
        
        if (NearestPickup == this)
        {
            NearestPickup = null;
        }
    }

    // ─── Manual Pickup (called by HUD Pickup button) ──────────────────────────

    public void PickingUpManually()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

        Debug.Log($"[ItemPickup] PickingUpManually() called for '{itemData?.itemName}'.");

        if (BagManager.Instance == null)
        {
            Debug.LogError("[ItemPickup] BagManager.Instance is null — cannot pick up manually.");
            return;
        }

        if (itemData == null)
        {
            Debug.LogError($"[ItemPickup] itemData is null on '{gameObject.name}'.");
            return;
        }

        bool success = false;

        switch (itemData.itemType)
        {
            case ItemType.Weapon:
                if (itemData.prefab == null)
                {
                    Debug.LogError($"[ItemPickup] ⚠️ '{itemData.itemName}' prefab is null — cannot swap.");
                    return;
                }
                // Try to add the weapon first. If a slot is empty, it will be placed there.
                // If slots are full, SwapCurrentWeapon will be called to replace the active weapon.
                if (BagManager.Instance.TryAddWeapon(itemData.prefab, itemData))
                {
                    success = true;
                    Debug.Log($"[ItemPickup] ✅ Added '{itemData.itemName}' to an empty slot.");
                }
                else
                {
                    // Swap drops the current active weapon and equips this one
                    BagManager.Instance.SwapCurrentWeapon(itemData.prefab, itemData);
                    success = true;
                    Debug.Log($"[ItemPickup] ✅ Swapped current weapon for '{itemData.itemName}'.");
                }
                break;


            case ItemType.Ammo:
                success = BagManager.Instance.AddAmmo(itemData.ammoType, amount, itemData.weight * amount);
                break;

            case ItemType.Grenade:
                success = BagManager.Instance.AddGrenade(itemData.grenadeType, amount, itemData.weight * amount);
                break;

            case ItemType.Medikit:
                success = BagManager.Instance.AddMedikit(amount, itemData.weight * amount);
                break;

            case ItemType.ProteinShake:
                success = BagManager.Instance.AddProteinShake(amount, itemData.weight * amount);
                break;

            case ItemType.Scope:
                success = BagManager.Instance.AddScope(amount, itemData.weight * amount);
                break;
        }

        if (success)
        {
            TriggerDespawn();
        }
        else
        {
            Debug.LogWarning($"[ItemPickup] Manual pickup failed for '{itemData.itemName}' " +
                             $"(bag full or other error).");
        }
    }

    public void PlayPickupAudio()
    {
        AudioClip clipToPlay = pickupSound != null ? pickupSound : (itemData != null ? itemData.pickupSound : null);
        if (PlayerController.LocalPlayer != null)
        {
            PlayerController.LocalPlayer.PlayPickupSound(clipToPlay);
        }
    }

    public void TriggerDespawn()
    {
        PlayPickupAudio();

        if (PickupsInRange.Contains(this))
            PickupsInRange.Remove(this);
        if (NearestPickup == this)
            NearestPickup = null;

        if (IsSpawned)
        {
            if (IsServer)
            {
                GetComponent<NetworkObject>().Despawn(true);
            }
            else
            {
                RequestDespawnServerRpc();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
    }
}
