using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject nameEntryPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject cabinetPanel;
    [SerializeField] private GameObject shopPanel;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private SettingsManager settingsManager;

    [Header("Navigation Buttons")]
    [SerializeField] private Button navPlayButton;
    [SerializeField] private Button navCabinetButton;
    [SerializeField] private Button navShopButton;
    [SerializeField] private Button navSettingsButton;
    [SerializeField] private Button navExitButton;

    [Header("Name Entry Panel")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button nameSubmitButton;

    [Header("Main Menu Player Profile & Header UI")]
    [SerializeField] private Image profileSkinIcon;
    [SerializeField] private TextMeshProUGUI profileNameText;
    [SerializeField] private TextMeshProUGUI mainCoinsText;

    [Header("Cabinet / Customization")]
    [SerializeField] private CharacterAssembler previewAssembler;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform characterPreviewSpawnPoint;
    [SerializeField] private Button cabinetPrevButton;
    [SerializeField] private Button cabinetNextButton;
    [SerializeField] private Button cabinetEquipButton;
    [SerializeField] private TextMeshProUGUI cabinetSkinNameText;
    [SerializeField] private TextMeshProUGUI cabinetSkinStatusText;
    [SerializeField] private Button cabinetBackButton;

    [Header("Shop")]
    [SerializeField] private Button shopPrevButton;
    [SerializeField] private Button shopNextButton;
    [SerializeField] private Button shopBuyButton;
    [SerializeField] private TextMeshProUGUI shopSkinNameText;
    [SerializeField] private TextMeshProUGUI shopSkinPriceText;
    [SerializeField] private TextMeshProUGUI shopCoinsText;
    [SerializeField] private Button shopBackButton;

    [Header("Lobby Play Panel (Lobby + Relay)")]
    [SerializeField] private Button hostButton; // Repurposed as "Quick Play" to preserve editor serialization
    [SerializeField] private Button joinButton; // Repurposed as "Manual Join" to preserve editor serialization
    [SerializeField] private Button generateCodeButton; // "Generate Code" button
    [SerializeField] private TMP_InputField joinCodeInputField; // Repurposed to preserve editor serialization
    [SerializeField] private TextMeshProUGUI generatedCodeText; // Displays code if host
    [SerializeField] private TextMeshProUGUI playStatusText;
    [SerializeField] private Button playBackButton;

    // State Variables
    private CharacterSkinData[] skins;
    private int cabinetSelectedIndex = 0;
    private int shopSelectedIndex = 0;
    private int coins = 1000;
    private GameObject previewPlayerInstance;

    public static MainMenuController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Update()
    {
    }

    private void Start()
    {
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) ScreenAndUIScaler.ConfigureCanvas(canvas);

        // 1. Initialize Player Coins
        coins = PlayerPrefs.GetInt("Coins", 1000);
        PlayerPrefs.SetInt("Coins", coins);

        // 2. Setup Player Prefab Preview & Fetch Available Skins
        SetupPreviewPlayer();

        // 2b. Initialize Player Profile Header UI & Settings Panel UI
        UpdatePlayerProfileUI();
        EnsureSettingsPanelUI();
        EnsureEmberParticles();

        // 3. Register Navigation Listeners
        if (navPlayButton != null) navPlayButton.onClick.AddListener(() => ShowPanel(playPanel));
        if (navCabinetButton != null) navCabinetButton.onClick.AddListener(() => ShowPanel(cabinetPanel));
        if (navShopButton != null) navShopButton.onClick.AddListener(() => ShowPanel(shopPanel));

        if (navSettingsButton == null && mainPanel != null)
        {
            foreach (var btn in mainPanel.GetComponentsInChildren<Button>(true))
            {
                string n = btn.gameObject.name.ToLower();
                if (n.Contains("setting"))
                {
                    navSettingsButton = btn;
                    break;
                }
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null && tmp.text.ToLower().Contains("setting"))
                {
                    navSettingsButton = btn;
                    break;
                }
            }
        }

        if (navSettingsButton != null) navSettingsButton.onClick.AddListener(OpenSettingsPanel);
        if (navExitButton != null) navExitButton.onClick.AddListener(ExitGame);

        // Back Buttons
        if (playBackButton != null) playBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        if (cabinetBackButton != null) cabinetBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        if (shopBackButton != null) shopBackButton.onClick.AddListener(() => ShowPanel(mainPanel));

        // 4. Register Panel Specific Listeners
        if (nameSubmitButton != null) nameSubmitButton.onClick.AddListener(SubmitName);
        
        if (cabinetPrevButton != null) cabinetPrevButton.onClick.AddListener(CycleCabinetPrev);
        if (cabinetNextButton != null) cabinetNextButton.onClick.AddListener(CycleCabinetNext);
        if (cabinetEquipButton != null) cabinetEquipButton.onClick.AddListener(EquipSelectedSkin);

        if (shopPrevButton != null) shopPrevButton.onClick.AddListener(CycleShopPrev);
        if (shopNextButton != null) shopNextButton.onClick.AddListener(CycleShopNext);
        if (shopBuyButton != null) shopBuyButton.onClick.AddListener(BuySelectedSkin);

        // Quick Play Matchmaking (using hostButton) & Manual Join (using joinButton) & Generate Code
        if (hostButton != null) hostButton.onClick.AddListener(OnQuickPlayClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnManualJoinClicked);

        // Auto-find Generate Code button if unassigned
        if (generateCodeButton == null && playPanel != null)
        {
            foreach (var btn in playPanel.GetComponentsInChildren<Button>(true))
            {
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null && tmp.text.ToLower().Contains("generate"))
                {
                    generateCodeButton = btn;
                    break;
                }
                var txt = btn.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null && txt.text.ToLower().Contains("generate"))
                {
                    generateCodeButton = btn;
                    break;
                }
            }
        }
        if (generateCodeButton != null) generateCodeButton.onClick.AddListener(OnGenerateCodeClicked);

        // 5. Initial Display Selection
        cabinetSelectedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        shopSelectedIndex = 0;

        // 6. Direct to Name Entry or Main Panel based on if player name has been explicitly set
        int nameHasBeenSet = PlayerPrefs.GetInt("PlayerNameHasBeenSet", 0);
        if (nameHasBeenSet == 1 && !string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerName", "")))
        {
            ShowPanel(mainPanel);
        }
        else
        {
            ShowPanel(nameEntryPanel);
        }
    }

    public void OpenSettingsPanel()
    {
        EnsureSettingsPanelUI();
        ShowPanel(settingsPanel);
    }

    public void ShowMainPanel()
    {
        ShowPanel(mainPanel);
    }

    public void EnsureSettingsPanelUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (settingsPanel == null)
        {
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            {
                string n = t.gameObject.name.ToLower();
                if (n == "settingspanel" || n == "settings" || n.Contains("setting"))
                {
                    settingsPanel = t.gameObject;
                    break;
                }
            }
        }

        if (settingsPanel == null)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(canvas.transform, false);
            RectTransform rt = settingsPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        SettingsManager sm = settingsPanel.GetComponent<SettingsManager>();
        if (sm == null)
        {
            settingsPanel.AddComponent<SettingsManager>();
        }
    }

    private void EnsureEmberParticles()
    {
        if (FindObjectOfType<FireEmberParticleSystem>() == null)
        {
            FireEmberParticleSystem.CreateEmberEffect(new Vector3(-6f, 0f, 0f));
        }
    }

    private void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == settingsPanel)
        {
            EnsureSettingsPanelUI();
        }

        // Overlay panels: close all overlays first (but keep mainPanel always active)
        if (playPanel != null) playPanel.SetActive(false);
        if (cabinetPanel != null) cabinetPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // nameEntryPanel is a special case — it replaces mainPanel until name is set
        bool isNameEntry = panelToShow == nameEntryPanel;
        if (mainPanel != null) mainPanel.SetActive(!isNameEntry);
        if (nameEntryPanel != null) nameEntryPanel.SetActive(isNameEntry);

        // Open the requested overlay panel (if it's not mainPanel or nameEntryPanel)
        if (panelToShow != null && panelToShow != mainPanel && panelToShow != nameEntryPanel)
        {
            panelToShow.SetActive(true);
        }

        // Per-panel refresh logic
        if (panelToShow == mainPanel || panelToShow == null)
        {
            ResetPreviewToEquippedSkin();
            UpdatePlayerProfileUI();
        }
        else if (panelToShow == cabinetPanel)
        {
            cabinetSelectedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            UpdateCabinetUI();
        }
        else if (panelToShow == shopPanel)
        {
            shopSelectedIndex = 0;
            UpdateShopUI();
        }
        else if (panelToShow == playPanel)
        {
            ResetPreviewToEquippedSkin();
            UpdatePlayStatus("Ready to search or host lobby");
            if (generatedCodeText != null) generatedCodeText.text = "JOIN CODE: -";
            SetPlayInputInteractable(true);
        }
    }

    public void ResetPreviewToEquippedSkin()
    {
        int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        if (skins == null && previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
        }

        if (previewAssembler != null && skins != null && equippedIndex >= 0 && equippedIndex < skins.Length)
        {
            previewAssembler.SetCharacterSkin(skins[equippedIndex]);
        }

        cabinetSelectedIndex = equippedIndex;
    }

    #region Name Entry Logic
    private void SubmitName()
    {
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text.Trim()))
        {
            string cleanName = nameInputField.text.Trim();
            PlayerPrefs.SetString("PlayerName", cleanName);
            PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenu] Player nickname saved: {cleanName}");
            UpdatePlayerProfileUI();
            ShowPanel(mainPanel);
        }
        else
        {
            Debug.LogWarning("[MainMenu] Cannot submit name: Input field is empty.");
        }
    }
    #endregion

    #region Preview Player Setup & Sanitization
    private void SetupPreviewPlayer()
    {
        if (previewAssembler != null)
        {
            previewPlayerInstance = previewAssembler.gameObject;
            SanitizePreviewPlayer(previewPlayerInstance);
            skins = previewAssembler.GetAvailableSkins();
            PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
            return;
        }

        // Try finding an existing CharacterAssembler in scene
        previewAssembler = FindObjectOfType<CharacterAssembler>();
        if (previewAssembler != null)
        {
            previewPlayerInstance = previewAssembler.gameObject;
            previewPlayerInstance.transform.position = Vector3.zero;
            previewPlayerInstance.transform.rotation = Quaternion.identity;

            SanitizePreviewPlayer(previewPlayerInstance);
            skins = previewAssembler.GetAvailableSkins();
            PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
            return;
        }

        // Try loading playerPrefab if unassigned
        if (playerPrefab == null)
        {
            playerPrefab = Resources.Load<GameObject>("Player");
        }

#if UNITY_EDITOR
        if (playerPrefab == null)
        {
            playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
        }
#endif

        if (playerPrefab != null)
        {
            previewPlayerInstance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            previewPlayerInstance.name = "PlayerPreview_MainMenu";

            SanitizePreviewPlayer(previewPlayerInstance);

            previewAssembler = previewPlayerInstance.GetComponentInChildren<CharacterAssembler>();
            if (previewAssembler != null)
            {
                skins = previewAssembler.GetAvailableSkins();
                PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] Player prefab reference missing; cabinet preview may be unassigned.");
        }
    }

    private void SanitizePreviewPlayer(GameObject obj)
    {
        if (obj == null) return;

        // Force preview player position strictly to X=0, Y=0, Z=0
        obj.transform.position = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;

        // 1. Destroy NetworkObject component so Netcode for GameObjects ignores this preview object completely
        var netObj = obj.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null)
        {
            DestroyImmediate(netObj);
        }

        // 2. Disable physics & gravity
        var rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = false;
            rb.bodyType = RigidbodyType2D.Static;
            rb.position = Vector2.zero;
        }

        // 3. Disable all colliders
        foreach (var col in obj.GetComponentsInChildren<Collider2D>(true))
        {
            col.enabled = false;
        }

        // 4. Destroy all gameplay/network components on the preview instance so it CANNOT act as a player or hijack controls
        Component[] componentsToDestroy = new Component[]
        {
            obj.GetComponent<PlayerController>(),
            obj.GetComponent<PlayerAiming>(),
            obj.GetComponent<WeaponController>(),
            obj.GetComponent<BagManager>(),
            obj.GetComponent<PlayerHealth>(),
            obj.GetComponent<PlayerEnergy>(),
            obj.GetComponent<Unity.Netcode.Components.NetworkTransform>(),
            obj.GetComponent("ClientNetworkTransform") as Component,
            obj.GetComponent("OwnerNetworkAnimator") as Component,
            obj.GetComponent<AimingDots>()
        };

        foreach (var c in componentsToDestroy)
        {
            if (c != null) DestroyImmediate(c);
        }

        // Disable any cameras or audio listeners on preview
        foreach (var cam in obj.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (var listener in obj.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

        // 5. Ensure CharacterAssembler is active & equipped skin loaded
        var ca = obj.GetComponentInChildren<CharacterAssembler>();
        if (ca != null)
        {
            ca.enabled = true;
            ca.LoadEquippedSkin();
        }
    }

    private void CleanupPreviewPlayer()
    {
        if (previewPlayerInstance != null)
        {
            Debug.Log("[MainMenu] Destroying Main Menu player preview instance before starting game.");
            Destroy(previewPlayerInstance);
            previewPlayerInstance = null;
            previewAssembler = null;
        }

        // Search and destroy any unspawned Player objects in scene hierarchy
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        foreach (var pc in pcs)
        {
            var netObj = pc.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
            {
                Debug.Log($"[MainMenu] Destroying unspawned PlayerController object '{pc.name}'.");
                Destroy(pc.gameObject);
            }
        }

        GameObject[] scenePlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in scenePlayers)
        {
            var netObj = p.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
            {
                Debug.Log($"[MainMenu] Destroying unspawned scene player object '{p.name}'.");
                Destroy(p);
            }
        }
    }

    private void OnDestroy()
    {
        CleanupPreviewPlayer();
    }
    #endregion

    #region Cabinet (Customization) Logic
    private void CycleCabinetPrev()
    {
        if (skins == null || skins.Length == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex - 1 + skins.Length) % skins.Length;
        UpdateCabinetUI();
    }

    private void CycleCabinetNext()
    {
        if (skins == null || skins.Length == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex + 1) % skins.Length;
        UpdateCabinetUI();
    }

    private void EquipSelectedSkin()
    {
        if (skins == null || skins.Length == 0) return;

        // Check if selected skin is unlocked
        if (IsSkinUnlocked(cabinetSelectedIndex))
        {
            PlayerPrefs.SetInt("EquippedSkinIndex", cabinetSelectedIndex);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenu] Equipped skin index: {cabinetSelectedIndex}");
            UpdateCabinetUI();
            UpdatePlayerProfileUI();
        }
    }

    private void UpdateCabinetUI()
    {
        if (previewAssembler == null) SetupPreviewPlayer();
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[cabinetSelectedIndex]);

        // Render details
        if (cabinetSkinNameText != null)
        {
            cabinetSkinNameText.text = skins[cabinetSelectedIndex].skinName;
        }

        bool isUnlocked = IsSkinUnlocked(cabinetSelectedIndex);
        bool isEquipped = PlayerPrefs.GetInt("EquippedSkinIndex", 0) == cabinetSelectedIndex;

        if (cabinetSkinStatusText != null)
        {
            if (isEquipped)
            {
                cabinetSkinStatusText.text = "<color=green>EQUIPPED</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = false;
            }
            else if (isUnlocked)
            {
                cabinetSkinStatusText.text = "<color=yellow>UNLOCKED</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = true;
            }
            else
            {
                cabinetSkinStatusText.text = "<color=red>LOCKED (Go to Shop)</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = false;
            }
        }
    }
    #endregion

    #region Shop Logic
    private void CycleShopPrev()
    {
        if (skins == null || skins.Length == 0) return;
        shopSelectedIndex = (shopSelectedIndex - 1 + skins.Length) % skins.Length;
        UpdateShopUI();
    }

    private void CycleShopNext()
    {
        if (skins == null || skins.Length == 0) return;
        shopSelectedIndex = (shopSelectedIndex + 1) % skins.Length;
        UpdateShopUI();
    }

    private void BuySelectedSkin()
    {
        if (skins == null || skins.Length == 0) return;

        CharacterSkinData targetSkin = skins[shopSelectedIndex];
        if (IsSkinUnlocked(shopSelectedIndex)) return;

        if (coins >= targetSkin.price)
        {
            // Deduct coins and save unlock state
            coins -= targetSkin.price;
            PlayerPrefs.SetInt("Coins", coins);
            PlayerPrefs.SetInt($"Skin_Unlocked_{shopSelectedIndex}", 1);
            PlayerPrefs.Save();

            Debug.Log($"[MainMenu] Purchased skin '{targetSkin.skinName}' for {targetSkin.price} coins.");
            UpdateShopUI();
            UpdatePlayerProfileUI();
        }
        else
        {
            Debug.LogWarning("[MainMenu] Not enough coins to purchase skin.");
        }
    }

    #region Main Menu Profile & Header UI
    public void UpdatePlayerProfileUI()
    {
        EnsureProfileHeaderUI();

        // 1. Player Name
        string pName = PlayerPrefs.GetString("PlayerName", "Player");
        if (string.IsNullOrEmpty(pName)) pName = "Player";
        if (profileNameText != null)
        {
            profileNameText.text = pName;
        }

        // 2. Coins Balance
        int currentCoins = PlayerPrefs.GetInt("Coins", 1000);
        if (mainCoinsText != null)
        {
            mainCoinsText.text = $"{currentCoins}";
        }

        // 3. Profile Skin Avatar Icon
        int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        if (skins == null && previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
        }

        if (skins != null && equippedIndex >= 0 && equippedIndex < skins.Length)
        {
            CharacterSkinData currentSkin = skins[equippedIndex];
            if (currentSkin != null && currentSkin.head != null && profileSkinIcon != null)
            {
                profileSkinIcon.sprite = currentSkin.head;
                profileSkinIcon.enabled = true;
                profileSkinIcon.gameObject.SetActive(true);
            }
        }
    }

    private void EnsureProfileHeaderUI()
    {
        if (mainPanel == null) return;

        // Auto-find profileNameText if unassigned
        if (profileNameText == null)
        {
            foreach (var tmp in mainPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string n = tmp.gameObject.name.ToLower();
                if (n.Contains("profile") || n.Contains("playername") || n.Contains("username") || n.Contains("name"))
                {
                    profileNameText = tmp;
                    break;
                }
            }
        }

        // Auto-find mainCoinsText if unassigned
        if (mainCoinsText == null)
        {
            foreach (var tmp in mainPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string n = tmp.gameObject.name.ToLower();
                if (n != "shopcointext" && (n.Contains("coin") || n.Contains("money") || n.Contains("gold")))
                {
                    mainCoinsText = tmp;
                    break;
                }
            }
        }

        // Auto-find profileSkinIcon if unassigned
        if (profileSkinIcon == null)
        {
            foreach (var img in mainPanel.GetComponentsInChildren<Image>(true))
            {
                string n = img.gameObject.name.ToLower();
                if (n.Contains("profile") || n.Contains("avatar") || n.Contains("skinicon") || n.Contains("headicon") || n.Contains("icon"))
                {
                    profileSkinIcon = img;
                    break;
                }
            }
        }

        // If still null, dynamically build Top Header Bar inside mainPanel!
        if (profileNameText == null || mainCoinsText == null || profileSkinIcon == null)
        {
            CreateHeaderProfileBarUI();
        }
    }

    private void CreateHeaderProfileBarUI()
    {
        if (mainPanel == null) return;

        // Container GameObject for Top Profile Header Bar
        GameObject headerBarObj = new GameObject("ProfileHeaderBar", typeof(RectTransform));
        headerBarObj.transform.SetParent(mainPanel.transform, false);

        RectTransform headerRt = headerBarObj.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(0f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);
        headerRt.anchoredPosition = new Vector2(30f, -25f);
        headerRt.sizeDelta = new Vector2(540f, 65f);

        // 1. Profile Badge (Avatar Icon + Player Name)
        GameObject profileBadgeObj = new GameObject("ProfileBadge", typeof(RectTransform), typeof(Image));
        profileBadgeObj.transform.SetParent(headerBarObj.transform, false);

        RectTransform pbRt = profileBadgeObj.GetComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0f, 0.5f);
        pbRt.anchorMax = new Vector2(0f, 0.5f);
        pbRt.pivot = new Vector2(0f, 0.5f);
        pbRt.anchoredPosition = Vector2.zero;
        pbRt.sizeDelta = new Vector2(280f, 60f);

        Image pbBg = profileBadgeObj.GetComponent<Image>();
        pbBg.color = new Color(0.08f, 0.12f, 0.18f, 0.85f); // Translucent dark blue-gray panel

        // Avatar Frame / Border
        GameObject avatarFrameObj = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image));
        avatarFrameObj.transform.SetParent(profileBadgeObj.transform, false);

        RectTransform afRt = avatarFrameObj.GetComponent<RectTransform>();
        afRt.anchorMin = new Vector2(0f, 0.5f);
        afRt.anchorMax = new Vector2(0f, 0.5f);
        afRt.pivot = new Vector2(0f, 0.5f);
        afRt.anchoredPosition = new Vector2(6f, 0f);
        afRt.sizeDelta = new Vector2(50f, 50f);

        Image afBg = avatarFrameObj.GetComponent<Image>();
        afBg.color = new Color(0.2f, 0.3f, 0.45f, 0.9f); // Border accent frame

        // Profile Avatar Image Component (profileSkinIcon)
        GameObject iconObj = new GameObject("ProfileSkinIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(avatarFrameObj.transform, false);

        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(3f, 3f);
        iconRt.offsetMax = new Vector2(-3f, -3f);

        profileSkinIcon = iconObj.GetComponent<Image>();
        profileSkinIcon.preserveAspect = true;

        // Player Name Text (profileNameText)
        GameObject nameTxtObj = new GameObject("ProfileNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameTxtObj.transform.SetParent(profileBadgeObj.transform, false);

        RectTransform nameRt = nameTxtObj.GetComponent<RectTransform>();
        nameRt.anchorMin = Vector2.zero;
        nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = new Vector2(65f, 0f);
        nameRt.offsetMax = new Vector2(-10f, 0f);

        profileNameText = nameTxtObj.GetComponent<TextMeshProUGUI>();
        profileNameText.fontSize = 20;
        profileNameText.fontStyle = FontStyles.Bold;
        profileNameText.color = Color.white;
        profileNameText.alignment = TextAlignmentOptions.MidlineLeft;

        // 2. Coins Badge (Coins Display)
        GameObject coinsBadgeObj = new GameObject("CoinsBadge", typeof(RectTransform), typeof(Image));
        coinsBadgeObj.transform.SetParent(headerBarObj.transform, false);

        RectTransform cbRt = coinsBadgeObj.GetComponent<RectTransform>();
        cbRt.anchorMin = new Vector2(0f, 0.5f);
        cbRt.anchorMax = new Vector2(0f, 0.5f);
        cbRt.pivot = new Vector2(0f, 0.5f);
        cbRt.anchoredPosition = new Vector2(295f, 0f);
        cbRt.sizeDelta = new Vector2(220f, 60f);

        Image cbBg = coinsBadgeObj.GetComponent<Image>();
        cbBg.color = new Color(0.08f, 0.12f, 0.18f, 0.85f); // Translucent dark panel

        // Coins Text (mainCoinsText)
        GameObject coinsTxtObj = new GameObject("MainCoinsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        coinsTxtObj.transform.SetParent(coinsBadgeObj.transform, false);

        RectTransform coinsRt = coinsTxtObj.GetComponent<RectTransform>();
        coinsRt.anchorMin = Vector2.zero;
        coinsRt.anchorMax = Vector2.one;
        coinsRt.offsetMin = new Vector2(15f, 0f);
        coinsRt.offsetMax = new Vector2(-15f, 0f);

        mainCoinsText = coinsTxtObj.GetComponent<TextMeshProUGUI>();
        mainCoinsText.fontSize = 20;
        mainCoinsText.fontStyle = FontStyles.Bold;
        mainCoinsText.color = new Color(1f, 0.85f, 0.2f); // Gold text
        mainCoinsText.alignment = TextAlignmentOptions.Center;

        // 3. Settings Button (if navSettingsButton was not assigned in scene)
        if (navSettingsButton == null)
        {
            GameObject settingsBtnObj = new GameObject("DynamicSettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            settingsBtnObj.transform.SetParent(headerBarObj.transform, false);

            RectTransform sbRt = settingsBtnObj.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(0f, 0.5f);
            sbRt.anchorMax = new Vector2(0f, 0.5f);
            sbRt.pivot = new Vector2(0f, 0.5f);
            sbRt.anchoredPosition = new Vector2(530f, 0f);
            sbRt.sizeDelta = new Vector2(140f, 60f);

            Image sbBg = settingsBtnObj.GetComponent<Image>();
            sbBg.color = new Color(0.12f, 0.45f, 0.75f, 0.9f); // Translucent blue button

            GameObject settingsTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            settingsTxtObj.transform.SetParent(settingsBtnObj.transform, false);

            RectTransform stRt = settingsTxtObj.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.offsetMin = Vector2.zero;
            stRt.offsetMax = Vector2.zero;

            TextMeshProUGUI settingsTxt = settingsTxtObj.GetComponent<TextMeshProUGUI>();
            settingsTxt.text = "SETTINGS";
            settingsTxt.fontSize = 18;
            settingsTxt.fontStyle = FontStyles.Bold;
            settingsTxt.color = Color.white;
            settingsTxt.alignment = TextAlignmentOptions.Center;

            navSettingsButton = settingsBtnObj.GetComponent<Button>();
            navSettingsButton.onClick.AddListener(OpenSettingsPanel);
        }
    }
    #endregion

    private void UpdateShopUI()
    {
        if (previewAssembler == null) SetupPreviewPlayer();
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[shopSelectedIndex]);

        // Render coins balance
        if (shopCoinsText != null)
        {
            shopCoinsText.text = $"{coins}";
        }

        // Render skin info
        if (shopSkinNameText != null)
        {
            shopSkinNameText.text = skins[shopSelectedIndex].skinName;
        }

        bool isUnlocked = IsSkinUnlocked(shopSelectedIndex);

        if (shopSkinPriceText != null)
        {
            if (isUnlocked)
            {
                shopSkinPriceText.text = "UNLOCKED";
                if (shopBuyButton != null) shopBuyButton.gameObject.SetActive(false);
            }
            else
            {
                shopSkinPriceText.text = $"Price: {skins[shopSelectedIndex].price} Coins";
                if (shopBuyButton != null)
                {
                    shopBuyButton.gameObject.SetActive(true);
                    // Disable if player can't afford
                    shopBuyButton.interactable = coins >= skins[shopSelectedIndex].price;
                }
            }
        }
    }

    private bool IsSkinUnlocked(int index)
    {
        if (index == 0) return true; // Default skin is always unlocked
        return PlayerPrefs.GetInt($"Skin_Unlocked_{index}", 0) == 1;
    }
    #endregion

    #region Automatic Quick Play & Matchmaking Logic
    private void OnQuickPlayClicked()
    {
        CleanupPreviewPlayer();
        LoadingGameController.TargetMode = LoadingGameController.MatchMode.QuickPlay;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
    }

    private void OnManualJoinClicked()
    {
        if (joinCodeInputField == null || string.IsNullOrEmpty(joinCodeInputField.text))
        {
            UpdatePlayStatus("<color=yellow>Enter a room join code</color>");
            return;
        }

        string rawCode = joinCodeInputField.text.Trim().ToUpper();

        CleanupPreviewPlayer();
        LoadingGameController.TargetMode = LoadingGameController.MatchMode.JoinCode;
        LoadingGameController.JoinCodeToUse = rawCode;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
    }

    private void OnGenerateCodeClicked()
    {
        CleanupPreviewPlayer();
        LoadingGameController.TargetMode = LoadingGameController.MatchMode.PrivateHost;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
    }

    private void UpdatePlayStatus(string message)
    {
        if (playStatusText != null)
        {
            playStatusText.text = message;
        }
        Debug.Log($"[MainMenuUI] {message}");
    }

    private void SetPlayInputInteractable(bool state)
    {
        if (hostButton != null) hostButton.interactable = state;
        if (joinButton != null) joinButton.interactable = state;
        if (generateCodeButton != null) generateCodeButton.interactable = state;
        if (joinCodeInputField != null) joinCodeInputField.interactable = state;
    }
    #endregion

    private void ExitGame()
    {
        Debug.Log("[MainMenu] Exiting Game...");
        Application.Quit();
    }
}
