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

    [Header("Health & Energy")]
    public Slider          healthSlider;
    public TextMeshProUGUI healthText;
    public Slider          energySlider;
    public TextMeshProUGUI energyText;

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

    [Header("Settings Menu")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button leaveGameButton;
    [SerializeField] private Button closeSettingsButton;
    private GameObject leaveConfirmationModal;

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

        // Enforce landscape orientation & uniform resolution-independent UI scaling
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        if (canvas != null) ScreenAndUIScaler.ConfigureCanvas(canvas);

        // Ensure Settings UI (Gear button & Leave Game popup) exists and is wired
        EnsureSettingsUI();

        // Button listeners
        weaponSlot1?.onClick.AddListener(() => SwitchWeapon(0));
        weaponSlot2?.onClick.AddListener(() => SwitchWeapon(1));
        boomButton?.onClick.AddListener(ThrowGrenade);
        pickupButton?.onClick.AddListener(OnPickupPressed);
        medikitButton?.onClick.AddListener(() => BagManager.Instance?.UseMedikit());
        shakeButton?.onClick.AddListener(() => BagManager.Instance?.UseProteinShake());

        // Ensure Bag UI is initialized and bagButton is hooked
        EnsureBagUI();

        // Ensure Tactical Compass UI is initialized
        EnsureCompassUI();

        if (prevSpectateButton != null) prevSpectateButton.onClick.AddListener(OnPrevSpectateClicked);
        if (nextSpectateButton != null) nextSpectateButton.onClick.AddListener(OnNextSpectateClicked);
        if (spectatorPanel != null) spectatorPanel.SetActive(false);

        // Bind local player events & seed UI (weapons, grenades, consumables)
        BindLocalPlayer();

        // Auto-find Health & Energy UI elements if unassigned
        AutoResolveHealthAndEnergyUI();

        // Subscribe to Health events
        var health = PlayerHealth.Instance;
        if (health == null) health = FindObjectOfType<PlayerHealth>();
        if (health != null)
        {
            health.OnHealthChanged -= UpdateHealthUI;
            health.OnHealthChanged += UpdateHealthUI;
            health.OnDeath         -= ShowGameOverModal;
            health.OnDeath         += ShowGameOverModal;
            UpdateHealthUI(health.GetCurrentHealth(), health.GetMaxHealth());
        }

        // Subscribe to Energy events
        var energy = PlayerEnergy.Instance;
        if (energy == null) energy = FindObjectOfType<PlayerEnergy>();
        if (energy != null)
        {
            energy.OnEnergyChanged -= UpdateEnergyUI;
            energy.OnEnergyChanged += UpdateEnergyUI;
            UpdateEnergyUI(energy.GetCurrentEnergy(), energy.GetMaxEnergy());
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

        if (PlayerEnergy.Instance != null)
            PlayerEnergy.Instance.OnEnergyChanged -= UpdateEnergyUI;

        // Unsubscribe from local player visual triggers
        PlayerController.OnLocalPlayerStunned -= HandleLocalPlayerStunned;
        PlayerController.OnLocalPlayerEnterSmoke -= HandleEnterSmoke;
        PlayerController.OnLocalPlayerExitSmoke -= HandleExitSmoke;
    }

    private void HandleMigrationStateChanged(bool isMigrating)
    {
        EnsureMigrationUI();
        if (migrationOverlayPanel != null)
        {
            migrationOverlayPanel.SetActive(isMigrating);
            if (isMigrating) migrationOverlayPanel.transform.SetAsLastSibling();
        }
    }

    private void HandleMigrationStatusChanged(string statusMessage)
    {
        EnsureMigrationUI();
        if (migrationStatusText != null)
        {
            migrationStatusText.text = statusMessage;
        }
    }


    // ─── Update (Handles dynamic multi-item pickup list) ─────────────────────
    
    private System.Collections.Generic.List<Button> spawnedPickupButtons = new System.Collections.Generic.List<Button>();
    private System.Collections.Generic.List<ItemPickup> lastPickups = new System.Collections.Generic.List<ItemPickup>();
    private bool isPickupUIInitialized = false;
    private bool isPlayerEventsBound = false;

    private void Update()
    {
        if (!isPlayerEventsBound && (BagManager.Instance != null || WeaponController.Instance != null))
        {
            isPlayerEventsBound = true;
            BindLocalPlayer();
        }

        if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            ToggleBag();
        }

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

    public void BindLocalPlayer()
    {
        if (BagManager.Instance != null)
        {
            BagManager.Instance.OnBagUpdated          -= HandleBagUpdated;
            BagManager.Instance.OnBagUpdated          += HandleBagUpdated;

            BagManager.Instance.OnGrenadeUpdated      -= UpdateGrenadeUI;
            BagManager.Instance.OnGrenadeUpdated      += UpdateGrenadeUI;

            BagManager.Instance.OnMedikitUpdated       -= UpdateMedikitUI;
            BagManager.Instance.OnMedikitUpdated       += UpdateMedikitUI;

            BagManager.Instance.OnProteinShakeUpdated  -= UpdateShakeUI;
            BagManager.Instance.OnProteinShakeUpdated  += UpdateShakeUI;

            UpdateGrenadeUI(BagManager.Instance.activeGrenadeType, BagManager.Instance.GetGrenadeCount(BagManager.Instance.activeGrenadeType));
            UpdateMedikitUI(BagManager.Instance.medikitCount);
            UpdateShakeUI(BagManager.Instance.proteinShakeCount);
        }

        if (WeaponController.Instance != null)
        {
            WeaponController.Instance.OnAmmoChanged       -= UpdateAmmoUI;
            WeaponController.Instance.OnAmmoChanged       += UpdateAmmoUI;

            WeaponController.Instance.OnWeaponSlotUpdated -= OnWeaponSlotUpdated;
            WeaponController.Instance.OnWeaponSlotUpdated += OnWeaponSlotUpdated;

            UpdateAmmoUI(WeaponController.Instance.GetCurrentAmmo(), WeaponController.Instance.GetMaxAmmo());
        }

        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
    }

    private void HandleBagUpdated()
    {
        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
        if (BagManager.Instance != null)
        {
            UpdateGrenadeUI(BagManager.Instance.activeGrenadeType, BagManager.Instance.GetGrenadeCount(BagManager.Instance.activeGrenadeType));
        }
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────

    private void UpdateGrenadeUI(GrenadeType type, int count)
    {
        if (BagManager.Instance == null)
        {
            if (boomButton != null) boomButton.gameObject.SetActive(false);
            return;
        }
        
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
        bool hasGrenades = activeCount > 0 && activeType != GrenadeType.None;

        if (boomButton != null)
        {
            // Only show grenade button if player has a grenade in bag
            boomButton.gameObject.SetActive(hasGrenades);
            boomButton.interactable = hasGrenades;

            if (hasGrenades)
            {
                // Find icon on button or child image
                Image targetImg = null;
                Image[] images = boomButton.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img != null && img.gameObject != boomButton.gameObject)
                    {
                        targetImg = img;
                        break;
                    }
                }
                if (targetImg == null) targetImg = boomButton.GetComponent<Image>();

                if (targetImg != null)
                {
                    var data = BagManager.Instance.allItemData?.Find(x => x != null && x.itemType == ItemType.Grenade && x.grenadeType == activeType);
                    if (data != null && data.icon != null)
                    {
                        targetImg.sprite = data.icon;
                        targetImg.preserveAspect = true;
                        targetImg.color = Color.white;
                    }
                }
            }
        }

        if (boomCountText != null)
        {
            boomCountText.text = activeCount.ToString();
            boomCountText.gameObject.SetActive(hasGrenades);
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

    private void AutoResolveHealthAndEnergyUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Slider[] sliders = canvas.GetComponentsInChildren<Slider>(true);
        foreach (var s in sliders)
        {
            if (s == null) continue;
            string sName = s.gameObject.name.ToLower();
            if (healthSlider == null && (sName.Contains("health") || sName.Contains("hp") || sName.Contains("life")))
            {
                healthSlider = s;
            }
            else if (energySlider == null && (sName.Contains("energy") || sName.Contains("stamina") || sName.Contains("boost") || sName.Contains("mana") || sName.Contains("power")))
            {
                energySlider = s;
            }
        }

        TextMeshProUGUI[] tmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            if (t == null) continue;
            string tName = t.gameObject.name.ToLower();
            if (healthText == null && (tName.Contains("health") || tName.Contains("hp")))
            {
                healthText = t;
            }
            else if (energyText == null && (tName.Contains("energy") || tName.Contains("stamina") || tName.Contains("boost")))
            {
                energyText = t;
            }
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

    private void UpdateEnergyUI(float current, float max)
    {
        if (energySlider != null)
        {
            energySlider.maxValue = max;
            energySlider.value    = current;
        }
        if (energyText != null)
            energyText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
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

        // Auto-find slot icon Image if unassigned in inspector
        if (icon == null)
        {
            Button slotBtn = slotIndex == 0 ? weaponSlot1 : weaponSlot2;
            if (slotBtn != null)
            {
                Image[] imgs = slotBtn.GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img != null && img.gameObject != slotBtn.gameObject)
                    {
                        icon = img;
                        if (slotIndex == 0) weapon1Icon = img; else weapon2Icon = img;
                        break;
                    }
                }
            }
        }

        if (ammoText != null)
        {
            ammoText.text = weapon != null
                ? $"{weapon.GetCurrentAmmo()}/{BagManager.Instance?.GetAmmo(weapon.ammoType) ?? 0}"
                : "-";
        }

        if (icon != null)
        {
            Sprite targetSprite = null;
            if (weapon != null)
            {
                if (weapon.itemData != null && weapon.itemData.icon != null)
                {
                    targetSprite = weapon.itemData.icon;
                }
                else if (weapon.weaponSprite != null)
                {
                    targetSprite = weapon.weaponSprite;
                }
                else if (BagManager.Instance != null && BagManager.Instance.allItemData != null)
                {
                    string wName = weapon.weaponName.ToLower();
                    var matchedData = BagManager.Instance.allItemData.Find(d => d != null && d.itemName.ToLower().Contains(wName));
                    if (matchedData != null && matchedData.icon != null)
                    {
                        targetSprite = matchedData.icon;
                    }
                }
            }

            if (targetSprite != null)
            {
                icon.sprite = targetSprite;
                icon.preserveAspect = true;
                icon.color = Color.white;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.sprite = null;
                icon.color = new Color(0, 0, 0, 0);
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

    public void EnsureBagUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            BagUI existing = canvas.GetComponentInChildren<BagUI>(true);
            if (existing == null) existing = FindObjectOfType<BagUI>(true);

            if (existing == null)
            {
                GameObject bagUIGO = new GameObject("BagUI", typeof(RectTransform));
                bagUIGO.transform.SetParent(canvas.transform, false);
                existing = bagUIGO.AddComponent<BagUI>();
            }
            BagUI.Instance = existing;
            existing.EnsureBagStructure();
        }

        if (bagButton == null)
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                string n = btn.gameObject.name.ToLower();
                if (n.Contains("bag") || n.Contains("inventory") || n.Contains("backpack"))
                {
                    bagButton = btn;
                    break;
                }
            }
        }

        if (bagButton != null)
        {
            bagButton.onClick.RemoveListener(ToggleBag);
            bagButton.onClick.AddListener(ToggleBag);
        }
    }

    public void EnsureCompassUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            CompassUI existing = canvas.GetComponentInChildren<CompassUI>(true) ?? FindObjectOfType<CompassUI>(true);
            if (existing == null)
            {
                GameObject compassGO = new GameObject("CompassUI", typeof(RectTransform));
                compassGO.transform.SetParent(canvas.transform, false);
                existing = compassGO.AddComponent<CompassUI>();
            }
            existing.EnsureCompassStructure();
        }
    }

    public void ToggleBag()
    {
        Debug.Log("[HUDManager] 🎒 ToggleBag triggered!");
        EnsureBagUI();
        if (BagUI.Instance != null)
        {
            BagUI.Instance.ToggleBag();
        }
        else
        {
            Debug.LogError("[HUDManager] ❌ BagUI.Instance could not be found or initialized!");
        }
    }

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

    /// <summary>
    /// Disables action buttons (weapons, grenades, pickup, consumables, bag) during ghost spectating mode.
    /// </summary>
    public void SetGhostUI(bool isGhost)
    {
        if (weaponSlot1 != null) weaponSlot1.gameObject.SetActive(!isGhost);
        if (weaponSlot2 != null) weaponSlot2.gameObject.SetActive(!isGhost);
        if (boomButton != null) boomButton.gameObject.SetActive(!isGhost);
        if (pickupButton != null) pickupButton.gameObject.SetActive(!isGhost);
        if (medikitButton != null) medikitButton.gameObject.SetActive(!isGhost);
        if (shakeButton != null) shakeButton.gameObject.SetActive(!isGhost);
        if (bagButton != null) bagButton.gameObject.SetActive(!isGhost);

        // Hide Health & Energy bars and text displays for ghosts
        if (healthSlider != null) healthSlider.gameObject.SetActive(!isGhost);
        if (healthText != null) healthText.gameObject.SetActive(!isGhost);
        if (energySlider != null) energySlider.gameObject.SetActive(!isGhost);
        if (energyText != null) energyText.gameObject.SetActive(!isGhost);
    }

    public void ToggleSettingsMenu()
    {
        if (settingsPanel == null) EnsureSettingsUI();
        if (settingsPanel != null)
        {
            bool newState = !settingsPanel.activeSelf;
            settingsPanel.SetActive(newState);
        }
    }

    private void OnLeaveGameClicked()
    {
        EnsureConfirmationModalUI();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (leaveConfirmationModal != null)
        {
            leaveConfirmationModal.SetActive(true);
            leaveConfirmationModal.transform.SetAsLastSibling();
        }
    }

    private async void ConfirmLeaveGame()
    {
        Debug.Log("[HUDManager] User confirmed leave game... Gracefully disconnecting.");

        if (leaveConfirmationModal != null) leaveConfirmationModal.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Reset player inventory & state
        if (BagManager.Instance != null) BagManager.Instance.ClearInventory();
        if (WeaponController.Instance != null) WeaponController.Instance.ClearAttachPointChildren();

        // Disconnect Relay / Netcode session gracefully so host migration triggers for remaining players!
        if (RelayNetworkManager.Instance != null)
        {
            try
            {
                await RelayNetworkManager.Instance.LeaveMatchGracefully();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HUDManager] Relay disconnect exception: {ex.Message}");
            }
        }
        else if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            try
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HUDManager] Shutdown exception: {ex.Message}");
            }
        }

        // Load MainMenuScene (with fallbacks to MainMenu or scene index 0)
        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }
        catch
        {
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            catch
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
        }
    }

    private void EnsureConfirmationModalUI()
    {
        if (leaveConfirmationModal != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("LeaveConfirmationModal");
        if (existing != null)
        {
            leaveConfirmationModal = existing.gameObject;
            return;
        }

        // Fullscreen dark overlay
        GameObject overlayGO = new GameObject("LeaveConfirmationModal", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        overlayGO.transform.SetParent(canvas.transform, false);

        RectTransform oRt = overlayGO.GetComponent<RectTransform>();
        oRt.anchorMin = Vector2.zero; oRt.anchorMax = Vector2.one; oRt.sizeDelta = Vector2.zero;
        oRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image oImg = overlayGO.GetComponent<UnityEngine.UI.Image>();
        oImg.color = new Color(0f, 0f, 0f, 0.8f);

        // Confirmation Card
        GameObject cardGO = new GameObject("Card", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        cardGO.transform.SetParent(overlayGO.transform, false);
        RectTransform cRt = cardGO.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(460f, 250f);
        cRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image cImg = cardGO.GetComponent<UnityEngine.UI.Image>();
        cImg.color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

        // Warning Icon / Title
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform tRt = titleGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -20f);
        tRt.sizeDelta = new Vector2(0f, 40f);

        TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "⚠️ LEAVE GAME?";
        titleTmp.fontSize = 24;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.4f, 0.4f);

        // Warning Message Body
        GameObject descGO = new GameObject("DescText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descGO.transform.SetParent(cardGO.transform, false);
        RectTransform dRt = descGO.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0f, 0.5f); dRt.anchorMax = new Vector2(1f, 0.5f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.anchoredPosition = new Vector2(0f, 10f);
        dRt.sizeDelta = new Vector2(-40f, 80f);

        TextMeshProUGUI descTmp = descGO.GetComponent<TextMeshProUGUI>();
        descTmp.text = "Are you sure you want to leave?\nIf you leave, this game can be disrupted for remaining players.";
        descTmp.fontSize = 17;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color = new Color(0.9f, 0.9f, 0.9f);

        // YES Button (Confirm)
        GameObject yesBtnGO = new GameObject("YesButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        yesBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform yRt = yesBtnGO.GetComponent<RectTransform>();
        yRt.anchorMin = new Vector2(0.28f, 0f); yRt.anchorMax = new Vector2(0.28f, 0f);
        yRt.pivot = new Vector2(0.5f, 0f);
        yRt.anchoredPosition = new Vector2(0f, 20f);
        yRt.sizeDelta = new Vector2(160f, 45f);

        UnityEngine.UI.Image yImg = yesBtnGO.GetComponent<UnityEngine.UI.Image>();
        yImg.color = new Color(0.85f, 0.2f, 0.2f, 1f);

        GameObject yTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        yTextGO.transform.SetParent(yesBtnGO.transform, false);
        RectTransform ytRt = yTextGO.GetComponent<RectTransform>();
        ytRt.anchorMin = Vector2.zero; ytRt.anchorMax = Vector2.one; ytRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI yTmp = yTextGO.GetComponent<TextMeshProUGUI>();
        yTmp.text = "YES, LEAVE";
        yTmp.fontSize = 16;
        yTmp.fontStyle = FontStyles.Bold;
        yTmp.alignment = TextAlignmentOptions.Center;
        yTmp.color = Color.white;

        Button yesBtn = yesBtnGO.GetComponent<Button>();
        yesBtn.onClick.AddListener(ConfirmLeaveGame);

        // NO Button (Cancel)
        GameObject noBtnGO = new GameObject("NoButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        noBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform nRt = noBtnGO.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.72f, 0f); nRt.anchorMax = new Vector2(0.72f, 0f);
        nRt.pivot = new Vector2(0.5f, 0f);
        nRt.anchoredPosition = new Vector2(0f, 20f);
        nRt.sizeDelta = new Vector2(160f, 45f);

        UnityEngine.UI.Image nImg = noBtnGO.GetComponent<UnityEngine.UI.Image>();
        nImg.color = new Color(0.25f, 0.3f, 0.4f, 1f);

        GameObject nTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        nTextGO.transform.SetParent(noBtnGO.transform, false);
        RectTransform ntRt = nTextGO.GetComponent<RectTransform>();
        ntRt.anchorMin = Vector2.zero; ntRt.anchorMax = Vector2.one; ntRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI nTmp = nTextGO.GetComponent<TextMeshProUGUI>();
        nTmp.text = "CANCEL";
        nTmp.fontSize = 16;
        nTmp.alignment = TextAlignmentOptions.Center;
        nTmp.color = Color.white;

        Button noBtn = noBtnGO.GetComponent<Button>();
        noBtn.onClick.AddListener(() => leaveConfirmationModal?.SetActive(false));

        leaveConfirmationModal = overlayGO;
        leaveConfirmationModal.SetActive(false);
    }

    private void EnsureMigrationUI()
    {
        if (migrationOverlayPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("MigrationOverlayPanel");
        if (existing != null)
        {
            migrationOverlayPanel = existing.gameObject;
            migrationStatusText = migrationOverlayPanel.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        // Fullscreen overlay panel
        GameObject panelGO = new GameObject("MigrationOverlayPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image img = panelGO.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);

        // Center card
        GameObject cardGO = new GameObject("Card", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        cardGO.transform.SetParent(panelGO.transform, false);
        RectTransform cRt = cardGO.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(480f, 220f);
        cRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image cImg = cardGO.GetComponent<UnityEngine.UI.Image>();
        cImg.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

        // Title
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform tRt = titleGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -20f);
        tRt.sizeDelta = new Vector2(0f, 40f);

        TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "HOST MIGRATION IN PROGRESS";
        titleTmp.fontSize = 22;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.85f, 0.3f);

        // Migration status message text
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGO.transform.SetParent(cardGO.transform, false);
        RectTransform sRt = statusGO.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(20f, 20f); sRt.offsetMax = new Vector2(-20f, -60f);

        migrationStatusText = statusGO.GetComponent<TextMeshProUGUI>();
        migrationStatusText.text = "Transferring host to another player... Please wait.";
        migrationStatusText.fontSize = 17;
        migrationStatusText.alignment = TextAlignmentOptions.Center;
        migrationStatusText.color = Color.white;

        migrationOverlayPanel = panelGO;
        migrationOverlayPanel.SetActive(false);
    }

    private void EnsureSettingsUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Settings Toggle Button (top-right corner ⚙️)
        if (settingsButton == null)
        {
            Transform existingBtn = canvas.transform.Find("SettingsButton");
            if (existingBtn != null) settingsButton = existingBtn.GetComponent<Button>();
            else
            {
                GameObject btnGO = new GameObject("SettingsButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
                btnGO.transform.SetParent(canvas.transform, false);

                RectTransform rt = btnGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-25f, -25f);
                rt.sizeDelta = new Vector2(50f, 50f);

                UnityEngine.UI.Image img = btnGO.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.15f, 0.18f, 0.25f, 0.9f);

                GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGO.transform.SetParent(btnGO.transform, false);
                RectTransform textRt = textGO.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
                tmp.text = "OPT";
                tmp.fontSize = 15;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                settingsButton = btnGO.GetComponent<Button>();
            }
        }

        // 2. Settings Panel Modal
        if (settingsPanel == null)
        {
            Transform existingPanel = canvas.transform.Find("SettingsPanel");
            if (existingPanel != null) settingsPanel = existingPanel.gameObject;
            else
            {
                GameObject panelGO = new GameObject("SettingsPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                panelGO.transform.SetParent(canvas.transform, false);

                RectTransform rt = panelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(400f, 250f);
                rt.anchoredPosition = Vector2.zero;

                UnityEngine.UI.Image img = panelGO.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.08f, 0.1f, 0.15f, 0.96f);

                // Title Text
                GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGO.transform.SetParent(panelGO.transform, false);
                RectTransform titleRt = titleGO.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -20f);
                titleRt.sizeDelta = new Vector2(0f, 40f);

                TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
                titleTmp.text = "SETTINGS";
                titleTmp.fontSize = 28;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.Center;
                titleTmp.color = Color.white;

                // Leave Game Button
                GameObject leaveBtnGO = new GameObject("LeaveGameButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
                leaveBtnGO.transform.SetParent(panelGO.transform, false);
                RectTransform leaveRt = leaveBtnGO.GetComponent<RectTransform>();
                leaveRt.anchorMin = new Vector2(0.5f, 0.5f); leaveRt.anchorMax = new Vector2(0.5f, 0.5f);
                leaveRt.pivot = new Vector2(0.5f, 0.5f);
                leaveRt.anchoredPosition = new Vector2(0f, 10f);
                leaveRt.sizeDelta = new Vector2(240f, 50f);

                UnityEngine.UI.Image leaveImg = leaveBtnGO.GetComponent<UnityEngine.UI.Image>();
                leaveImg.color = new Color(0.85f, 0.2f, 0.2f, 1f);

                GameObject leaveTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                leaveTextGO.transform.SetParent(leaveBtnGO.transform, false);
                RectTransform lTextRt = leaveTextGO.GetComponent<RectTransform>();
                lTextRt.anchorMin = Vector2.zero; lTextRt.anchorMax = Vector2.one; lTextRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI leaveTmp = leaveTextGO.GetComponent<TextMeshProUGUI>();
                leaveTmp.text = "LEAVE GAME";
                leaveTmp.fontSize = 20;
                leaveTmp.fontStyle = FontStyles.Bold;
                leaveTmp.alignment = TextAlignmentOptions.Center;
                leaveTmp.color = Color.white;

                leaveGameButton = leaveBtnGO.GetComponent<Button>();

                // Close Button
                GameObject closeBtnGO = new GameObject("CloseSettingsButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
                closeBtnGO.transform.SetParent(panelGO.transform, false);
                RectTransform closeRt = closeBtnGO.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(0.5f, 0f); closeRt.anchorMax = new Vector2(0.5f, 0f);
                closeRt.pivot = new Vector2(0.5f, 0f);
                closeRt.anchoredPosition = new Vector2(0f, 20f);
                closeRt.sizeDelta = new Vector2(160f, 40f);

                UnityEngine.UI.Image closeImg = closeBtnGO.GetComponent<UnityEngine.UI.Image>();
                closeImg.color = new Color(0.3f, 0.35f, 0.45f, 1f);

                GameObject closeTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                closeTextGO.transform.SetParent(closeBtnGO.transform, false);
                RectTransform cTextRt = closeTextGO.GetComponent<RectTransform>();
                cTextRt.anchorMin = Vector2.zero; cTextRt.anchorMax = Vector2.one; cTextRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI closeTmp = closeTextGO.GetComponent<TextMeshProUGUI>();
                closeTmp.text = "RESUME";
                closeTmp.fontSize = 18;
                closeTmp.alignment = TextAlignmentOptions.Center;
                closeTmp.color = Color.white;

                closeSettingsButton = closeBtnGO.GetComponent<Button>();

                settingsPanel = panelGO;
                settingsPanel.SetActive(false);
            }
        }

        // Wire Button Listeners
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ToggleSettingsMenu);
        }
        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(() => settingsPanel?.SetActive(false));
        }
        if (leaveGameButton != null)
        {
            leaveGameButton.onClick.RemoveAllListeners();
            leaveGameButton.onClick.AddListener(OnLeaveGameClicked);
        }

        EnsureNotificationUI();
    }

    [Header("Match Notifications & Role Badge")]
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private TextMeshProUGUI roleBadgeText;

    public void ShowNotification(string message)
    {
        EnsureNotificationUI();
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideNotification));
            Invoke(nameof(HideNotification), 4f);
        }
        Debug.Log($"[HUDManager] Notification: {message}");
    }

    private void HideNotification()
    {
        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }
    }

    private void EnsureNotificationUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) return;

        if (notificationText == null)
        {
            GameObject notifGO = new GameObject("HUDNotificationText", typeof(RectTransform), typeof(TextMeshProUGUI));
            notifGO.transform.SetParent(canvas.transform, false);

            RectTransform rt = notifGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.72f);
            rt.anchorMax = new Vector2(0.5f, 0.72f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(650f, 50f);

            notificationText = notifGO.GetComponent<TextMeshProUGUI>();
            notificationText.fontSize = 20;
            notificationText.fontStyle = FontStyles.Bold;
            notificationText.alignment = TextAlignmentOptions.Center;
            notificationText.color = new Color(1f, 0.95f, 0.4f, 1f); // Bright yellow highlight
            notificationText.outlineWidth = 0.2f;
            notificationText.outlineColor = Color.black;
            notifGO.SetActive(false);
        }

        if (roleBadgeText == null)
        {
            GameObject roleGO = new GameObject("HUDRoleBadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            roleGO.transform.SetParent(canvas.transform, false);

            RectTransform rt = roleGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(25f, -95f);
            rt.sizeDelta = new Vector2(300f, 40f);

            roleBadgeText = roleGO.GetComponent<TextMeshProUGUI>();
            roleBadgeText.fontSize = 18;
            roleBadgeText.fontStyle = FontStyles.Bold;
            roleBadgeText.alignment = TextAlignmentOptions.Left;
            roleBadgeText.color = Color.white;
            roleBadgeText.outlineWidth = 0.15f;
            roleBadgeText.outlineColor = Color.black;

            UpdateRoleBadgeDisplay();
        }
    }

    public void UpdateRoleBadgeDisplay()
    {
        if (roleBadgeText == null) EnsureNotificationUI();
        if (roleBadgeText == null) return;

        roleBadgeText.richText = true;

        PlayerController localPlayer = null;
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && (p.IsOwner || p.IsLocal))
            {
                localPlayer = p;
                break;
            }
        }

        if (localPlayer != null)
        {
            if (localPlayer.playerRole.Value == PlayerRole.Thief)
            {
                roleBadgeText.text = "ROLE: <color=#FF3333>THIEF</color>";
            }
            else
            {
                roleBadgeText.text = "ROLE: <color=#00E5FF>HOSTAGE</color>";
            }
        }
    }

    // ─── Game Over & Restart Modal ───────────────────────────────────────────

    private GameObject gameOverPanel;

    public void ShowGameOverModal()
    {
        EnsureGameOverUI();
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }
    }

    private void EnsureGameOverUI()
    {
        if (gameOverPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Background Modal
        GameObject panelGO = new GameObject("GameOverPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image bgImg = panelGO.GetComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f); // Frosted dark backdrop

        // Center Card
        GameObject cardGO = new GameObject("GameOverCard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        cardGO.transform.SetParent(panelGO.transform, false);

        RectTransform cardRt = cardGO.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(440f, 280f);
        cardRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image cardImg = cardGO.GetComponent<UnityEngine.UI.Image>();
        cardImg.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);

        // Title Text
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -25f);
        titleRt.sizeDelta = new Vector2(0f, 45f);

        TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "YOU DIED";
        titleTmp.fontSize = 34;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.25f, 0.25f, 1f);

        // Subtitle Text
        GameObject subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGO.transform.SetParent(cardGO.transform, false);
        RectTransform subRt = subGO.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f); subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -70f);
        subRt.sizeDelta = new Vector2(0f, 30f);

        TextMeshProUGUI subTmp = subGO.GetComponent<TextMeshProUGUI>();
        subTmp.text = "Defeated in Combat";
        subTmp.fontSize = 16;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.7f, 0.75f, 0.85f, 1f);

        // Restart Button
        GameObject restartBtnGO = new GameObject("RestartButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        restartBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform restRt = restartBtnGO.GetComponent<RectTransform>();
        restRt.anchorMin = new Vector2(0.5f, 0.5f); restRt.anchorMax = new Vector2(0.5f, 0.5f);
        restRt.pivot = new Vector2(0.5f, 0.5f);
        restRt.anchoredPosition = new Vector2(0f, -15f);
        restRt.sizeDelta = new Vector2(260f, 48f);

        UnityEngine.UI.Image restImg = restartBtnGO.GetComponent<UnityEngine.UI.Image>();
        restImg.color = new Color(0.18f, 0.65f, 0.35f, 1f); // Vibrant green

        GameObject restTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        restTextGO.transform.SetParent(restartBtnGO.transform, false);
        RectTransform rTextRt = restTextGO.GetComponent<RectTransform>();
        rTextRt.anchorMin = Vector2.zero; rTextRt.anchorMax = Vector2.one; rTextRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI restTmp = restTextGO.GetComponent<TextMeshProUGUI>();
        restTmp.text = "RESTART MATCH";
        restTmp.fontSize = 20;
        restTmp.fontStyle = FontStyles.Bold;
        restTmp.alignment = TextAlignmentOptions.Center;
        restTmp.color = Color.white;

        Button restBtn = restartBtnGO.GetComponent<Button>();
        restBtn.onClick.AddListener(RestartMatch);

        // Main Menu Button
        GameObject menuBtnGO = new GameObject("MainMenuButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        menuBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform menuRt = menuBtnGO.GetComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, 0f); menuRt.anchorMax = new Vector2(0.5f, 0f);
        menuRt.pivot = new Vector2(0.5f, 0f);
        menuRt.anchoredPosition = new Vector2(0f, 25f);
        menuRt.sizeDelta = new Vector2(260f, 42f);

        UnityEngine.UI.Image menuImg = menuBtnGO.GetComponent<UnityEngine.UI.Image>();
        menuImg.color = new Color(0.28f, 0.32f, 0.42f, 1f);

        GameObject menuTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        menuTextGO.transform.SetParent(menuBtnGO.transform, false);
        RectTransform mTextRt = menuTextGO.GetComponent<RectTransform>();
        mTextRt.anchorMin = Vector2.zero; mTextRt.anchorMax = Vector2.one; mTextRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI menuTmp = menuTextGO.GetComponent<TextMeshProUGUI>();
        menuTmp.text = "MAIN MENU";
        menuTmp.fontSize = 17;
        menuTmp.fontStyle = FontStyles.Bold;
        menuTmp.alignment = TextAlignmentOptions.Center;
        menuTmp.color = Color.white;

        Button menuBtn = menuBtnGO.GetComponent<Button>();
        menuBtn.onClick.AddListener(ReturnToMainMenu);

        gameOverPanel = panelGO;
        gameOverPanel.SetActive(false);
    }

    // ─── Victory & Reward Modal ──────────────────────────────────────────────

    private GameObject victoryPanel;
    private TextMeshProUGUI victoryCoinsEarnedText;
    private TextMeshProUGUI victoryTotalCoinsText;

    public void ShowVictoryModal(int coinsEarned = 10, int totalCoins = 1000)
    {
        EnsureVictoryUI();
        if (victoryCoinsEarnedText != null)
        {
            victoryCoinsEarnedText.text = $"💰 +{coinsEarned} COINS EARNED!";
        }
        if (victoryTotalCoinsText != null)
        {
            victoryTotalCoinsText.text = $"Total Balance: {totalCoins} Coins";
        }
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling();
        }
    }

    private void EnsureVictoryUI()
    {
        if (victoryPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Background Modal
        GameObject panelGO = new GameObject("VictoryPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image bgImg = panelGO.GetComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f); // Deep frosted backdrop

        // Center Card
        GameObject cardGO = new GameObject("VictoryCard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        cardGO.transform.SetParent(panelGO.transform, false);

        RectTransform cardRt = cardGO.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(460f, 320f);
        cardRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image cardImg = cardGO.GetComponent<UnityEngine.UI.Image>();
        cardImg.color = new Color(0.1f, 0.12f, 0.18f, 0.98f);

        // Card Gold Border
        Outline cardOutline = cardGO.AddComponent<Outline>();
        cardOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.85f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        // Title Text
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cardGO.transform, false);
        RectTransform titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -22f);
        titleRt.sizeDelta = new Vector2(0f, 45f);

        TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "🏆 YOU WIN! 🏆";
        titleTmp.fontSize = 32;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = new Color(1f, 0.85f, 0.2f, 1f); // Vibrant Gold

        // Subtitle Text
        GameObject subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGO.transform.SetParent(cardGO.transform, false);
        RectTransform subRt = subGO.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f); subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -65f);
        subRt.sizeDelta = new Vector2(0f, 25f);

        TextMeshProUGUI subTmp = subGO.GetComponent<TextMeshProUGUI>();
        subTmp.text = "Safe Cracked & Treasure Secured!";
        subTmp.fontSize = 15;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.85f, 0.9f, 1f, 1f);

        // Coins Earned Text
        GameObject rewardGO = new GameObject("RewardText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rewardGO.transform.SetParent(cardGO.transform, false);
        RectTransform rewardRt = rewardGO.GetComponent<RectTransform>();
        rewardRt.anchorMin = new Vector2(0f, 1f); rewardRt.anchorMax = new Vector2(1f, 1f);
        rewardRt.pivot = new Vector2(0.5f, 1f);
        rewardRt.anchoredPosition = new Vector2(0f, -100f);
        rewardRt.sizeDelta = new Vector2(0f, 35f);

        victoryCoinsEarnedText = rewardGO.GetComponent<TextMeshProUGUI>();
        victoryCoinsEarnedText.text = "💰 +10 COINS EARNED!";
        victoryCoinsEarnedText.fontSize = 22;
        victoryCoinsEarnedText.fontStyle = FontStyles.Bold;
        victoryCoinsEarnedText.alignment = TextAlignmentOptions.Center;
        victoryCoinsEarnedText.color = new Color(0.2f, 1f, 0.4f, 1f); // Neon Green

        // Total Balance Text
        GameObject totalGO = new GameObject("TotalText", typeof(RectTransform), typeof(TextMeshProUGUI));
        totalGO.transform.SetParent(cardGO.transform, false);
        RectTransform totalRt = totalGO.GetComponent<RectTransform>();
        totalRt.anchorMin = new Vector2(0f, 1f); totalRt.anchorMax = new Vector2(1f, 1f);
        totalRt.pivot = new Vector2(0.5f, 1f);
        totalRt.anchoredPosition = new Vector2(0f, -135f);
        totalRt.sizeDelta = new Vector2(0f, 25f);

        victoryTotalCoinsText = totalGO.GetComponent<TextMeshProUGUI>();
        victoryTotalCoinsText.text = "Total Balance: 1010 Coins";
        victoryTotalCoinsText.fontSize = 14;
        victoryTotalCoinsText.alignment = TextAlignmentOptions.Center;
        victoryTotalCoinsText.color = new Color(0.7f, 0.8f, 0.95f, 1f);

        // Restart Match Button
        GameObject restartBtnGO = new GameObject("VictoryRestartButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        restartBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform restRt = restartBtnGO.GetComponent<RectTransform>();
        restRt.anchorMin = new Vector2(0.5f, 0f); restRt.anchorMax = new Vector2(0.5f, 0f);
        restRt.pivot = new Vector2(0.5f, 0f);
        restRt.anchoredPosition = new Vector2(0f, 75f);
        restRt.sizeDelta = new Vector2(280f, 45f);

        UnityEngine.UI.Image restImg = restartBtnGO.GetComponent<UnityEngine.UI.Image>();
        restImg.color = new Color(0.16f, 0.68f, 0.38f, 1f); // Rich Green

        GameObject restTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        restTextGO.transform.SetParent(restartBtnGO.transform, false);
        RectTransform rTextRt = restTextGO.GetComponent<RectTransform>();
        rTextRt.anchorMin = Vector2.zero; rTextRt.anchorMax = Vector2.one; rTextRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI restTmp = restTextGO.GetComponent<TextMeshProUGUI>();
        restTmp.text = "PLAY AGAIN";
        restTmp.fontSize = 19;
        restTmp.fontStyle = FontStyles.Bold;
        restTmp.alignment = TextAlignmentOptions.Center;
        restTmp.color = Color.white;

        Button restBtn = restartBtnGO.GetComponent<Button>();
        restBtn.onClick.AddListener(RestartMatch);

        // Main Menu Button
        GameObject menuBtnGO = new GameObject("VictoryMainMenuButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        menuBtnGO.transform.SetParent(cardGO.transform, false);
        RectTransform menuRt = menuBtnGO.GetComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, 0f); menuRt.anchorMax = new Vector2(0.5f, 0f);
        menuRt.pivot = new Vector2(0.5f, 0f);
        menuRt.anchoredPosition = new Vector2(0f, 22f);
        menuRt.sizeDelta = new Vector2(280f, 40f);

        UnityEngine.UI.Image menuImg = menuBtnGO.GetComponent<UnityEngine.UI.Image>();
        menuImg.color = new Color(0.25f, 0.3f, 0.42f, 1f);

        GameObject menuTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        menuTextGO.transform.SetParent(menuBtnGO.transform, false);
        RectTransform mTextRt = menuTextGO.GetComponent<RectTransform>();
        mTextRt.anchorMin = Vector2.zero; mTextRt.anchorMax = Vector2.one; mTextRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI menuTmp = menuTextGO.GetComponent<TextMeshProUGUI>();
        menuTmp.text = "MAIN MENU";
        menuTmp.fontSize = 16;
        menuTmp.fontStyle = FontStyles.Bold;
        menuTmp.alignment = TextAlignmentOptions.Center;
        menuTmp.color = Color.white;

        Button menuBtn = menuBtnGO.GetComponent<Button>();
        menuBtn.onClick.AddListener(ReturnToMainMenu);

        victoryPanel = panelGO;
        victoryPanel.SetActive(false);
    }

    public void RestartMatch()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
    }

    public void ReturnToMainMenu()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}

