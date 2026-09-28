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
    [Header("Slot Background Sprites")]
    public Sprite selectedSlotSprite;   // Blue / selected slot sprite
    public Sprite unselectedSlotSprite; // Gray / unselected slot sprite

    [Header("Fire Buttons")]
    public Button fireButton;     // Primary / Right Fire Button
    public Button leftFireButton; // Secondary / Left Fire Button

    [Header("Grenade")]
    public Button          boomButton;
    public TextMeshProUGUI boomCountText;

    [Header("Grenade Selection Bar (Bottom List)")]
    public GameObject grenadeBarPanel;
    public Button     grenadeSlotExplosive;
    public Button     grenadeSlotStun;
    public Button     grenadeSlotSmoke;

    [Header("Pickup Scroll View Panel")]
    public Button        pickupButton;
    public ScrollRect    pickupScrollView;
    public RectTransform pickupContentContainer; // Content container inside Viewport
    public GameObject    pickupItemPrefab;

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

    [Header("Custom Lobby & Player List Panel")]
    public GameObject customLobbyPanel;
    public Button     toggleLobbyPanelButton;   // ThreeDotsButton / Open panel button on HUD
    public Button     closeLobbyPanelButton;    // Close button inside panel
    public Button     copyRoomCodeButton;       // CopyIDButton
    public Button     startMatchButton;         // Start button (Host)
    public Button     leaveLobbyButton;         // LeaveButton
    public TextMeshProUGUI roomIDText;          // RoomIDText / JoinCode text
    public TextMeshProUGUI playerCountText;     // PLayerCount text
    public Transform  playerListContainer;      // PlayerListPanel container for player card entries
    public GameObject playerListCardPrefab;     // Player card prefab (white entry card)

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
    [SerializeField] public TextMeshProUGUI settingsInfoText;
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

        // Ensure Custom Lobby & Player List Panel UI is initialized & hidden by default
        EnsureCustomLobbyPanelUI();
        if (customLobbyPanel != null) customLobbyPanel.SetActive(false);

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
            BagManager.Instance.OnBagUpdated          -= HandleBagUpdated;
            BagManager.Instance.OnGrenadeUpdated      -= UpdateGrenadeUI;
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
        if (!isPlayerEventsBound && (BagManager.Instance != null || WeaponController.Instance != null || PlayerHealth.Instance != null || PlayerEnergy.Instance != null))
        {
            isPlayerEventsBound = true;
            BindLocalPlayer();
        }

        if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            ToggleBag();
        }

        UpdatePickupUI();
        UpdateHealthAndEnergyUIContinuous();
    }

    private void UpdateHealthAndEnergyUIContinuous()
    {
        if (healthSlider == null || energySlider == null)
        {
            AutoResolveHealthAndEnergyUI();
        }

        var health = PlayerHealth.Instance != null ? PlayerHealth.Instance : FindObjectOfType<PlayerHealth>();
        if (health != null)
        {
            UpdateHealthUI(health.GetCurrentHealth(), health.GetMaxHealth());
        }

        var energy = PlayerEnergy.Instance != null ? PlayerEnergy.Instance : FindObjectOfType<PlayerEnergy>();
        if (energy != null)
        {
            UpdateEnergyUI(energy.GetCurrentEnergy(), energy.GetMaxEnergy());
        }
    }

    private void EnsurePickupScrollView()
    {
        if (pickupScrollView != null && pickupItemPrefab != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Auto-find existing ScrollRect in Canvas if named containing "pickup"
        if (pickupScrollView == null)
        {
            ScrollRect[] srs = canvas.GetComponentsInChildren<ScrollRect>(true);
            foreach (var sr in srs)
            {
                if (sr != null && sr.gameObject.name.ToLower().Contains("pickup"))
                {
                    pickupScrollView = sr;
                    break;
                }
            }
        }

        // Dynamically create Pickup Scroll View on HUD canvas if unassigned
        if (pickupScrollView == null)
        {
            GameObject scrollGO = new GameObject("PickupScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(canvas.transform, false);

            RectTransform scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(1f, 0.5f);
            scrollRt.anchorMax = new Vector2(1f, 0.5f);
            scrollRt.pivot = new Vector2(1f, 0.5f);
            scrollRt.anchoredPosition = new Vector2(-60f, 0f); // Positioned on the right side
            scrollRt.sizeDelta = new Vector2(240f, 220f);

            Image scrollBg = scrollGO.GetComponent<Image>();
            scrollBg.color = new Color(0.08f, 0.12f, 0.18f, 0.85f); // Sleek dark panel

            // Viewport
            GameObject vpGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            vpGO.transform.SetParent(scrollGO.transform, false);
            RectTransform vpRt = vpGO.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one; vpRt.sizeDelta = Vector2.zero;

            Image vpImg = vpGO.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.05f);
            vpGO.GetComponent<Mask>().showMaskGraphic = false;

            // Content
            GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(vpGO.transform, false);
            RectTransform contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            pickupScrollView = scrollGO.GetComponent<ScrollRect>();
            pickupScrollView.content = contentRt;
            pickupScrollView.viewport = vpRt;
            pickupScrollView.horizontal = false;
            pickupScrollView.vertical = true;
            pickupScrollView.movementType = ScrollRect.MovementType.Elastic;
        }

        if (pickupContentContainer == null && pickupScrollView != null && pickupScrollView.content != null)
        {
            pickupContentContainer = pickupScrollView.content;
        }

        // Dynamically create Pickup Item Prefab template if unassigned
        if (pickupItemPrefab == null && pickupScrollView != null)
        {
            Transform parent = pickupContentContainer != null ? (Transform)pickupContentContainer : (pickupScrollView.content != null ? (Transform)pickupScrollView.content : pickupScrollView.transform);

            GameObject itemGO = new GameObject("PickupItemTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PickupItemEntryUI));
            itemGO.transform.SetParent(parent, false);

            RectTransform itemRt = itemGO.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(0f, 50f);

            Image itemBg = itemGO.GetComponent<Image>();
            itemBg.color = new Color(0.18f, 0.24f, 0.32f, 0.95f); // Sleek item entry button background

            // Image Icon Child
            GameObject iconGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(itemGO.transform, false);
            RectTransform iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(8f, 0f);
            iconRt.sizeDelta = new Vector2(38f, 38f);

            Image iconImg = iconGO.GetComponent<Image>();
            iconImg.preserveAspect = true;

            // Name Text Child
            GameObject nameGO = new GameObject("ItemNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(itemGO.transform, false);
            RectTransform nameRt = nameGO.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(52f, 2f);
            nameRt.offsetMax = new Vector2(-6f, -2f);

            TextMeshProUGUI tmp = nameGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "Item Name";
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;

            // Wire PickupItemEntryUI component references
            PickupItemEntryUI entryUI = itemGO.GetComponent<PickupItemEntryUI>();
            if (entryUI != null)
            {
                entryUI.itemIcon = iconImg;
                entryUI.itemName = tmp;
                entryUI.pickButton = itemGO.GetComponent<Button>();
            }

            itemGO.SetActive(false); // Template stays inactive until instantiated
            pickupItemPrefab = itemGO;
        }

        // Scroll View is hidden by default until player is standing near an item
        if (pickupScrollView != null && !isPickupUIInitialized)
        {
            pickupScrollView.gameObject.SetActive(false);
        }
    }

    private void UpdatePickupUI()
    {
        EnsurePickupScrollView();

        if (pickupScrollView == null) return;

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

        // Clear previous spawned item entries in ScrollView
        foreach (var btn in spawnedPickupButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        spawnedPickupButtons.Clear();

        // Hide Scroll View when no items are in range (player is not standing near any item)
        if (currentPickups.Count == 0)
        {
            pickupScrollView.gameObject.SetActive(false);
            if (pickupButton != null) pickupButton.gameObject.SetActive(false);
            return;
        }

        // Show Scroll View ONLY when player is standing near items
        pickupScrollView.gameObject.SetActive(true);
        if (pickupButton != null) pickupButton.gameObject.SetActive(false);

        // Parent item entries directly under Content (inside Viewport)
        Transform contentParent = pickupContentContainer != null ? (Transform)pickupContentContainer : (pickupScrollView.content != null ? (Transform)pickupScrollView.content : pickupScrollView.transform);

        // 1. Manage item spacing and height expansion from code
        var vlg = contentParent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = contentParent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;                   // Compact 4px spacing between items
        vlg.childForceExpandHeight = false; // Prevent items from stretching apart vertically!
        vlg.childControlHeight = true;      // Control item row heights cleanly
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        var csf = contentParent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = contentParent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 2. Hide scrollbar if there are less than 4 items
        bool showScrollbar = currentPickups.Count >= 4;
        pickupScrollView.vertical = showScrollbar;

        if (pickupScrollView.verticalScrollbar != null)
        {
            pickupScrollView.verticalScrollbar.gameObject.SetActive(showScrollbar);
        }

        Scrollbar[] scrollbars = pickupScrollView.GetComponentsInChildren<Scrollbar>(true);
        foreach (var sb in scrollbars)
        {
            if (sb != null)
            {
                sb.gameObject.SetActive(showScrollbar);
            }
        }

        for (int i = 0; i < currentPickups.Count; i++)
        {
            var pickup = currentPickups[i];
            if (pickup == null || pickup.itemData == null) continue;

            GameObject entryGO;
            if (pickupItemPrefab != null)
            {
                entryGO = Instantiate(pickupItemPrefab, contentParent, false);
            }
            else
            {
                entryGO = new GameObject($"PickupItem_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(PickupItemEntryUI));
                entryGO.transform.SetParent(contentParent, false);
            }

            entryGO.SetActive(true);

            // Ensure compact LayoutElement height on item entry
            var le = entryGO.GetComponent<LayoutElement>();
            if (le == null) le = entryGO.AddComponent<LayoutElement>();
            le.minHeight = 45f;
            le.preferredHeight = 45f;
            le.flexibleHeight = 0f;

            // Get or add PickupItemEntryUI component
            PickupItemEntryUI entryUI = entryGO.GetComponent<PickupItemEntryUI>();
            if (entryUI == null) entryUI = entryGO.AddComponent<PickupItemEntryUI>();

            // Auto-resolve components if unassigned on entryUI
            if (entryUI.itemName == null) entryUI.itemName = entryGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (entryUI.itemIcon == null)
            {
                Image[] imgs = entryGO.GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img != null && img.gameObject != entryGO && img.gameObject.name.ToLower().Contains("icon"))
                    {
                        entryUI.itemIcon = img;
                        break;
                    }
                }
                if (entryUI.itemIcon == null && imgs.Length > 1) entryUI.itemIcon = imgs[1];
            }
            if (entryUI.pickButton == null) entryUI.pickButton = entryGO.GetComponent<Button>();

            // Configure Item Icon Image, Name Text, and Pick Button Callback
            entryUI.Setup(pickup.itemData, () => pickup.PickingUpManually());

            if (entryUI.pickButton != null)
            {
                spawnedPickupButtons.Add(entryUI.pickButton);
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

        AutoResolveHealthAndEnergyUI();

        var health = PlayerHealth.Instance != null ? PlayerHealth.Instance : FindObjectOfType<PlayerHealth>();
        if (health != null)
        {
            health.OnHealthChanged -= UpdateHealthUI;
            health.OnHealthChanged += UpdateHealthUI;
            UpdateHealthUI(health.GetCurrentHealth(), health.GetMaxHealth());
        }

        var energy = PlayerEnergy.Instance != null ? PlayerEnergy.Instance : FindObjectOfType<PlayerEnergy>();
        if (energy != null)
        {
            energy.OnEnergyChanged -= UpdateEnergyUI;
            energy.OnEnergyChanged += UpdateEnergyUI;
            UpdateEnergyUI(energy.GetCurrentEnergy(), energy.GetMaxEnergy());
        }

        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
    }

    private void HandleBagUpdated()
    {
        if (this == null || transform == null || !gameObject.activeInHierarchy) return;
        RefreshWeaponSlotUI(0);
        RefreshWeaponSlotUI(1);
        if (BagManager.Instance != null)
        {
            UpdateGrenadeUI(BagManager.Instance.activeGrenadeType, BagManager.Instance.GetGrenadeCount(BagManager.Instance.activeGrenadeType));
        }
    }

    public Sprite GetGrenadeIconSprite(GrenadeType type)
    {
        if (BagManager.Instance != null && BagManager.Instance.allItemData != null)
        {
            var data = BagManager.Instance.allItemData.Find(x => x != null && x.itemType == ItemType.Grenade && x.grenadeType == type);
            if (data != null && data.icon != null) return data.icon;
        }
        return null;
    }

    private void EnsureGrenadeSelectionBar()
    {
        if (this == null || transform == null || !gameObject.activeInHierarchy) return;
        if (grenadeBarPanel != null && grenadeSlotExplosive != null && grenadeSlotStun != null && grenadeSlotSmoke != null)
        {
            WireGrenadeSlotListeners();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Auto-find existing grenade slot buttons in Canvas if unassigned
        if (grenadeSlotExplosive == null || grenadeSlotStun == null || grenadeSlotSmoke == null)
        {
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                string n = btn.gameObject.name.ToLower();
                if (grenadeSlotExplosive == null && (n.Contains("explosive") || (n.Contains("boom") && n.Contains("slot")))) grenadeSlotExplosive = btn;
                if (grenadeSlotStun == null && n.Contains("stun")) grenadeSlotStun = btn;
                if (grenadeSlotSmoke == null && n.Contains("smoke")) grenadeSlotSmoke = btn;
            }
        }

        // 2. Dynamically Create Grenade Selection Bar container at bottom after guns if missing
        if (grenadeBarPanel == null)
        {
            GameObject barGO = new GameObject("GrenadeSelectionBar", typeof(RectTransform));
            barGO.transform.SetParent(canvas.transform, false);

            RectTransform barRt = barGO.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0f);
            barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(275f, 67.2f); // Placed at bottom bar after GunSlot2
            barRt.sizeDelta = new Vector2(230f, 75f);

            grenadeBarPanel = barGO;
        }

        // 3. Create missing slots inside grenadeBarPanel
        if (grenadeSlotExplosive == null && grenadeBarPanel != null)
        {
            grenadeSlotExplosive = CreateGrenadeSlotButton(grenadeBarPanel.transform, "Slot_Explosive", GrenadeType.Explosive, new Vector2(-75f, 0f));
        }
        if (grenadeSlotStun == null && grenadeBarPanel != null)
        {
            grenadeSlotStun = CreateGrenadeSlotButton(grenadeBarPanel.transform, "Slot_Stun", GrenadeType.Stun, new Vector2(0f, 0f));
        }
        if (grenadeSlotSmoke == null && grenadeBarPanel != null)
        {
            grenadeSlotSmoke = CreateGrenadeSlotButton(grenadeBarPanel.transform, "Slot_Smoke", GrenadeType.Smoke, new Vector2(75f, 0f));
        }

        WireGrenadeSlotListeners();
    }

    private void WireGrenadeSlotListeners()
    {
        if (grenadeSlotExplosive != null)
        {
            grenadeSlotExplosive.onClick.RemoveAllListeners();
            grenadeSlotExplosive.onClick.AddListener(() => OnSelectGrenadeClicked(GrenadeType.Explosive));
        }
        if (grenadeSlotStun != null)
        {
            grenadeSlotStun.onClick.RemoveAllListeners();
            grenadeSlotStun.onClick.AddListener(() => OnSelectGrenadeClicked(GrenadeType.Stun));
        }
        if (grenadeSlotSmoke != null)
        {
            grenadeSlotSmoke.onClick.RemoveAllListeners();
            grenadeSlotSmoke.onClick.AddListener(() => OnSelectGrenadeClicked(GrenadeType.Smoke));
        }
    }

    private Button CreateGrenadeSlotButton(Transform parent, string name, GrenadeType type, Vector2 anchoredPos)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(65f, 65f);

        Image bgImg = btnGO.GetComponent<Image>();
        if (unselectedSlotSprite != null)
        {
            bgImg.sprite = unselectedSlotSprite;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        }

        // Icon Image Child
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(btnGO.transform, false);
        RectTransform iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.15f, 0.15f);
        iconRt.anchorMax = new Vector2(0.85f, 0.85f);
        iconRt.sizeDelta = Vector2.zero;

        Image iconImg = iconGO.GetComponent<Image>();
        Sprite iconSprite = GetGrenadeIconSprite(type);
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = new Color(0, 0, 0, 0);
        }

        // Count Text Child
        GameObject textGO = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.35f);
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.color = Color.yellow;
        tmp.text = "0";

        return btnGO.GetComponent<Button>();
    }

    private void OnSelectGrenadeClicked(GrenadeType type)
    {
        if (BagManager.Instance == null) return;
        int count = BagManager.Instance.GetGrenadeCount(type);
        if (count > 0 || BagManager.Instance.activeGrenadeType == type)
        {
            BagManager.Instance.EquipGrenade(type);
            UpdateGrenadeUI(type, count);
        }
        else
        {
            ShowNotification($"⚠️ No {type} Grenades in inventory!");
        }
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────

    private void UpdateGrenadeUI(GrenadeType type, int count)
    {
        EnsureGrenadeSelectionBar();

        if (BagManager.Instance == null)
        {
            if (boomButton != null) boomButton.gameObject.SetActive(false);
            if (grenadeBarPanel != null) grenadeBarPanel.SetActive(false);
            return;
        }

        GrenadeType activeType = BagManager.Instance.activeGrenadeType;

        // Auto-switch to another available grenade type if active runs out
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
            boomButton.gameObject.SetActive(true);
            boomButton.interactable = hasGrenades;

            // Find child icon Image or target Image on boomButton
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
                Sprite activeIcon = GetGrenadeIconSprite(activeType);
                if (activeIcon != null)
                {
                    targetImg.sprite = activeIcon;
                    targetImg.preserveAspect = true;
                    targetImg.color = hasGrenades ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        if (boomCountText != null)
        {
            boomCountText.text = activeCount.ToString();
            boomCountText.gameObject.SetActive(true);
        }

        // Update Bottom Grenade Selection List Slots (Explosive, Stun, Smoke)
        UpdateSingleGrenadeSlotUI(grenadeSlotExplosive, GrenadeType.Explosive, activeType);
        UpdateSingleGrenadeSlotUI(grenadeSlotStun,      GrenadeType.Stun,      activeType);
        UpdateSingleGrenadeSlotUI(grenadeSlotSmoke,     GrenadeType.Smoke,     activeType);
    }

    private void UpdateSingleGrenadeSlotUI(Button slotBtn, GrenadeType slotType, GrenadeType activeType)
    {
        if (slotBtn == null || BagManager.Instance == null) return;

        int count = BagManager.Instance.GetGrenadeCount(slotType);
        bool isSelected = (slotType == activeType);

        // Update Background Frame (Blue for selected, Gray for unselected)
        Image bgImg = slotBtn.GetComponent<Image>();
        if (bgImg != null)
        {
            if (isSelected)
            {
                if (selectedSlotSprite != null)
                {
                    bgImg.sprite = selectedSlotSprite;
                    bgImg.color = Color.white;
                }
                else
                {
                    bgImg.color = new Color(0.15f, 0.55f, 0.95f, 1f);
                }
            }
            else
            {
                if (unselectedSlotSprite != null)
                {
                    bgImg.sprite = unselectedSlotSprite;
                    bgImg.color = Color.white;
                }
                else
                {
                    bgImg.color = new Color(0.35f, 0.35f, 0.35f, 0.85f);
                }
            }
        }

        // Update Icon Sprite
        Image iconImg = null;
        Image[] imgs = slotBtn.GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            if (img != null && img.gameObject != slotBtn.gameObject)
            {
                iconImg = img;
                break;
            }
        }
        if (iconImg != null)
        {
            Sprite iconSprite = GetGrenadeIconSprite(slotType);
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
                iconImg.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                iconImg.gameObject.SetActive(true);
            }
        }

        // Update Count Text
        TextMeshProUGUI tmpText = slotBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpText != null)
        {
            tmpText.text = count.ToString();
            tmpText.color = count > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.6f);
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

    private struct FillRectState
    {
        public bool isCached;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector3 localScale;
    }

    private FillRectState healthFillState;
    private FillRectState energyFillState;

    private void CacheSliderFillBounds()
    {
        if (!healthFillState.isCached && healthSlider != null && healthSlider.fillRect != null)
        {
            healthFillState.isCached = true;
            healthFillState.offsetMin = healthSlider.fillRect.offsetMin;
            healthFillState.offsetMax = healthSlider.fillRect.offsetMax;
            healthFillState.localScale = healthSlider.fillRect.localScale;
        }
        if (!energyFillState.isCached && energySlider != null && energySlider.fillRect != null)
        {
            energyFillState.isCached = true;
            energyFillState.offsetMin = energySlider.fillRect.offsetMin;
            energyFillState.offsetMax = energySlider.fillRect.offsetMax;
            energyFillState.localScale = energySlider.fillRect.localScale;
        }
    }

    private void RestoreSliderFillBounds(Slider slider, ref FillRectState state)
    {
        if (!state.isCached || slider == null || slider.fillRect == null) return;

        RectTransform fillRt = slider.fillRect;
        float norm = Mathf.Clamp01(slider.maxValue > 0 ? (slider.value / slider.maxValue) : 0f);

        Vector2 min = fillRt.offsetMin;
        Vector2 max = fillRt.offsetMax;

        // Restore Left (min.x) and Right (max.x) padding set in Inspector
        min.x = state.offsetMin.x;
        max.x = Mathf.Lerp(0f, state.offsetMax.x, norm);

        // Restore Bottom (min.y) and Top (max.y) padding set in Inspector
        min.y = state.offsetMin.y;
        max.y = state.offsetMax.y;

        fillRt.offsetMin = min;
        fillRt.offsetMax = max;
        fillRt.localScale = state.localScale;
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
            string pName = s.transform.parent != null ? s.transform.parent.gameObject.name.ToLower() : "";
            string grandPName = (s.transform.parent != null && s.transform.parent.parent != null) ? s.transform.parent.parent.gameObject.name.ToLower() : "";
            string fullName = $"{grandPName}_{pName}_{sName}";

            if (healthSlider == null && (fullName.Contains("health") || fullName.Contains("hp") || fullName.Contains("life")))
            {
                healthSlider = s;
            }
            else if (energySlider == null && (fullName.Contains("energy") || fullName.Contains("stamina") || fullName.Contains("boost") || fullName.Contains("mana") || fullName.Contains("power")))
            {
                energySlider = s;
            }
        }

        TextMeshProUGUI[] tmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in tmps)
        {
            if (t == null) continue;
            string tName = t.gameObject.name.ToLower();
            string pName = t.transform.parent != null ? t.transform.parent.gameObject.name.ToLower() : "";
            string grandPName = (t.transform.parent != null && t.transform.parent.parent != null) ? t.transform.parent.parent.gameObject.name.ToLower() : "";
            string fullName = $"{grandPName}_{pName}_{tName}";

            if (healthText == null && (fullName.Contains("health") || fullName.Contains("hp")))
            {
                healthText = t;
            }
            else if (energyText == null && (fullName.Contains("energy") || fullName.Contains("stamina") || fullName.Contains("boost")))
            {
                energyText = t;
            }
        }

        CacheSliderFillBounds();
    }

    private void UpdateHealthUI(int current, int max)
    {
        CacheSliderFillBounds();

        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value    = current;

            // Restore fill bounds (left, right, top, bottom) and localScale so right edge NEVER goes out
            RestoreSliderFillBounds(healthSlider, ref healthFillState);
        }
        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }

    private void UpdateEnergyUI(float current, float max)
    {
        CacheSliderFillBounds();

        if (energySlider != null)
        {
            energySlider.maxValue = max;
            energySlider.value    = current;

            // Restore fill bounds (left, right, top, bottom) and localScale so right edge NEVER goes out
            RestoreSliderFillBounds(energySlider, ref energyFillState);
        }
        if (energyText != null)
            energyText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
    }

    private void EnsureSlotSprites()
    {
        if (selectedSlotSprite != null && unselectedSlotSprite != null) return;

        Image img1 = weaponSlot1 != null ? weaponSlot1.GetComponent<Image>() : null;
        Image img2 = weaponSlot2 != null ? weaponSlot2.GetComponent<Image>() : null;

        if (img1 != null && img1.sprite != null)
        {
            string sName = img1.sprite.name;
            if (sName.EndsWith("_5") || sName.ToLower().Contains("blue") || sName.ToLower().Contains("selected"))
            {
                if (selectedSlotSprite == null) selectedSlotSprite = img1.sprite;
            }
            else if (sName.EndsWith("_4") || sName.ToLower().Contains("gray") || sName.ToLower().Contains("unselected"))
            {
                if (unselectedSlotSprite == null) unselectedSlotSprite = img1.sprite;
            }
        }

        if (img2 != null && img2.sprite != null)
        {
            string sName = img2.sprite.name;
            if (sName.EndsWith("_5") || sName.ToLower().Contains("blue") || sName.ToLower().Contains("selected"))
            {
                if (selectedSlotSprite == null) selectedSlotSprite = img2.sprite;
            }
            else if (sName.EndsWith("_4") || sName.ToLower().Contains("gray") || sName.ToLower().Contains("unselected"))
            {
                if (unselectedSlotSprite == null) unselectedSlotSprite = img2.sprite;
            }
        }

        if (selectedSlotSprite == null && img1 != null) selectedSlotSprite = img1.sprite;
        if (unselectedSlotSprite == null && img2 != null) unselectedSlotSprite = img2.sprite;
    }

    /// <summary>
    /// Called once on Start and whenever the weapon slot contents change,
    /// to refresh icon, background frame sprite, and static ammo display for a slot.
    /// </summary>
    private void RefreshWeaponSlotUI(int slotIndex)
    {
        EnsureSlotSprites();

        var weapon    = BagManager.Instance?.GetWeaponInSlot(slotIndex);
        var ammoText  = slotIndex == 0 ? weapon1AmmoText : weapon2AmmoText;
        var icon      = slotIndex == 0 ? weapon1Icon     : weapon2Icon;
        Button slotBtn = slotIndex == 0 ? weaponSlot1 : weaponSlot2;

        // Dynamic background frame selection (Selected sprite vs Unselected sprite)
        if (slotBtn != null)
        {
            Image bgImg = slotBtn.GetComponent<Image>();
            if (bgImg != null)
            {
                int activeSlot = WeaponController.Instance != null ? WeaponController.Instance.GetCurrentSlot() : -1;
                bool isSelected = (activeSlot == slotIndex) && (weapon != null);

                if (isSelected)
                {
                    if (selectedSlotSprite != null)
                    {
                        bgImg.sprite = selectedSlotSprite;
                        bgImg.color = Color.white;
                    }
                    else
                    {
                        bgImg.color = new Color(0.15f, 0.55f, 0.95f, 1f); // Fallback blue
                    }
                }
                else
                {
                    if (unselectedSlotSprite != null)
                    {
                        bgImg.sprite = unselectedSlotSprite;
                        bgImg.color = Color.white;
                    }
                    else
                    {
                        bgImg.color = new Color(0.45f, 0.45f, 0.45f, 1f); // Fallback gray
                    }
                }
            }
        }

        // Auto-find slot icon Image if unassigned in inspector
        if (icon == null && slotBtn != null)
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

    // ─── Custom Lobby & Player List Panel Management ─────────────────────────

    private System.Collections.Generic.List<GameObject> spawnedPlayerCards = new System.Collections.Generic.List<GameObject>();

    public void EnsureCustomLobbyPanelUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Auto-find customLobbyPanel if unassigned
        if (customLobbyPanel == null)
        {
            Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t == null) continue;
                string tName = t.gameObject.name.ToLower();
                if (tName.Contains("customlobbypanel") || tName == "lobbypanel" || tName.Contains("playerlistpanel"))
                {
                    customLobbyPanel = t.gameObject;
                    break;
                }
            }
        }

        // 2. Auto-find toggleLobbyPanelButton (ThreeDotsButton)
        if (toggleLobbyPanelButton == null)
        {
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                string bName = b.gameObject.name.ToLower();
                if (bName.Contains("threedots") || bName.Contains("togglelobby") || bName == "dotsbutton")
                {
                    toggleLobbyPanelButton = b;
                    break;
                }
            }
        }

        // 3. Auto-find buttons, texts, and playerListContainer inside customLobbyPanel
        if (customLobbyPanel != null)
        {
            Button[] panelButtons = customLobbyPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in panelButtons)
            {
                if (b == null) continue;
                string bName = b.gameObject.name.ToLower();
                if (closeLobbyPanelButton == null && (bName == "close" || bName.Contains("close"))) closeLobbyPanelButton = b;
                if (copyRoomCodeButton == null && (bName.Contains("copy") || bName.Contains("code"))) copyRoomCodeButton = b;
                if (startMatchButton == null && (bName == "start" || bName.Contains("start"))) startMatchButton = b;
                if (leaveLobbyButton == null && (bName.Contains("leave") || bName == "exit")) leaveLobbyButton = b;
            }

            TextMeshProUGUI[] panelTexts = customLobbyPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in panelTexts)
            {
                if (txt == null) continue;
                string tName = txt.gameObject.name.ToLower();
                if (roomIDText == null && (tName.Contains("roomid") || tName.Contains("joincode") || tName.Contains("code"))) roomIDText = txt;
                if (playerCountText == null && (tName.Contains("playercount") || tName.Contains("count"))) playerCountText = txt;
            }

            if (playerListContainer == null)
            {
                Transform[] childTransforms = customLobbyPanel.GetComponentsInChildren<Transform>(true);
                foreach (var ct in childTransforms)
                {
                    if (ct == null || ct.gameObject == customLobbyPanel) continue;
                    string ctName = ct.gameObject.name.ToLower();
                    if (ctName.Contains("playerlistpanel") || ctName.Contains("content") || ctName.Contains("playerlist"))
                    {
                        playerListContainer = ct;
                        break;
                    }
                }
            }
        }

        // Configure playerListContainer layout (tight spacing, no vertical expansion stretching)
        if (playerListContainer != null)
        {
            VerticalLayoutGroup vlg = playerListContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = playerListContainer.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing = 6f; // Compact 6px gap between player cards
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false; // Prevent items from stretching apart vertically
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            ContentSizeFitter csf = playerListContainer.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = playerListContainer.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // Wire button listeners
        if (toggleLobbyPanelButton != null)
        {
            toggleLobbyPanelButton.onClick.RemoveAllListeners();
            toggleLobbyPanelButton.onClick.AddListener(ToggleCustomLobbyPanel);
        }

        if (closeLobbyPanelButton != null)
        {
            closeLobbyPanelButton.onClick.RemoveAllListeners();
            closeLobbyPanelButton.onClick.AddListener(CloseCustomLobbyPanel);
        }

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.onClick.RemoveAllListeners();
            copyRoomCodeButton.onClick.AddListener(CopyRoomCodeToClipboard);
        }

        if (startMatchButton != null)
        {
            startMatchButton.onClick.RemoveAllListeners();
            startMatchButton.onClick.AddListener(OnStartMatchFromLobbyClicked);
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.RemoveAllListeners();
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyFromPanelClicked);
        }
    }

    public void ToggleCustomLobbyPanel()
    {
        if (customLobbyPanel != null)
        {
            bool active = !customLobbyPanel.activeSelf;
            SetCustomLobbyPanelVisible(active);
        }
        else
        {
            EnsureCustomLobbyPanelUI();
            if (customLobbyPanel != null)
            {
                customLobbyPanel.SetActive(true);
                RefreshLobbyPanelUI();
            }
        }
    }

    public void SetCustomLobbyPanelVisible(bool visible)
    {
        EnsureCustomLobbyPanelUI();
        if (customLobbyPanel != null)
        {
            customLobbyPanel.SetActive(visible);
            if (visible)
            {
                RefreshLobbyPanelUI();
            }
        }
    }

    public void OpenCustomLobbyPanel() => SetCustomLobbyPanelVisible(true);
    public void CloseCustomLobbyPanel() => SetCustomLobbyPanelVisible(false);

    public void RefreshLobbyPanelUI()
    {
        EnsureCustomLobbyPanelUI();

        // 1. Update Join / Room Code Text (format: "CODE: T67GTO", no "Room Code:")
        string joinCode = "";
        if (RelayNetworkManager.Instance != null && !string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
        {
            joinCode = RelayNetworkManager.Instance.CurrentJoinCode;
        }

        if (roomIDText != null)
        {
            roomIDText.text = !string.IsNullOrEmpty(joinCode) ? $"CODE: {joinCode}" : "CODE: ---";
        }

        // 2. Populate / Refresh Player List
        RefreshLobbyPlayerList();
    }

    public void RefreshLobbyPlayerList()
    {
        if (playerListContainer == null) return;

        // Clear existing spawned player cards
        foreach (var card in spawnedPlayerCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedPlayerCards.Clear();

        var playerList = GetConnectedPlayerList();
        int playerCount = playerList.Count;
        bool localIsHost = false;

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            localIsHost = Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsServer;
        }
        else
        {
            localIsHost = true;
        }

        for (int i = 0; i < playerList.Count; i++)
        {
            var pData = playerList[i];
            bool isFriend = pData.isHost || (i == 0);
            if (CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                isFriend = isFriend || CounterBoom.Networking.FirebaseManager.Instance.IsFriend(pData.name);
            }
            Sprite headSprite = GetPlayerHeadSprite(pData.playerObj);

            CreatePlayerCardEntry(pData.name, pData.isHost, isFriend, headSprite);
        }

        // Update Player Count Text
        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {playerCount}";
        }

        // Show Start Match button ONLY for Host
        if (startMatchButton != null)
        {
            startMatchButton.gameObject.SetActive(localIsHost);
        }
    }

    private System.Collections.Generic.List<(string name, bool isHost, GameObject playerObj)> GetConnectedPlayerList()
    {
        var list = new System.Collections.Generic.List<(string name, bool isHost, GameObject playerObj)>();

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            PlayerController[] players = FindObjectsOfType<PlayerController>();

            if (nm.IsServer)
            {
                var clients = nm.ConnectedClientsList;
                for (int i = 0; i < clients.Count; i++)
                {
                    var client = clients[i];
                    ulong clientId = client.ClientId;
                    GameObject pObj = client.PlayerObject != null ? client.PlayerObject.gameObject : null;

                    PlayerController matchingPC = null;
                    if (pObj != null) matchingPC = pObj.GetComponent<PlayerController>();
                    if (matchingPC == null && players != null)
                    {
                        foreach (var pc in players)
                        {
                            if (pc != null && (pc.OwnerClientId == clientId || (pc.IsLocal && clientId == 0)))
                            {
                                matchingPC = pc;
                                if (pObj == null) pObj = pc.gameObject;
                                break;
                            }
                        }
                    }

                    bool clientIsHost = (matchingPC != null && matchingPC.lobbySlotIndex.Value == 0) || (clientId == Unity.Netcode.NetworkManager.ServerClientId);
                    string pName = GetActualPlayerName(clientId, matchingPC, pObj);

                    list.Add((pName, clientIsHost, pObj));
                }
            }
            else
            {
                if (players != null && players.Length > 0)
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        var pc = players[i];
                        bool clientIsHost = (pc.lobbySlotIndex.Value == 0) || pc.IsHost || pc.IsServer || (pc.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId);
                        string pName = GetActualPlayerName(pc.OwnerClientId, pc, pc.gameObject);

                        list.Add((pName, clientIsHost, pc.gameObject));
                    }
                }
                else
                {
                    var local = PlayerController.LocalPlayer ?? FindObjectOfType<PlayerController>();
                    GameObject pObj = local != null ? local.gameObject : null;
                    string pName = GetActualPlayerName(0, local, pObj);
                    list.Add((pName, true, pObj));
                }
            }
        }
        else
        {
            // Offline Mode
            var local = PlayerController.LocalPlayer ?? FindObjectOfType<PlayerController>();
            GameObject pObj = local != null ? local.gameObject : null;
            string pName = GetActualPlayerName(0, local, pObj);
            list.Add((pName, true, pObj));
        }

        return list;
    }

    private string GetActualPlayerName(ulong clientId, PlayerController pc = null, GameObject pObj = null)
    {
        if (pc != null)
        {
            string n = pc.playerName.Value.ToString();
            if (!string.IsNullOrEmpty(n) && !n.StartsWith("Player(Clone)")) return n;
        }

        if (pObj != null)
        {
            var pController = pObj.GetComponent<PlayerController>() ?? pObj.GetComponentInChildren<PlayerController>();
            if (pController != null)
            {
                string n = pController.playerName.Value.ToString();
                if (!string.IsNullOrEmpty(n) && !n.StartsWith("Player(Clone)")) return n;
            }
        }

        foreach (var p in FindObjectsOfType<PlayerController>())
        {
            if (p != null && (p.OwnerClientId == clientId || (p.IsLocal && clientId == 0)))
            {
                string n = p.playerName.Value.ToString();
                if (!string.IsNullOrEmpty(n) && !n.StartsWith("Player(Clone)")) return n;
            }
        }

        string localName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(localName) && (Unity.Netcode.NetworkManager.Singleton == null || clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId))
        {
            return localName;
        }

        return $"Player #{clientId + 1}";
    }

    private Sprite GetPlayerHeadSprite(GameObject playerObj = null)
    {
        // 1. Try from playerObj's CharacterAssembler
        if (playerObj != null)
        {
            var assembler = playerObj.GetComponent<CharacterAssembler>() ?? playerObj.GetComponentInChildren<CharacterAssembler>();
            if (assembler != null)
            {
                Transform headTr = assembler.GetHeadTransform();
                if (headTr != null)
                {
                    var sr = headTr.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) return sr.sprite;
                }
                var skins = assembler.GetAvailableSkins();
                if (skins != null && skins.Length > 0 && skins[0] != null && skins[0].head != null)
                {
                    return skins[0].head;
                }
            }
        }

        // 2. Try from local player
        var localPlayer = PlayerController.LocalPlayer ?? FindObjectOfType<PlayerController>();
        if (localPlayer != null)
        {
            var assembler = localPlayer.GetComponent<CharacterAssembler>() ?? localPlayer.GetComponentInChildren<CharacterAssembler>();
            if (assembler != null)
            {
                Transform headTr = assembler.GetHeadTransform();
                if (headTr != null)
                {
                    var sr = headTr.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) return sr.sprite;
                }
                var skins = assembler.GetAvailableSkins();
                if (skins != null && skins.Length > 0 && skins[0] != null && skins[0].head != null)
                {
                    return skins[0].head;
                }
            }
        }

        // 3. Fallback: try any CharacterAssembler in scene
        var anyAssembler = FindObjectOfType<CharacterAssembler>();
        if (anyAssembler != null)
        {
            Transform headTr = anyAssembler.GetHeadTransform();
            if (headTr != null)
            {
                var sr = headTr.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null) return sr.sprite;
            }
            var skins = anyAssembler.GetAvailableSkins();
            if (skins != null && skins.Length > 0 && skins[0] != null && skins[0].head != null)
            {
                return skins[0].head;
            }
        }

        return null;
    }

    private void CreatePlayerCardEntry(string playerName, bool isHost, bool isFriend = false, Sprite profileAvatar = null)
    {
        if (playerListContainer == null) return;

        GameObject cardGO;
        if (playerListCardPrefab != null)
        {
            cardGO = Instantiate(playerListCardPrefab, playerListContainer, false);
        }
        else
        {
            // Fallback dynamic card entry if prefab unassigned
            cardGO = new GameObject("PlayerCard", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(playerListContainer, false);

            RectTransform rt = cardGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 55f);

            Image img = cardGO.GetComponent<Image>();
            img.color = new Color(0.95f, 0.95f, 0.98f, 0.95f);

            // Profile Avatar Child
            GameObject avatarGO = new GameObject("ProfileAvatar", typeof(RectTransform), typeof(Image));
            avatarGO.transform.SetParent(cardGO.transform, false);
            RectTransform avRt = avatarGO.GetComponent<RectTransform>();
            avRt.anchorMin = new Vector2(0f, 0.5f);
            avRt.anchorMax = new Vector2(0f, 0.5f);
            avRt.pivot = new Vector2(0f, 0.5f);
            avRt.anchoredPosition = new Vector2(10f, 0f);
            avRt.sizeDelta = new Vector2(40f, 40f);

            Image avImg = avatarGO.GetComponent<Image>();
            avImg.color = new Color(0.8f, 0.85f, 0.95f, 1f);

            // Name Text Child
            GameObject textGO = new GameObject("PlayerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(cardGO.transform, false);

            RectTransform tRt = textGO.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = new Vector2(58f, 0f);
            tRt.offsetMax = new Vector2(-125f, 0f);

            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.black;
        }

        cardGO.SetActive(true);

        // Ensure LayoutElement height constraint so VerticalLayoutGroup doesn't stretch cards apart
        LayoutElement le = cardGO.GetComponent<LayoutElement>();
        if (le == null) le = cardGO.AddComponent<LayoutElement>();
        le.preferredHeight = 50f;
        le.flexibleHeight = 0f;

        // Check if prefab has dedicated PlayerCardUI component attached
        var cardUI = cardGO.GetComponent<CounterBoom.UI.PlayerCardUI>();
        if (cardUI != null)
        {
            cardUI.SetupCard(playerName, isHost, isFriend, profileAvatar, () =>
            {
                ShowNotification($"📩 Invite sent to {playerName}!");
            });
        }
        else
        {
            // 1. Bind Player Profile Avatar Image
            Image avatarImg = cardGO.transform.Find("ProfileAvatar")?.GetComponent<Image>();
            if (avatarImg == null)
            {
                Image[] imgs = cardGO.GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    if (img != null && img.gameObject != cardGO && (img.gameObject.name.ToLower().Contains("profile") || img.gameObject.name.ToLower().Contains("avatar")))
                    {
                        avatarImg = img;
                        break;
                    }
                }
            }
            if (avatarImg != null && profileAvatar != null)
            {
                avatarImg.sprite = profileAvatar;
                avatarImg.preserveAspect = true;
                avatarImg.color = Color.white;
            }

            // 2. Bind Player Name
            TextMeshProUGUI nameText = cardGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText != null)
            {
                nameText.text = playerName;
            }

            // 3. Host Indicator inside card
            Transform hostIndicator = cardGO.transform.Find("HostIndicator");
            if (hostIndicator != null)
            {
                hostIndicator.gameObject.SetActive(isHost);
            }

            // 4. Friend Status & Invite Button / Friend Icon from Code
            SetupFriendAndInviteUI(cardGO, playerName, isFriend);
        }

        spawnedPlayerCards.Add(cardGO);
    }

    private void SetupFriendAndInviteUI(GameObject cardGO, string playerName, bool isFriend)
    {
        if (cardGO == null) return;

        // Auto-find existing Invite Button or create dynamically
        Button inviteBtn = cardGO.transform.Find("InviteButton")?.GetComponent<Button>() ??
                           cardGO.transform.Find("Invite")?.GetComponent<Button>();

        if (inviteBtn == null)
        {
            Button[] btns = cardGO.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b != null && (b.gameObject.name.ToLower().Contains("invite") || b.gameObject.name.ToLower().Contains("add")))
                {
                    inviteBtn = b;
                    break;
                }
            }
        }

        // Dynamically create Invite Button if missing on card
        if (inviteBtn == null)
        {
            GameObject inviteGO = new GameObject("InviteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            inviteGO.transform.SetParent(cardGO.transform, false);

            RectTransform iRt = inviteGO.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(1f, 0.5f);
            iRt.anchorMax = new Vector2(1f, 0.5f);
            iRt.pivot = new Vector2(1f, 0.5f);
            iRt.anchoredPosition = new Vector2(-10f, 0f);
            iRt.sizeDelta = new Vector2(85f, 32f);

            Image iBg = inviteGO.GetComponent<Image>();
            iBg.color = new Color(0.12f, 0.55f, 0.95f, 0.95f); // Vibrant blue invite button

            GameObject iTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            iTextGO.transform.SetParent(inviteGO.transform, false);
            RectTransform itRt = iTextGO.GetComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one; itRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI itTmp = iTextGO.GetComponent<TextMeshProUGUI>();
            itTmp.text = "+ Invite";
            itTmp.fontSize = 13;
            itTmp.fontStyle = FontStyles.Bold;
            itTmp.alignment = TextAlignmentOptions.Center;
            itTmp.color = Color.white;

            inviteBtn = inviteGO.GetComponent<Button>();
        }

        // Auto-find or Dynamically Create Friend Icon Badge from Code
        Transform friendIconTr = cardGO.transform.Find("FriendIcon");
        if (friendIconTr == null)
        {
            GameObject friendGO = new GameObject("FriendIcon", typeof(RectTransform), typeof(Image));
            friendGO.transform.SetParent(cardGO.transform, false);

            RectTransform fRt = friendGO.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(1f, 0.5f);
            fRt.anchorMax = new Vector2(1f, 0.5f);
            fRt.pivot = new Vector2(1f, 0.5f);
            fRt.anchoredPosition = new Vector2(-10f, 0f);
            fRt.sizeDelta = new Vector2(85f, 32f);

            Image fBg = friendGO.GetComponent<Image>();
            fBg.color = new Color(0.15f, 0.68f, 0.38f, 0.95f); // Sleek green friend badge

            GameObject fTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            fTextGO.transform.SetParent(friendGO.transform, false);
            RectTransform ftRt = fTextGO.GetComponent<RectTransform>();
            ftRt.anchorMin = Vector2.zero; ftRt.anchorMax = Vector2.one; ftRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI ftTmp = fTextGO.GetComponent<TextMeshProUGUI>();
            ftTmp.text = "✔ Friend";
            ftTmp.fontSize = 13;
            ftTmp.fontStyle = FontStyles.Bold;
            ftTmp.alignment = TextAlignmentOptions.Center;
            ftTmp.color = Color.white;

            friendIconTr = friendGO.transform;
        }

        // Toggle UI based on Friend Status
        if (isFriend)
        {
            if (inviteBtn != null) inviteBtn.gameObject.SetActive(false);
            if (friendIconTr != null) friendIconTr.gameObject.SetActive(true);
        }
        else
        {
            if (friendIconTr != null) friendIconTr.gameObject.SetActive(false);
            if (inviteBtn != null)
            {
                inviteBtn.gameObject.SetActive(true);
                inviteBtn.onClick.RemoveAllListeners();
                inviteBtn.onClick.AddListener(() =>
                {
                    if (CounterBoom.Networking.FirebaseManager.Instance != null)
                    {
                        CounterBoom.Networking.FirebaseManager.Instance.SendFriendRequest(playerName, (success, msg) =>
                        {
                            ShowNotification(msg);
                        });
                    }
                    else
                    {
                        ShowNotification($"📩 Invite sent to {playerName}!");
                    }
                    // Switch UI to Friend Badge dynamically!
                    inviteBtn.gameObject.SetActive(false);
                    if (friendIconTr != null) friendIconTr.gameObject.SetActive(true);
                });
            }
        }
    }

    private void CopyRoomCodeToClipboard()
    {
        string code = RelayNetworkManager.Instance != null ? RelayNetworkManager.Instance.CurrentJoinCode : "";
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            ShowNotification($"📋 Room Code '{code}' Copied!");
        }
        else
        {
            ShowNotification("⚠️ No active Room Code to copy!");
        }
    }

    private void OnStartMatchFromLobbyClicked()
    {
        CloseCustomLobbyPanel();
        if (RelayNetworkManager.Instance != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            RelayNetworkManager.Instance.StartMatchFromLobby();
        }
        else
        {
            ShowNotification("🎮 Match Started!");
        }
    }

    private void OnLeaveLobbyFromPanelClicked()
    {
        OnLeaveGameClicked();
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

    private GameObject ghostModeBanner;

    /// <summary>
    /// Disables action buttons (weapons, grenades, pickup, consumables, bag) during ghost spectating mode.
    /// Only move control remains active.
    /// </summary>
    public void SetGhostUI(bool isGhost)
    {
        if (weaponSlot1 != null) weaponSlot1.gameObject.SetActive(!isGhost);
        if (weaponSlot2 != null) weaponSlot2.gameObject.SetActive(!isGhost);
        if (boomButton != null) boomButton.gameObject.SetActive(!isGhost);
        if (pickupButton != null) pickupButton.gameObject.SetActive(!isGhost);
        if (pickupScrollView != null) pickupScrollView.gameObject.SetActive(!isGhost);
        if (medikitButton != null) medikitButton.gameObject.SetActive(!isGhost);
        if (shakeButton != null) shakeButton.gameObject.SetActive(!isGhost);
        if (bagButton != null) bagButton.gameObject.SetActive(!isGhost);

        // Hide Health & Energy bars and text displays for ghosts
        if (healthSlider != null) healthSlider.gameObject.SetActive(!isGhost);
        if (healthText != null) healthText.gameObject.SetActive(!isGhost);
        if (energySlider != null) energySlider.gameObject.SetActive(!isGhost);
        if (energyText != null) energyText.gameObject.SetActive(!isGhost);

        // Top ghost mode indicator banner
        if (isGhost)
        {
            if (ghostModeBanner == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    ghostModeBanner = new GameObject("GhostModeBanner", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    ghostModeBanner.transform.SetParent(canvas.transform, false);

                    RectTransform rt = ghostModeBanner.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -15f);
                    rt.sizeDelta = new Vector2(240f, 36f);

                    UnityEngine.UI.Image img = ghostModeBanner.GetComponent<UnityEngine.UI.Image>();
                    img.color = new Color(0.08f, 0.14f, 0.24f, 0.85f);

                    GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    txtGO.transform.SetParent(ghostModeBanner.transform, false);
                    RectTransform txtRt = txtGO.GetComponent<RectTransform>();
                    txtRt.anchorMin = Vector2.zero;
                    txtRt.anchorMax = Vector2.one;
                    txtRt.sizeDelta = Vector2.zero;

                    TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
                    tmp.text = "👻 GHOST MODE";
                    tmp.fontSize = 17;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(0.5f, 0.88f, 1.0f, 1.0f);
                }
            }
            if (ghostModeBanner != null) ghostModeBanner.SetActive(true);
        }
        else
        {
            if (ghostModeBanner != null) ghostModeBanner.SetActive(false);
        }
    }

    public void SetSettingsText(string text)
    {
        EnsureSettingsUI();
        if (settingsInfoText != null)
        {
            settingsInfoText.text = text;
        }
    }

    public void ToggleSettingsMenu()
    {
        if (settingsPanel == null) EnsureSettingsUI();
        if (settingsPanel != null)
        {
            bool newState = !settingsPanel.activeSelf;
            settingsPanel.SetActive(newState);
            if (newState)
            {
                settingsPanel.transform.SetAsLastSibling();
                if (settingsInfoText != null && (string.IsNullOrEmpty(settingsInfoText.text) || settingsInfoText.text == "New Text"))
                {
                    settingsInfoText.text = "<size=24><b>GAME PAUSED</b></size>\n<size=13><color=#88aacc>Select an option below</color></size>";
                }
            }
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

        // Immediately update player presence to online upon leaving match
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.SetForcedPresenceStatus("online");
            CounterBoom.Networking.FirebaseManager.Instance.ForceSendPresenceUpdate();
        }

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

        // Load MainMenuScene (with fallback to scene index 0)
        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }
        catch
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
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

        // Auto-resolve Settings Info / Header Text
        if (settingsInfoText == null && settingsPanel != null)
        {
            foreach (var tmp in settingsPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (leaveGameButton != null && tmp.transform.IsChildOf(leaveGameButton.transform)) continue;
                if (closeSettingsButton != null && tmp.transform.IsChildOf(closeSettingsButton.transform)) continue;
                settingsInfoText = tmp;
                break;
            }
        }

        if (settingsInfoText != null)
        {
            settingsInfoText.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(settingsInfoText.text) || settingsInfoText.text == "New Text")
            {
                settingsInfoText.text = "<size=24><b>GAME PAUSED</b></size>\n<size=13><color=#88aacc>Select an option below</color></size>";
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
            // Position above panels (top center) and bring to front
            RectTransform rt = notificationText.rectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -25f);
            }
            notificationText.transform.SetAsLastSibling();

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
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -25f);
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
        menuBtn.onClick.AddListener(ReturnToMainMenuViaLoading);

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
        ReturnToMainMenuViaLoading();
    }

    // ─── Animated Winner & End Game Modal ───────────────────────────────────────

    private GameObject winnerPanel;
    private GameObject winnerCardGO;
    private TextMeshProUGUI winnerTitleText;
    private TextMeshProUGUI winnerSubtitleText;
    private TextMeshProUGUI winnerRoleBadgeText;
    private TextMeshProUGUI winnerCountdownText;
    private Outline winnerCardOutline;
    private Coroutine autoReturnCoroutine;
    private Coroutine animateModalCoroutine;

    public void ShowAnimatedWinnerModal(PlayerRole winner)
    {
        EnsureWinnerUI();

        if (winner == PlayerRole.Hostage)
        {
            if (winnerTitleText != null)
            {
                winnerTitleText.text = "🏆 HOSTAGES WIN! 🏆";
                winnerTitleText.color = new Color(0.2f, 1f, 0.65f, 1f); // Vibrant Emerald Green
            }
            if (winnerSubtitleText != null)
            {
                winnerSubtitleText.text = "All living hostages unlocked the gate and escaped safely!";
            }
            if (winnerRoleBadgeText != null)
            {
                winnerRoleBadgeText.text = "ESCAPED VICTORY";
                winnerRoleBadgeText.color = new Color(0.15f, 0.9f, 0.5f, 1f);
            }
            if (winnerCardOutline != null)
            {
                winnerCardOutline.effectColor = new Color(0.2f, 0.9f, 0.5f, 0.85f);
            }
        }
        else
        {
            if (winnerTitleText != null)
            {
                winnerTitleText.text = "🏆 THIEVES WIN! 🏆";
                winnerTitleText.color = new Color(1f, 0.82f, 0.2f, 1f); // Vibrant Gold
            }
            if (winnerSubtitleText != null)
            {
                winnerSubtitleText.text = "Safe cracked, treasure stolen, and thief escaped through the gate!";
            }
            if (winnerRoleBadgeText != null)
            {
                winnerRoleBadgeText.text = "HEIST SUCCESSFUL";
                winnerRoleBadgeText.color = new Color(1f, 0.75f, 0.1f, 1f);
            }
            if (winnerCardOutline != null)
            {
                winnerCardOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.85f);
            }
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(true);
            winnerPanel.transform.SetAsLastSibling();
        }

        // Animate card scale bounce
        if (animateModalCoroutine != null) StopCoroutine(animateModalCoroutine);
        animateModalCoroutine = StartCoroutine(AnimateWinnerModalRoutine(winnerCardGO));

        // Start 10-second countdown to return to main menu
        if (autoReturnCoroutine != null) StopCoroutine(autoReturnCoroutine);
        autoReturnCoroutine = StartCoroutine(AutoReturnCountdownRoutine(10));
    }

    private System.Collections.IEnumerator AnimateWinnerModalRoutine(GameObject targetCard)
    {
        if (targetCard == null) yield break;

        targetCard.transform.localScale = Vector3.zero;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scale;
            if (t < 0.65f)
            {
                float tUp = t / 0.65f;
                scale = Mathf.Lerp(0f, 1.14f, Mathf.Sin(tUp * Mathf.PI * 0.5f));
            }
            else
            {
                float tDown = (t - 0.65f) / 0.35f;
                scale = Mathf.Lerp(1.14f, 1.0f, Mathf.Sin(tDown * Mathf.PI * 0.5f));
            }

            targetCard.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        targetCard.transform.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator AutoReturnCountdownRoutine(int seconds)
    {
        int remaining = seconds;
        while (remaining > 0)
        {
            if (winnerCountdownText != null)
            {
                winnerCountdownText.text = $"Returning to Main Menu in {remaining}s...";
            }
            yield return new WaitForSecondsRealtime(1.0f);
            remaining--;
        }

        if (winnerCountdownText != null)
        {
            winnerCountdownText.text = "Returning to Main Menu...";
        }

        ReturnToMainMenuViaLoading();
    }

    private void EnsureWinnerUI()
    {
        if (winnerPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Dim backdrop
        GameObject panelGO = new GameObject("WinnerPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panelGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image bgImg = panelGO.GetComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.01f, 0.02f, 0.05f, 0.94f);

        // Center card
        winnerCardGO = new GameObject("WinnerCard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        winnerCardGO.transform.SetParent(panelGO.transform, false);

        RectTransform cardRt = winnerCardGO.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(480f, 320f);
        cardRt.anchoredPosition = Vector2.zero;

        UnityEngine.UI.Image cardImg = winnerCardGO.GetComponent<UnityEngine.UI.Image>();
        cardImg.color = new Color(0.08f, 0.1f, 0.15f, 0.98f);

        winnerCardOutline = winnerCardGO.AddComponent<Outline>();
        winnerCardOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.85f);
        winnerCardOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // Close button (X) in top right
        GameObject closeBtnGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        closeBtnGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform closeRt = closeBtnGO.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(36f, 36f);

        UnityEngine.UI.Image closeImg = closeBtnGO.GetComponent<UnityEngine.UI.Image>();
        closeImg.color = new Color(0.2f, 0.24f, 0.32f, 0.9f);

        Outline closeOutline = closeBtnGO.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0.5f, 0.55f, 0.65f, 0.7f);
        closeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject closeTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        closeTxtGO.transform.SetParent(closeBtnGO.transform, false);
        RectTransform ctxtRt = closeTxtGO.GetComponent<RectTransform>();
        ctxtRt.anchorMin = Vector2.zero; ctxtRt.anchorMax = Vector2.one; ctxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI ctxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        ctxt.text = "✕";
        ctxt.fontSize = 20f;
        ctxt.fontStyle = FontStyles.Bold;
        ctxt.alignment = TextAlignmentOptions.Center;
        ctxt.color = Color.white;

        Button closeBtn = closeBtnGO.GetComponent<Button>();
        closeBtn.onClick.AddListener(ReturnToMainMenuViaLoading);

        // Winner Badge
        GameObject badgeGO = new GameObject("WinnerBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform badgeRt = badgeGO.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 1f); badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(0.5f, 1f);
        badgeRt.anchoredPosition = new Vector2(0f, -22f);
        badgeRt.sizeDelta = new Vector2(0f, 22f);

        winnerRoleBadgeText = badgeGO.GetComponent<TextMeshProUGUI>();
        winnerRoleBadgeText.text = "VICTORY";
        winnerRoleBadgeText.fontSize = 14f;
        winnerRoleBadgeText.fontStyle = FontStyles.Bold;
        winnerRoleBadgeText.alignment = TextAlignmentOptions.Center;
        winnerRoleBadgeText.color = new Color(1f, 0.85f, 0.2f, 0.9f);

        // Title
        GameObject titleGO = new GameObject("WinnerTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -48f);
        titleRt.sizeDelta = new Vector2(0f, 44f);

        winnerTitleText = titleGO.GetComponent<TextMeshProUGUI>();
        winnerTitleText.text = "🏆 VICTORY! 🏆";
        winnerTitleText.fontSize = 30f;
        winnerTitleText.fontStyle = FontStyles.Bold;
        winnerTitleText.alignment = TextAlignmentOptions.Center;
        winnerTitleText.color = new Color(1f, 0.85f, 0.2f, 1f);

        // Subtitle
        GameObject subGO = new GameObject("WinnerSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform subRt = subGO.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 1f); subRt.anchorMax = new Vector2(1f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0f, -96f);
        subRt.sizeDelta = new Vector2(0f, 32f);

        winnerSubtitleText = subGO.GetComponent<TextMeshProUGUI>();
        winnerSubtitleText.text = "Game Over";
        winnerSubtitleText.fontSize = 15f;
        winnerSubtitleText.alignment = TextAlignmentOptions.Center;
        winnerSubtitleText.color = new Color(0.85f, 0.9f, 1f, 1f);

        // Countdown Text
        GameObject cdGO = new GameObject("CountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
        cdGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform cdRt = cdGO.GetComponent<RectTransform>();
        cdRt.anchorMin = new Vector2(0f, 0f); cdRt.anchorMax = new Vector2(1f, 0f);
        cdRt.pivot = new Vector2(0.5f, 0f);
        cdRt.anchoredPosition = new Vector2(0f, 95f);
        cdRt.sizeDelta = new Vector2(0f, 26f);

        winnerCountdownText = cdGO.GetComponent<TextMeshProUGUI>();
        winnerCountdownText.text = "Returning to Main Menu in 10s...";
        winnerCountdownText.fontSize = 14f;
        winnerCountdownText.alignment = TextAlignmentOptions.Center;
        winnerCountdownText.color = new Color(0.65f, 0.75f, 0.9f, 0.9f);

        // Return to Main Menu Button
        GameObject menuBtnGO = new GameObject("ReturnMainMenuButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Button));
        menuBtnGO.transform.SetParent(winnerCardGO.transform, false);
        RectTransform menuRt = menuBtnGO.GetComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, 0f); menuRt.anchorMax = new Vector2(0.5f, 0f);
        menuRt.pivot = new Vector2(0.5f, 0f);
        menuRt.anchoredPosition = new Vector2(0f, 32f);
        menuRt.sizeDelta = new Vector2(280f, 46f);

        UnityEngine.UI.Image menuImg = menuBtnGO.GetComponent<UnityEngine.UI.Image>();
        menuImg.color = new Color(0.2f, 0.45f, 0.85f, 1f);

        Outline mOutline = menuBtnGO.AddComponent<Outline>();
        mOutline.effectColor = new Color(0.4f, 0.65f, 1f, 0.8f);
        mOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject menuTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        menuTextGO.transform.SetParent(menuBtnGO.transform, false);
        RectTransform mTextRt = menuTextGO.GetComponent<RectTransform>();
        mTextRt.anchorMin = Vector2.zero; mTextRt.anchorMax = Vector2.one; mTextRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI menuTmp = menuTextGO.GetComponent<TextMeshProUGUI>();
        menuTmp.text = "RETURN TO MAIN MENU";
        menuTmp.fontSize = 16f;
        menuTmp.fontStyle = FontStyles.Bold;
        menuTmp.alignment = TextAlignmentOptions.Center;
        menuTmp.color = Color.white;

        Button menuBtn = menuBtnGO.GetComponent<Button>();
        menuBtn.onClick.AddListener(ReturnToMainMenuViaLoading);

        winnerPanel = panelGO;
        winnerPanel.SetActive(false);
    }

    public void ReturnToMainMenuViaLoading()
    {
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }

        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.SetForcedPresenceStatus("online");
            CounterBoom.Networking.FirebaseManager.Instance.ForceSendPresenceUpdate();
        }

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        LoadingGameController.TargetMode = LoadingGameController.MatchMode.ReturnToMainMenu;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
    }
}

