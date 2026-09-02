using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component attached to Pickup Item Prefabs in the Pickup Scroll View.
/// Allows dragging and dropping the Item Icon Image and Item Name Text in the Unity Inspector.
/// </summary>
public class PickupItemEntryUI : MonoBehaviour
{
    [Header("Item Entry Components")]
    public Image itemIcon;
    public TextMeshProUGUI itemName;
    public Button pickButton;

    /// <summary>
    /// Configures the item icon, name, and pick button click listener.
    /// </summary>
    public void Setup(InventoryItemData itemData, System.Action onPickClicked)
    {
        if (itemData != null)
        {
            if (itemName != null)
            {
                itemName.text = itemData.itemName;
            }

            if (itemIcon != null)
            {
                if (itemData.icon != null)
                {
                    itemIcon.sprite = itemData.icon;
                    itemIcon.preserveAspect = true;
                    itemIcon.color = Color.white;
                    itemIcon.gameObject.SetActive(true);
                }
                else
                {
                    itemIcon.gameObject.SetActive(false);
                }
            }
        }

        Button btn = pickButton != null ? pickButton : GetComponent<Button>();
        if (btn != null && onPickClicked != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onPickClicked?.Invoke());
        }
    }
}
