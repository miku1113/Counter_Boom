using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BagUI : MonoBehaviour
{
    public static BagUI Instance;

    [Header("UI Panels")]
    public RectTransform sideWindow;
    public Transform     itemContainer;
    public TMPro.TextMeshProUGUI weightText;
    public Button        closeButton;

    [Header("Prefabs")]
    public GameObject itemSlotPrefab;

    [Header("Icons (Fallback)")]
    public Sprite weaponIcon;
    public Sprite ammoIcon;
    public Sprite grenadeIcon;
    public Sprite medikitIcon;
    public Sprite proteinShakeIcon;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (sideWindow != null) sideWindow.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (BagManager.Instance != null)
            BagManager.Instance.OnBagUpdated += RefreshUI;

        if (WeaponController.Instance != null)
            WeaponController.Instance.OnWeaponSlotUpdated += _ => RefreshUIIfOpen();

        if (closeButton != null)
            closeButton.onClick.AddListener(ToggleBag);
    }

    private void OnDestroy()
    {
        if (BagManager.Instance != null)
            BagManager.Instance.OnBagUpdated -= RefreshUI;

        if (WeaponController.Instance != null)
            WeaponController.Instance.OnWeaponSlotUpdated -= _ => RefreshUIIfOpen();
    }

    public void ToggleBag()
    {
        if (sideWindow == null) return;
        bool newState = !sideWindow.gameObject.activeSelf;
        sideWindow.gameObject.SetActive(newState);
        if (newState) RefreshUI();
    }

    private void RefreshUIIfOpen()
    {
        if (sideWindow != null && sideWindow.gameObject.activeSelf)
            RefreshUI();
    }

    public void RefreshUI()
    {
        if (itemContainer == null)
        {
            Debug.LogError("[BagUI] itemContainer is not assigned! Cannot render bag items.");
            return;
        }

        if (itemSlotPrefab == null)
        {
            Debug.LogError("[BagUI] itemSlotPrefab is not assigned! Assign a slot prefab in the Inspector.");
            return;
        }

        // Clear existing slots
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        if (BagManager.Instance == null)
        {
            Debug.LogWarning("[BagUI] BagManager.Instance is null — nothing to show.");
            return;
        }

        int totalSlots = 0;

        // ── 1. Equipped Weapons ────────────────────────────────────────────────
        // Weapons live in WeaponController slots, not BagManager fields.
        // Show them here so the bag doesn't look empty after picking up guns.
        for (int i = 0; i < 2; i++)
        {
            HandheldWeapon w = BagManager.Instance.GetWeaponInSlot(i);
            if (w == null) continue;

            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot   = slotObj.GetComponent<BagItemSlot>();
            if (slot == null) continue;

            // Build display data from the weapon's own InventoryItemData if available,
            // otherwise create a temporary placeholder.
            InventoryItemData data = w.itemData;
            if (data == null)
            {
                data          = ScriptableObject.CreateInstance<InventoryItemData>();
                data.itemName = w.weaponName;
                data.itemType = ItemType.Weapon;
                data.icon     = weaponIcon;
            }

            slot.weaponSlotIndex = i;
            slot.Setup(data, 1);
            totalSlots++;
        }

        // ── 2. Ammo ────────────────────────────────────────────────────────────
        foreach (var pair in BagManager.Instance.ammoInventory)
        {
            if (pair.Value <= 0) continue;

            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot   = slotObj.GetComponent<BagItemSlot>();
            if (slot == null) continue;

            InventoryItemData data = BagManager.Instance.GetItemData(pair.Key);
            if (data == null)
            {
                data          = ScriptableObject.CreateInstance<InventoryItemData>();
                data.itemName = pair.Key.ToString() + " Ammo";
                data.itemType = ItemType.Ammo;
                data.ammoType = pair.Key;
                data.icon     = ammoIcon;
            }

            slot.Setup(data, pair.Value);
            totalSlots++;
        }

        // ── 3. Grenades ────────────────────────────────────────────────────────
        if (BagManager.Instance.grenadeCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot   = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                InventoryItemData data = BagManager.Instance.GetGrenadeData();
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Grenade";
                    data.itemType = ItemType.Grenade;
                    data.icon     = grenadeIcon;
                }
                slot.Setup(data, BagManager.Instance.grenadeCount);
                totalSlots++;
            }
        }

        // ── 4. Medikit ─────────────────────────────────────────────────────────
        if (BagManager.Instance.medikitCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot   = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                InventoryItemData data = BagManager.Instance.GetMedikitData();
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Medikit";
                    data.itemType = ItemType.Medikit;
                    data.icon     = medikitIcon;
                }
                slot.Setup(data, BagManager.Instance.medikitCount);
                totalSlots++;
            }
        }

        // ── 5. Protein Shake ───────────────────────────────────────────────────
        if (BagManager.Instance.proteinShakeCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot   = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                InventoryItemData data = BagManager.Instance.GetProteinShakeData();
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Protein Shake";
                    data.itemType = ItemType.ProteinShake;
                    data.icon     = proteinShakeIcon;
                }
                slot.Setup(data, BagManager.Instance.proteinShakeCount);
                totalSlots++;
            }
        }

        // ── 6. Weight display ──────────────────────────────────────────────────
        if (weightText != null)
            weightText.text = $"Weight: {BagManager.Instance.currentWeight}/{BagManager.Instance.maxWeight}";

        if (totalSlots == 0)
            Debug.Log("[BagUI] Bag is genuinely empty — no weapons, ammo, or consumables picked up yet.");
        else
            Debug.Log($"[BagUI] Rendered {totalSlots} item slot(s).");
    }
}
