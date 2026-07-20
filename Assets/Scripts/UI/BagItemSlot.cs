using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BagItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public InventoryItemData itemData;
    public int amount;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Button useButton;
    [SerializeField] private TextMeshProUGUI itemNameText;
    
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
        
        if (useButton != null)
        {
            useButton.onClick.AddListener(OnUse);
        }
    }

    public void Setup(InventoryItemData data, int count)
    {
        itemData = data;
        amount = count;
        
        if (iconImage != null) 
        {
            iconImage.sprite = data.icon;
            iconImage.preserveAspect = true;
        }
        
        if (amountText != null) amountText.text = count.ToString();
        if (itemNameText != null) itemNameText.text = data.itemName;

        // Show/Hide Use Button for Consumables and Grenades
        if (useButton != null)
        {
            if (data.itemType == ItemType.Medikit || 
                data.itemType == ItemType.ProteinShake || 
                data.itemType == ItemType.Grenade)
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
        }
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
