using UnityEngine;

public enum AmmoType { None, Type1, Type2, Type3 }
public enum ItemType { Weapon, Ammo, Grenade, Medikit, ProteinShake, Scope }

[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]
public class InventoryItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public AmmoType ammoType; // Which ammo this weapon uses or which type this ammo box is
    public int weight;
    public GameObject prefab;
    public Sprite icon;
}
