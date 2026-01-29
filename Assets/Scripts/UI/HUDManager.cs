using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Weapon Slots")]
    public Button weaponSlot1;
    public Button weaponSlot2;
    public TextMeshProUGUI weapon1AmmoText;
    public TextMeshProUGUI weapon2AmmoText;
    [SerializeField] private Image weapon1Icon;
    [SerializeField] private Image weapon2Icon;

    [Header("Grenade")]
    public Button boomButton;
    public TextMeshProUGUI boomCountText;

    [Header("Pickup")]
    public Button pickupButton;

    [Header("Health")]
    public Slider healthSlider;
    public TextMeshProUGUI healthText; // "100/100"

    [Header("Consumables")]
    public Button medikitButton;
    public TextMeshProUGUI medikitCountText;
    public Button shakeButton;
    public TextMeshProUGUI shakeCountText;

    [Header("Bag")]
    public Button bagButton;

    private void Start()
    {
        if (weaponSlot1 != null) weaponSlot1.onClick.AddListener(() => SwitchWeapon(0));
        if (weaponSlot2 != null) weaponSlot2.onClick.AddListener(() => SwitchWeapon(1));
        if (boomButton != null) boomButton.onClick.AddListener(ThrowGrenade);
        if (pickupButton != null) pickupButton.onClick.AddListener(OnPickupPressed);
        if (bagButton != null) bagButton.onClick.AddListener(ToggleBag);
        
        if (medikitButton != null) medikitButton.onClick.AddListener(() => BagManager.Instance?.UseMedikit());
        if (shakeButton != null) shakeButton.onClick.AddListener(() => BagManager.Instance?.UseProteinShake());

        // Initial Health Update setup
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnHealthChanged += UpdateHealthUI;
            UpdateHealthUI(PlayerHealth.Instance.GetCurrentHealth(), PlayerHealth.Instance.GetMaxHealth());
        }
        else
        {
            // Try to find if not singleton'd yet (though Awake should have run)
            var ph = FindObjectOfType<PlayerHealth>();
            if (ph != null)
            {
                 ph.OnHealthChanged += UpdateHealthUI;
                 UpdateHealthUI(ph.GetCurrentHealth(), ph.GetMaxHealth());
            }
        }
    }

    private void OnDestroy()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.OnHealthChanged -= UpdateHealthUI;
        }
    }

    private void Update()
    {
        // Update Boom button state
        if (BagManager.Instance != null)
        {
             if (boomButton != null)
             {
                 boomButton.interactable = BagManager.Instance.grenadeCount > 0;
                 if (boomCountText != null) boomCountText.text = BagManager.Instance.grenadeCount.ToString();
             }

             if (medikitButton != null)
             {
                 // Check if hurt? Maybe always interactable if count > 0?
                 // But BagManager check handles logic. Just check count for UI feedback.
                 medikitButton.interactable = BagManager.Instance.medikitCount > 0; 
                 if (medikitCountText != null) medikitCountText.text = BagManager.Instance.medikitCount.ToString();
             }

             if (shakeButton != null)
             {
                 shakeButton.interactable = BagManager.Instance.proteinShakeCount > 0;
                 if (shakeCountText != null) shakeCountText.text = BagManager.Instance.proteinShakeCount.ToString();
             }
        }

        // Update Pickup button visibility
        if (pickupButton != null)
        {
            pickupButton.gameObject.SetActive(ItemPickup.NearestPickup != null);
        }

        // Update Weapon Info
        UpdateWeaponUI();
    }

    private void UpdateHealthUI(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (healthText != null)
        {
            healthText.text = $"{current}/{max}";
        }
    }

    private void UpdateWeaponUI()
    {
        if (BagManager.Instance == null) return;

        UpdateSlotUI(0, weapon1AmmoText, weapon1Icon);
        UpdateSlotUI(1, weapon2AmmoText, weapon2Icon);
    }

    private void UpdateSlotUI(int slotIndex, TextMeshProUGUI ammoText, Image icon)
    {
        var weapon = BagManager.Instance.weaponSlots[slotIndex];
        
        if (ammoText != null)
        {
            ammoText.text = weapon != null ? $"{weapon.GetCurrentAmmo()}/{BagManager.Instance.GetAmmo(weapon.ammoType)}" : "-";
        }

        if (icon != null)
        {
            if (weapon != null && weapon.itemData != null)
            {
                icon.sprite = weapon.itemData.icon;
                icon.preserveAspect = true;
                icon.color = Color.white; // Show
            }
            else
            {
                icon.sprite = null;
                icon.color = new Color(0, 0, 0, 0); // Hide transparently
            }
        }
    }

    private void SwitchWeapon(int slot)
    {
        if (WeaponController.Instance != null) WeaponController.Instance.SwitchToSlot(slot);
    }

    private void ThrowGrenade()
    {
        if (WeaponController.Instance != null) WeaponController.Instance.ThrowGrenade();
    }

    private void OnPickupPressed()
    {
        if (ItemPickup.NearestPickup != null) ItemPickup.NearestPickup.PickingUpManually();
    }

    private void ToggleBag()
    {
        if (BagUI.Instance != null) BagUI.Instance.ToggleBag();
    }
}
