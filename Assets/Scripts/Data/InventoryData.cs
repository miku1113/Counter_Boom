using UnityEngine;

public enum AmmoType { None, Type1, Type2, Type3 }
public enum ItemType { Weapon, Ammo, Grenade, Medikit, ProteinShake, Scope }
public enum GrenadeType { None, Explosive, Stun, Smoke }

[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]
public class InventoryItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public AmmoType ammoType;       // Which ammo this weapon uses or which type this ammo box is
    public GrenadeType grenadeType; // Which type of grenade this is (only if itemType is Grenade)
    public int weight;
    public GameObject prefab;       // The world pickup prefab / drop visual
    public GameObject projectilePrefab; // The projectile prefab to instantiate when throwing (only if Grenade)
    public Sprite icon;
}
