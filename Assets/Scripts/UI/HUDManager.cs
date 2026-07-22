using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
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

    [Header("Multiplayer")]
    [SerializeField] private TextMeshProUGUI joinCodeHUDText;

    [Header("Host Migration")]
    [SerializeField] private GameObject migrationOverlayPanel;
    [SerializeField] private TextMeshProUGUI migrationStatusText;

    [Header("Spectator Mode")]
    [SerializeField] private GameObject spectatorPanel;
    [SerializeField] private TextMeshProUGUI spectatingPlayerNameText;
    [SerializeField] private Button prevSpectateButton;
    [SerializeField] private Button nextSpectateButton;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Start()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Button listeners
        weaponSlot1?.onClick.AddListener(() => SwitchWeapon(0));
        weaponSlot2?.onClick.AddListener(() => SwitchWeapon(1));
        boomButton?.onClick.AddListener(ThrowGrenade);
        pickupButton?.onClick.AddListener(OnPickupPressed);
        bagButton?.onClick.AddListener(ToggleBag);
        medikitButton?.onClick.AddListener(() => BagManager.Instance?.UseMedikit());
        shakeButton?.onClick.AddListener(() => BagManager.Instance?.UseProteinShake());

        if (prevSpectateButton != null) prevSpectateButton.onClick.AddListener(OnPrevSpectateClicked);
        if (nextSpectateButton != null) nextSpectateButton.onClick.AddListener(OnNextSpectateClicked);
        if (spectatorPanel != null) spectatorPanel.SetActive(false);

        // Subscribe to BagManager events (replaces per-frame polling)
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnGrenadeUpdated      += UpdateGrenadeUI;
            BagManager.Instance.OnMedikitUpdated       += UpdateMedikitUI;
            BagManager.Instance.OnProteinShakeUpdated  += UpdateShakeUI;

            // Force an initial update from current state
            UpdateGrenadeUI(BagManager.Instance.activeGrenadeType, BagManager.Instance.GetGrenadeCount(BagManager.Instance.activeGrenadeType));
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

        // Subscribe to local player visual triggers
        PlayerController.OnLocalPlayerStunned += HandleLocalPlayerStunned;
        PlayerController.OnLocalPlayerEnterSmoke += HandleEnterSmoke;
        PlayerController.OnLocalPlayerExitSmoke += HandleExitSmoke;

        // Subscribe to Host Migration events
        RelayNetworkManager.OnMigrationStateChanged += HandleMigrationStateChanged;
        RelayNetworkManager.OnMigrationStatusChanged += HandleMigrationStatusChanged;
        if (migrationOverlayPanel != null) migrationOverlayPanel.SetActive(false);

        // Display the active room code if available
        if (joinCodeHUDText != null)
        {
            if (RelayNetworkManager.Instance != null && !string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
            {
                joinCodeHUDText.text = $"Room Code: {RelayNetworkManager.Instance.CurrentJoinCode}";
                joinCodeHUDText.gameObject.SetActive(true);
            }
            else
            {
                joinCodeHUDText.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        RelayNetworkManager.OnMigrationStateChanged -= HandleMigrationStateChanged;
        RelayNetworkManager.OnMigrationStatusChanged -= HandleMigrationStatusChanged;

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

        // Unsubscribe from local player visual triggers
        PlayerController.OnLocalPlayerStunned -= HandleLocalPlayerStunned;
        PlayerController.OnLocalPlayerEnterSmoke -= HandleEnterSmoke;
        PlayerController.OnLocalPlayerExitSmoke -= HandleExitSmoke;
    }

    private void HandleMigrationStateChanged(bool isMigrating)
    {
        if (migrationOverlayPanel != null)
        {
            migrationOverlayPanel.SetActive(isMigrating);
        }
    }

    private void HandleMigrationStatusChanged(string statusMessage)
    {
        if (migrationStatusText != null)
        {
            migrationStatusText.text = statusMessage;
        }
    }


    // ─── Update (Handles dynamic multi-item pickup list) ─────────────────────
    
    private System.Collections.Generic.List<Button> spawnedPickupButtons = new System.Collections.Generic.List<Button>();
    private System.Collections.Generic.List<ItemPickup> lastPickups = new System.Collections.Generic.List<ItemPickup>();
    private bool isPickupUIInitialized = false;

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
        bool changed = !isPickupUIInitialized || currentPickups.Count != lastPickups.Count;
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

        isPickupUIInitialized = true;

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

    private void UpdateGrenadeUI(GrenadeType type, int count)
    {
        if (BagManager.Instance == null) return;
        
        GrenadeType activeType = BagManager.Instance.activeGrenadeType;

        // Auto-switch to another available grenade type if the active one runs out
        if (activeType == type && count <= 0)
        {
            foreach (GrenadeType gType in System.Enum.GetValues(typeof(GrenadeType)))
            {
                if (gType != GrenadeType.None && BagManager.Instance.GetGrenadeCount(gType) > 0)
                {
                    BagManager.Instance.EquipGrenade(gType);
                    return;
                }
            }
        }

        int activeCount = BagManager.Instance.GetGrenadeCount(activeType);

        if (boomButton    != null) boomButton.interactable = activeCount > 0;
        if (boomCountText != null) boomCountText.text      = activeCount.ToString();

        // Update button icon dynamically
        if (boomButton != null)
        {
            var image = boomButton.GetComponent<Image>();
            if (image != null)
            {
                var data = BagManager.Instance.allItemData?.Find(x => x.itemType == ItemType.Grenade && x.grenadeType == activeType);
                if (data != null && data.icon != null)
                {
                    image.sprite = data.icon;
                    image.preserveAspect = true;
                    image.color = Color.white;
                }
            }
        }
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

    // ─── Stun and Smoke Dynamic Visual Effects ─────────────────────────────────

    private Image     dynamicFlashOverlay;
    private Coroutine flashCoroutine;

    private Image     dynamicSmokeOverlay;
    private Coroutine smokeCoroutine;
    private int       smokeStackCount = 0;

    private void CreateDynamicFlashOverlay()
    {
        if (dynamicFlashOverlay != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject overlayObj = new GameObject("StunFlashOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);

        dynamicFlashOverlay = overlayObj.AddComponent<Image>();
        dynamicFlashOverlay.color = new Color(1f, 1f, 1f, 0f); // Starts transparent
        dynamicFlashOverlay.raycastTarget = false;

        RectTransform rect = overlayObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayObj.transform.SetAsLastSibling();
    }

    private void HandleLocalPlayerStunned(float duration)
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(StunFlashRoutine(duration));
    }

    private System.Collections.IEnumerator StunFlashRoutine(float duration)
    {
        CreateDynamicFlashOverlay();
        if (dynamicFlashOverlay == null) yield break;

        float elapsed = 0f;
        Color c       = Color.white;
        c.a           = 0.95f; // Screen flashes to near-opaque white
        dynamicFlashOverlay.color = c;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.95f, 0f, elapsed / duration);
            c.a = alpha;
            dynamicFlashOverlay.color = c;
            yield return null;
        }

        c.a = 0f;
        dynamicFlashOverlay.color = c;
        flashCoroutine = null;
    }

    private void CreateDynamicSmokeOverlay()
    {
        if (dynamicSmokeOverlay != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject overlayObj = new GameObject("SmokeBlindnessOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);

        dynamicSmokeOverlay = overlayObj.AddComponent<Image>();
        dynamicSmokeOverlay.color = new Color(0.12f, 0.12f, 0.12f, 0f); // Starts transparent dark grey
        dynamicSmokeOverlay.raycastTarget = false;

        RectTransform rect = overlayObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayObj.transform.SetAsLastSibling();
    }

    private void HandleEnterSmoke()
    {
        smokeStackCount++;
        if (smokeCoroutine != null) StopCoroutine(smokeCoroutine);
        smokeCoroutine = StartCoroutine(FadeSmokeOverlay(0.85f, 0.4f)); // Fades in to 85% opacity over 0.4s
    }

    private void HandleExitSmoke()
    {
        smokeStackCount = Mathf.Max(0, smokeStackCount - 1);
        if (smokeStackCount == 0)
        {
            if (smokeCoroutine != null) StopCoroutine(smokeCoroutine);
            smokeCoroutine = StartCoroutine(FadeSmokeOverlay(0f, 0.5f)); // Fades out to 0% opacity over 0.5s
        }
    }

    private System.Collections.IEnumerator FadeSmokeOverlay(float targetOpacity, float duration)
    {
        CreateDynamicSmokeOverlay();
        if (dynamicSmokeOverlay == null) yield break;

        float elapsed = 0f;
        Color startColor  = dynamicSmokeOverlay.color;
        Color targetColor = new Color(0.12f, 0.12f, 0.12f, targetOpacity);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            dynamicSmokeOverlay.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }

        dynamicSmokeOverlay.color = targetColor;
        smokeCoroutine = null;
    }

    /// <summary>
    /// Updates the room code display with the countdown timer.
    /// </summary>
    public void UpdateRoomCodeAndTimer(string code, float timeRemaining)
    {
        if (joinCodeHUDText == null) return;

        if (string.IsNullOrEmpty(code))
        {
            joinCodeHUDText.gameObject.SetActive(false);
            return;
        }

        if (timeRemaining > 0.1f)
        {
            joinCodeHUDText.text = $"Room Code: {code} (Starts in: {Mathf.CeilToInt(timeRemaining)}s)";
            joinCodeHUDText.gameObject.SetActive(true);
        }
        else
        {
            joinCodeHUDText.text = $"Room Code: {code} (Match Started!)";
            joinCodeHUDText.gameObject.SetActive(true);
        }
    }

    public void EnableSpectatorUI(bool enable)
    {
        if (spectatorPanel != null)
        {
            spectatorPanel.SetActive(enable);
        }
        if (enable)
        {
            UpdateSpectatorName();
        }
    }

    private void OnPrevSpectateClicked()
    {
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SpectatePreviousTarget();
            UpdateSpectatorName();
        }
    }

    private void OnNextSpectateClicked()
    {
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SpectateNextTarget();
            UpdateSpectatorName();
        }
    }

    private void UpdateSpectatorName()
    {
        if (spectatingPlayerNameText != null && CameraController.Instance != null)
        {
            spectatingPlayerNameText.text = $"SPECTATING: {CameraController.Instance.GetCurrentSpectatedName()}";
        }
    }
}

