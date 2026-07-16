using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public InventoryItemData itemData;
    public int  amount     = 1;
    public bool wasDropped = false;

    // Currently closest pickup to the player (read by HUDManager to show Pickup button)
    public static ItemPickup NearestPickup;

    // ─── Trigger ─────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (itemData == null)
        {
            Debug.LogError($"[ItemPickup] ⚠️ ItemData is null on '{gameObject.name}'. " +
                           "Assign an InventoryItemData to the prefab or the spawned instance.");
            return;
        }

        // Dropped items always require a manual press
        if (wasDropped)
        {
            NearestPickup = this;
            Debug.Log($"[ItemPickup] '{itemData.itemName}' was dropped — manual pickup required.");
            return;
        }

        switch (itemData.itemType)
        {
            case ItemType.Weapon:
                TryAutoPickupWeapon();
                break;

            case ItemType.Ammo:
                if (BagManager.Instance != null &&
                    BagManager.Instance.AddAmmo(itemData.ammoType, amount, itemData.weight * amount))
                    Destroy(gameObject);
                break;

            case ItemType.Grenade:
                if (BagManager.Instance != null &&
                    BagManager.Instance.AddGrenade(amount, itemData.weight * amount))
                    Destroy(gameObject);
                break;

            case ItemType.Medikit:
                if (BagManager.Instance != null &&
                    BagManager.Instance.AddMedikit(amount, itemData.weight * amount))
                    Destroy(gameObject);
                break;

            case ItemType.ProteinShake:
                if (BagManager.Instance != null &&
                    BagManager.Instance.AddProteinShake(amount, itemData.weight * amount))
                    Destroy(gameObject);
                break;

            case ItemType.Scope:
                if (BagManager.Instance != null &&
                    BagManager.Instance.AddScope(amount, itemData.weight * amount))
                    Destroy(gameObject);
                break;
        }
    }

    private void TryAutoPickupWeapon()
    {
        Debug.Log($"[ItemPickup] Attempting to auto-pickup weapon: {itemData.itemName}");

        if (itemData.prefab == null)
        {
            Debug.LogError($"[ItemPickup] ⚠️ '{itemData.itemName}' has no Prefab assigned in its InventoryItemData. " +
                           "Cannot equip. Assign the weapon GameObject prefab.");
            return;
        }

        if (BagManager.Instance == null)
        {
            Debug.LogError("[ItemPickup] BagManager.Instance is null — cannot pick up weapon.");
            return;
        }

        bool added = BagManager.Instance.TryAddWeapon(itemData.prefab, itemData);
        if (added)
        {
            Debug.Log($"[ItemPickup] ✅ Auto-equipped '{itemData.itemName}'.");
            Destroy(gameObject);
        }
        else
        {
            // Slots full — show Pickup button so player can swap
            NearestPickup = this;
            Debug.Log($"[ItemPickup] Slots full for '{itemData.itemName}'. Stand here and press Pickup to swap.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (NearestPickup == this)
        {
            NearestPickup = null;
            Debug.Log($"[ItemPickup] Left '{itemData?.itemName}' — NearestPickup cleared.");
        }
    }

    // ─── Manual Pickup (called by HUD Pickup button) ──────────────────────────

    public void PickingUpManually()
    {
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
                // Swap drops the current active weapon and equips this one
                BagManager.Instance.SwapCurrentWeapon(itemData.prefab);
                success = true;
                Debug.Log($"[ItemPickup] ✅ Swapped current weapon for '{itemData.itemName}'.");
                break;

            case ItemType.Ammo:
                success = BagManager.Instance.AddAmmo(itemData.ammoType, amount, itemData.weight * amount);
                break;

            case ItemType.Grenade:
                success = BagManager.Instance.AddGrenade(amount, itemData.weight * amount);
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
            if (NearestPickup == this) NearestPickup = null;
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"[ItemPickup] Manual pickup failed for '{itemData.itemName}' " +
                             $"(bag full or other error).");
        }
    }
}
