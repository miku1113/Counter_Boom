using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BagUI : MonoBehaviour
{
    public static BagUI Instance;

    [Header("UI Panels")]
    public RectTransform sideWindow;
    public Transform itemContainer;
    public TMPro.TextMeshProUGUI weightText;
    public Button closeButton;

    [Header("Prefabs")]
    public GameObject itemSlotPrefab;

    [Header("Icons (Fallback)")]
    public Sprite ammoIcon;
    public Sprite grenadeIcon;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        if (sideWindow != null) sideWindow.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnBagUpdated += RefreshUI;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ToggleBag);
        }
    }

    private void OnDestroy()
    {
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnBagUpdated -= RefreshUI;
        }
    }

    public void ToggleBag()
    {
        if (sideWindow != null)
        {
            bool newState = !sideWindow.gameObject.activeSelf;
            sideWindow.gameObject.SetActive(newState);
            if (newState) RefreshUI();
        }
    }

    public void RefreshUI()
    {
        if (itemContainer == null) return;

        // Clear old slots
        foreach (Transform child in itemContainer)
        {
            Destroy(child.gameObject);
        }

        if (BagManager.Instance == null) return;

        // 1. Show Ammo
        foreach (var pair in BagManager.Instance.ammoInventory)
        {
            if (pair.Value > 0)
            {
                GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
                BagItemSlot slot = slotObj.GetComponent<BagItemSlot>();
                if (slot != null)
                {
                    // Try to get real data from BagManager
                    InventoryItemData data = BagManager.Instance.GetItemData(pair.Key);
                    
                    if (data == null)
                    {
                        // Fallback (will lack prefab for dropping!)
                        data = ScriptableObject.CreateInstance<InventoryItemData>();
                        data.itemName = pair.Key.ToString() + " Ammo";
                        data.itemType = ItemType.Ammo;
                        data.ammoType = pair.Key;
                        data.icon = ammoIcon;
                    }
                    
                    slot.Setup(data, pair.Value);
                }
            }
        }

        // 2. Show Grenades
        if (BagManager.Instance.grenadeCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                InventoryItemData data = BagManager.Instance.GetGrenadeData();

                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Boom";
                    data.itemType = ItemType.Grenade;
                    data.icon = grenadeIcon;
                }
                
                slot.Setup(data, BagManager.Instance.grenadeCount);
            }
        }
        
        // 3. Medikit
        if (BagManager.Instance.medikitCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                // Create temp data if needed or fetch from manager
                InventoryItemData data = ScriptableObject.CreateInstance<InventoryItemData>();
                data.itemName = "Medikit";
                data.itemType = ItemType.Medikit;
                // data.icon = ... (Ensure data has icon or assign generic)
                
                slot.Setup(data, BagManager.Instance.medikitCount);
            }
        }

        // 4. Protein Shake
        if (BagManager.Instance.proteinShakeCount > 0)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot slot = slotObj.GetComponent<BagItemSlot>();
            if (slot != null)
            {
                InventoryItemData data = ScriptableObject.CreateInstance<InventoryItemData>();
                data.itemName = "Protein Shake";
                data.itemType = ItemType.ProteinShake;
                
                slot.Setup(data, BagManager.Instance.proteinShakeCount);
            }
        }

        // 5. Update Weight Text
        if (weightText != null)
        {
            weightText.text = $"Weight: {BagManager.Instance.currentWeight}/{BagManager.Instance.maxWeight}";
        }
    }
}
