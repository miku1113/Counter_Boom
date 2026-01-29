using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public InventoryItemData itemData;
    public int amount = 1;

    public static ItemPickup NearestPickup;

    public bool wasDropped = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (itemData == null)
        {
            Debug.LogError($"[ItemPickup] ItemData is missing on {gameObject.name}");
            return;
        }

        if (other.CompareTag("Player"))
        {
            // If it was dropped, we FORCE manual pickup for EVERYTHING (Weapon, Ammo, Grenade)
            if (wasDropped)
            {
                Debug.Log($"[ItemPickup] Item {itemData.itemName} was dropped. Manual pickup required.");
                NearestPickup = this;
                return;
            }

            if (itemData.itemType == ItemType.Weapon)
            {
                Debug.Log($"[ItemPickup] Attempting to auto-pickup weapon: {itemData.itemName}");
                // For weapons, we check if we can auto-pickup or need manual
                if (BagManager.Instance != null && BagManager.Instance.TryAddWeapon(itemData.prefab, itemData))
                {
                    Destroy(gameObject);
                }
                else
                {
                    // Need manual pickup (slots full)
                    NearestPickup = this;
                    Debug.Log($"[ItemPickup] Slots full for {itemData.itemName}. Enabled manual pickup.");
                }
            }
            else
            {
                // Auto pickup for ammo/grenades
                if (BagManager.Instance != null)
                {
                    bool pickedUp = false;
                    if (itemData.itemType == ItemType.Ammo)
                        pickedUp = BagManager.Instance.AddAmmo(itemData.ammoType, amount, itemData.weight * amount);
                    else if (itemData.itemType == ItemType.Grenade)
                        pickedUp = BagManager.Instance.AddGrenade(amount, itemData.weight * amount);
                    else if (itemData.itemType == ItemType.Medikit)
                        pickedUp = BagManager.Instance.AddMedikit(amount, itemData.weight * amount);
                    else if (itemData.itemType == ItemType.ProteinShake)
                        pickedUp = BagManager.Instance.AddProteinShake(amount, itemData.weight * amount);
                    else if (itemData.itemType == ItemType.Scope)
                        pickedUp = BagManager.Instance.AddScope(amount, itemData.weight * amount);

                    if (pickedUp) Destroy(gameObject);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (NearestPickup == this) NearestPickup = null;
        }
    }

    public void PickingUpManually()
    {
        if (BagManager.Instance != null)
        {
            bool success = false;
            
            if (itemData.itemType == ItemType.Weapon)
            {
                BagManager.Instance.SwapCurrentWeapon(itemData.prefab);
                success = true; // Swap always succeeds (drops current)
            }
            else if (itemData.itemType == ItemType.Ammo)
            {
                success = BagManager.Instance.AddAmmo(itemData.ammoType, amount, itemData.weight * amount);
            }
            else if (itemData.itemType == ItemType.Grenade)
            {
                success = BagManager.Instance.AddGrenade(amount, itemData.weight * amount);
            }
            else if (itemData.itemType == ItemType.Medikit)
            {
                success = BagManager.Instance.AddMedikit(amount, itemData.weight * amount);
            }
            else if (itemData.itemType == ItemType.ProteinShake)
            {
                success = BagManager.Instance.AddProteinShake(amount, itemData.weight * amount);
            }
            else if (itemData.itemType == ItemType.Scope)
            {
                success = BagManager.Instance.AddScope(amount, itemData.weight * amount);
            }

            if (success)
            {
                Destroy(gameObject);
                if (NearestPickup == this) NearestPickup = null;
            }
        }
    }
}
