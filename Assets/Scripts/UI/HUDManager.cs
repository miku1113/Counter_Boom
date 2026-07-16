using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("Weapon Slots")]
    public Button             weaponSlot1;
    public Button             weaponSlot2;
    public TextMeshProUGUI    weapon1AmmoText;
    public TextMeshProUGUI    weapon2AmmoText;
    [SerializeField] private Image weapon1Icon;
    [SerializeField] private Image weapon2Icon;

    [Header("Grenade")]
    public Button          boomButton;
    public TextMeshProUGUI boomCountText;

    [Header("Pickup")]
    public Button pickupButton;

    [Header("Health")]
    public Slider          healthSlider;
    public TextMeshProUGUI healthText;

    [Header("Consumables")]
    public Button          medikitButton;
    public TextMeshProUGUI medikitCountText;
    public Button          shakeButton;
    public TextMeshProUGUI shakeCountText;

    [Header("Bag")]
    public Button bagButton;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Start()
    {
        // Button listeners
        weaponSlot1?.onClick.AddListener(() => SwitchWeapon(0));
        weaponSlot2?.onClick.AddListener(() => SwitchWeapon(1));
        boomButton?.onClick.AddListener(ThrowGrenade);
        pickupButton?.onClick.AddListener(OnPickupPressed);
        bagButton?.onClick.AddListener(ToggleBag);
        medikitButton?.onClick.AddListener(() => BagManager.Instance?.UseMedikit());
        shakeButton?.onClick.AddListener(() => BagManager.Instance?.UseProteinShake());

        // Subscribe to BagManager events (replaces per-frame polling)
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnGrenadeUpdated      += UpdateGrenadeUI;
            BagManager.Instance.OnMedikitUpdated       += UpdateMedikitUI;
            BagManager.Instance.OnProteinShakeUpdated  += UpdateShakeUI;

            // Force an initial update from current state
            UpdateGrenadeUI(BagManager.Instance.grenadeCount);
            UpdateMedikitUI(BagManager.Instance.medikitCount);
            UpdateShakeUI(BagManager.Instance.proteinShakeCount);
        }

        // Subscribe to WeaponController events for ammo display and slot icon updates
        if (WeaponController.Instance != null)
        {
            WeaponController.Instance.OnAmmoChanged      += UpdateAmmoUI;
            WeaponController.Instance.OnWeaponSlotUpdated += OnWeaponSlotUpdated;
            // Seed with current ammo
            UpdateAmmoUI(WeaponController.Instance.GetCurrentAmmo(), WeaponController.Instance.GetMaxAmmo());
        }

        // Subscribe to Health events
        var health = PlayerHealth.Instance;
        if (health == null) health = FindObjectOfType<PlayerHealth>();
        if (health != null)
        {
            health.OnHealthChanged += UpdateHealthUI;
            UpdateHealthUI(health.GetCurrentHealth(), health.GetMaxHealth());
        }

        // Seed weapon slot icons
        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
    }

    private void OnDestroy()
    {
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnGrenadeUpdated     -= UpdateGrenadeUI;
            BagManager.Instance.OnMedikitUpdated      -= UpdateMedikitUI;
            BagManager.Instance.OnProteinShakeUpdated -= UpdateShakeUI;
        }

        if (WeaponController.Instance != null)
        {
            WeaponController.Instance.OnAmmoChanged       -= UpdateAmmoUI;
            WeaponController.Instance.OnWeaponSlotUpdated -= OnWeaponSlotUpdated;
        }

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnHealthChanged -= UpdateHealthUI;
    }

    // ─── Update (Handles dynamic multi-item pickup list) ─────────────────────
    
    private System.Collections.Generic.List<Button> spawnedPickupButtons = new System.Collections.Generic.List<Button>();
    private System.Collections.Generic.List<ItemPickup> lastPickups = new System.Collections.Generic.List<ItemPickup>();

    private void Update()
    {
        UpdatePickupUI();
    }

    private void UpdatePickupUI()
    {
        if (pickupButton == null) return;

        // Safely remove any destroyed items from list
        ItemPickup.PickupsInRange.RemoveAll(item => item == null);
        var currentPickups = ItemPickup.PickupsInRange;

        // Check if list contents have changed
        bool changed = currentPickups.Count != lastPickups.Count;
        if (!changed)
        {
            for (int i = 0; i < currentPickups.Count; i++)
            {
                if (currentPickups[i] != lastPickups[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed) return;

        // Sync last list state
        lastPickups.Clear();
        lastPickups.AddRange(currentPickups);

        // Destroy previous clones
        foreach (var btn in spawnedPickupButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        spawnedPickupButtons.Clear();

        if (currentPickups.Count == 0)
        {
            pickupButton.gameObject.SetActive(false);
            return;
        }

        if (currentPickups.Count == 1)
        {
            pickupButton.gameObject.SetActive(true);
            var pickup = currentPickups[0];
            SetButtonText(pickupButton, $"Pick {pickup.itemData.itemName}");
            pickupButton.onClick.RemoveAllListeners();
            pickupButton.onClick.AddListener(() => pickup.PickingUpManually());
        }
        else
        {
            // Hide the template button, spawn custom buttons stacked vertically
            pickupButton.gameObject.SetActive(false);

            RectTransform templateRt = pickupButton.GetComponent<RectTransform>();
            float buttonHeight = templateRt.rect.height;
            float spacing = 10f;

            for (int i = 0; i < currentPickups.Count; i++)
            {
                var pickup = currentPickups[i];
                GameObject cloneObj = Instantiate(pickupButton.gameObject, pickupButton.transform.parent);
                cloneObj.SetActive(true);

                Button cloneBtn = cloneObj.GetComponent<Button>();
                SetButtonText(cloneBtn, $"Pick {pickup.itemData.itemName}");

                cloneBtn.onClick.RemoveAllListeners();
                cloneBtn.onClick.AddListener(() => pickup.PickingUpManually());

                RectTransform cloneRt = cloneObj.GetComponent<RectTransform>();
                cloneRt.anchoredPosition = templateRt.anchoredPosition + new Vector2(0, i * (buttonHeight + spacing));

                spawnedPickupButtons.Add(cloneBtn);
            }
        }
    }

    private void SetButtonText(Button button, string text)
    {
        var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }
        var txt = button.GetComponentInChildren<UnityEngine.UI.Text>();
        if (txt != null)
        {
            txt.text = text;
        }
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────

    private void UpdateGrenadeUI(int count)
    {
        if (boomButton    != null) boomButton.interactable = count > 0;
        if (boomCountText != null) boomCountText.text      = count.ToString();
    }

    private void UpdateMedikitUI(int count)
    {
        if (medikitButton   != null) medikitButton.interactable = count > 0;
        if (medikitCountText!= null) medikitCountText.text      = count.ToString();
    }

    private void UpdateShakeUI(int count)
    {
        if (shakeButton   != null) shakeButton.interactable = count > 0;
        if (shakeCountText!= null) shakeCountText.text      = count.ToString();
    }

    private void UpdateAmmoUI(int current, int max)
    {
        // Update the active slot's ammo text
        int activeSlot = BagManager.Instance != null ? BagManager.Instance.GetCurrentWeaponIndex() : 0;
        var ammoText   = activeSlot == 0 ? weapon1AmmoText : weapon2AmmoText;

        if (ammoText != null)
        {
            var weapon = BagManager.Instance?.GetWeaponInSlot(activeSlot);
            int bagAmmo = weapon != null && BagManager.Instance != null
                ? BagManager.Instance.GetAmmo(weapon.ammoType) : 0;
            ammoText.text = $"{current}/{bagAmmo}";
        }
    }

    private void UpdateHealthUI(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value    = current;
        }
        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }

    /// <summary>
    /// Called once on Start and whenever the weapon slot contents change,
    /// to refresh icon and static ammo display for a slot.
    /// </summary>
    private void RefreshWeaponSlotUI(int slotIndex)
    {
        var weapon    = BagManager.Instance?.GetWeaponInSlot(slotIndex);
        var ammoText  = slotIndex == 0 ? weapon1AmmoText : weapon2AmmoText;
        var icon      = slotIndex == 0 ? weapon1Icon     : weapon2Icon;

        if (ammoText != null)
            ammoText.text = weapon != null
                ? $"{weapon.GetCurrentAmmo()}/{BagManager.Instance?.GetAmmo(weapon.ammoType) ?? 0}"
                : "-";

        if (icon != null)
        {
            if (weapon != null && weapon.itemData != null)
            {
                icon.sprite        = weapon.itemData.icon;
                icon.preserveAspect = true;
                icon.color         = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color  = new Color(0, 0, 0, 0);
            }
        }
    }

    // ─── Button Callbacks ────────────────────────────────────────────────────

    private void SwitchWeapon(int slot)
    {
        WeaponController.Instance?.SwitchToSlot(slot);
        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
    }

    /// <summary>Called by WeaponController.OnWeaponSlotUpdated — refreshes one slot's icon immediately.</summary>
    private void OnWeaponSlotUpdated(int slotIndex)
    {
        RefreshWeaponSlotUI(slotIndex);
        // Also refresh the OTHER slot because currentSlot may have changed (e.g., after drop)
        RefreshWeaponSlotUI(1 - slotIndex);
    }

    private void ThrowGrenade()       => WeaponController.Instance?.ThrowGrenade();
    private void OnPickupPressed()    => ItemPickup.NearestPickup?.PickingUpManually();
    private void ToggleBag()          => BagUI.Instance?.ToggleBag();
}
