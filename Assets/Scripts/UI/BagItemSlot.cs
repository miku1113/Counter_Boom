using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BagItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventoryItemData itemData;
    public int amount;
    [SerializeField] public Image iconImage;
    [SerializeField] public TextMeshProUGUI amountText;
    [SerializeField] public Button useButton;
    [SerializeField] public Button dropButton;
    [SerializeField] public TextMeshProUGUI itemNameText;
    
    [System.NonSerialized] public int weaponSlotIndex = -1;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Transform originalParent;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        
        AutoResolveReferences();

        if (useButton != null)
        {
            useButton.onClick.RemoveListener(OnUse);
            useButton.onClick.AddListener(OnUse);
        }

        if (dropButton != null)
        {
            dropButton.onClick.RemoveListener(OnDrop);
            dropButton.onClick.AddListener(OnDrop);
        }
    }

    private void AutoResolveReferences()
    {
        if (iconImage == null)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject != this.gameObject)
                {
                    iconImage = img;
                    break;
                }
            }
        }

        if (itemNameText == null || amountText == null)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                string n = t.gameObject.name.ToLower();
                if (n.Contains("name") || n.Contains("title")) itemNameText = t;
                else if (n.Contains("amount") || n.Contains("count") || n.Contains("qty")) amountText = t;
            }
            if (tmps.Length > 0 && itemNameText == null) itemNameText = tmps[0];
            if (tmps.Length > 1 && amountText == null) amountText = tmps[1];
        }

        if (useButton == null || dropButton == null)
        {
            var btns = GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("use") || n.Contains("equip")) useButton = b;
                else if (n.Contains("drop") || n.Contains("trash")) dropButton = b;
            }
        }
    }

    public void Setup(InventoryItemData data, int count)
    {
        AutoResolveReferences();

        itemData = data;
        amount = count;
        
        if (iconImage != null) 
        {
            if (data != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.color = Color.white;
            }
            iconImage.preserveAspect = true;
        }
        
        if (amountText != null) amountText.text = count > 1 ? $"x{count}" : "";
        if (itemNameText != null) itemNameText.text = data != null ? data.itemName : "Item";

        // Show/Hide Use Button for Consumables, Weapons and Grenades
        if (useButton != null)
        {
            if (data != null && (data.itemType == ItemType.Medikit || 
                data.itemType == ItemType.ProteinShake || 
                data.itemType == ItemType.Grenade))
            {
                useButton.gameObject.SetActive(true);

                var txt = useButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (txt != null)
                {
                    txt.text = (data.itemType == ItemType.Grenade) ? "Equip" : "Use";
                }
                else
                {
                    var legacyTxt = useButton.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (legacyTxt != null) legacyTxt.text = (data.itemType == ItemType.Grenade) ? "Equip" : "Use";
                }
            }
            else
            {
                useButton.gameObject.SetActive(false);
            }
        }

        if (dropButton != null)
        {
            dropButton.gameObject.SetActive(true);
        }
    }
    
    private void OnUse()
    {
        if (BagManager.Instance != null && itemData != null)
        {
            if (itemData.itemType == ItemType.Medikit)
            {
                BagManager.Instance.UseMedikit();
            }
            else if (itemData.itemType == ItemType.ProteinShake)
            {
                BagManager.Instance.UseProteinShake();
            }
            else if (itemData.itemType == ItemType.Grenade)
            {
                BagManager.Instance.EquipGrenade(itemData.grenadeType);
            }
            BagUI.Instance?.RefreshUI();
        }
    }

    public void OnDrop()
    {
        if (BagManager.Instance != null && itemData != null)
        {
            if (itemData.itemType == ItemType.Ammo)
                BagManager.Instance.DropAmmo(itemData.ammoType, itemData, amount);
            else if (itemData.itemType == ItemType.Grenade)
                BagManager.Instance.DropGrenade(itemData.grenadeType, itemData);
            else if (itemData.itemType == ItemType.Medikit)
                BagManager.Instance.DropMedikit(itemData);
            else if (itemData.itemType == ItemType.ProteinShake)
                BagManager.Instance.DropProteinShake(itemData);
            else if (itemData.itemType == ItemType.Scope)
                BagManager.Instance.DropScope(itemData);
            else if (itemData.itemType == ItemType.Weapon)
                BagManager.Instance.DropWeapon(weaponSlotIndex != -1 ? weaponSlotIndex : 0);
        }

        BagUI.Instance?.RefreshUI();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        
        // Move to top of hierarchy so it's not clipped
        transform.SetParent(transform.root);
        
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        // Check if dropped outside the side window
        if (!RectTransformUtility.RectangleContainsScreenPoint(BagUI.Instance.sideWindow, eventData.position))
        {
            // Drop item in game world
            if (BagManager.Instance != null)
            {
                if (itemData.itemType == ItemType.Ammo)
                    BagManager.Instance.DropAmmo(itemData.ammoType, itemData, amount);
                else if (itemData.itemType == ItemType.Grenade)
                    BagManager.Instance.DropGrenade(itemData.grenadeType, itemData);
                else if (itemData.itemType == ItemType.Medikit)
                    BagManager.Instance.DropMedikit(itemData);
                else if (itemData.itemType == ItemType.ProteinShake)
                    BagManager.Instance.DropProteinShake(itemData);
                else if (itemData.itemType == ItemType.Scope)
                    BagManager.Instance.DropScope(itemData);
                else if (itemData.itemType == ItemType.Weapon)
                    BagManager.Instance.DropWeapon(weaponSlotIndex != -1 ? weaponSlotIndex : 0); 
            }
            
            Destroy(gameObject);
            BagUI.Instance.RefreshUI();
        }
        else
        {
            // Return to original parent/position
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}
