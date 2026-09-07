using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;

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

    [Header("Ambient Particle System (Optional)")]
    [SerializeField] private ParticleSystem ambientParticleSystem;
    [SerializeField] private Vector3 particleFlowDirection = new Vector3(5.0f, 0.0f, 0.0f);

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
    [SerializeField] private Button offlineModeButton; // "Offline Bot Mode" button
    [SerializeField] private TMP_InputField joinCodeInputField; // Repurposed to preserve editor serialization
    [SerializeField] private TextMeshProUGUI generatedCodeText; // Displays code if host
    [SerializeField] private TextMeshProUGUI playStatusText;
    [SerializeField] private Button playBackButton;
    [SerializeField] private Button logoutButton;

    [Header("Lobby Player Spawn Points")]
    [Tooltip("Transforms in Main Menu scene where room lobby players stand")]
    [SerializeField] private Transform[] lobbySpawnPoints;
    [SerializeField] private Vector3[] defaultLobbySpawnPositions = new Vector3[]
    {
        new Vector3(0f, 0.58f, 0f),      // Point 0 (my slot)
        new Vector3(-4.2f, 0.58f, 0f),   // Point 1 (friend 1)
        new Vector3(4.2f, 0.58f, 0f),    // Point 2 (friend 2)
        new Vector3(-8.4f, 0.58f, 0f)    // Point 3 (friend 3)
    };

    public Vector3 GetLobbySpawnPosition(int slotIndex)
    {
        if (slotIndex == 0)
        {
            return new Vector3(0f, 0.58f, 0f);
        }

        if (defaultLobbySpawnPositions != null && slotIndex >= 0 && slotIndex < defaultLobbySpawnPositions.Length)
        {
            return defaultLobbySpawnPositions[slotIndex];
        }

        if (lobbySpawnPoints != null && slotIndex >= 0 && slotIndex < lobbySpawnPoints.Length && lobbySpawnPoints[slotIndex] != null)
        {
            return lobbySpawnPoints[slotIndex].position;
        }

        float xOffset = (slotIndex == 0) ? 0f : ((slotIndex % 2 == 1) ? -4.2f * ((slotIndex + 1) / 2) : 4.2f * (slotIndex / 2));
        return new Vector3(xOffset, 0.58f, 0f);
    }

    [Header("Audio Settings")]
    [Tooltip("Sound effect played automatically when any UI button is clicked")]
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioSource audioSource;

    [Header("Friends System")]
    [SerializeField] private GameObject friendsPanel;
    [SerializeField] private Button navFriendsButton;
    [SerializeField] private Button tabSearchButton;
    [SerializeField] private Button tabRequestsButton;
    [SerializeField] private Button tabFriendsButton;
    [SerializeField] private Button friendsSearchButton;
    [SerializeField] private Button closeFriendsButton;
    [SerializeField] private Sprite activeTabSprite;
    [SerializeField] private Sprite inactiveTabSprite;
    [SerializeField] private Sprite playerDetailsButtonSprite;
    [SerializeField] private Sprite playerDetailsBackButtonSprite;
    [Tooltip("Custom background panel image sprite for incoming game invite popup modal")]
    [SerializeField] private Sprite invitePopupPanelSprite;
    [SerializeField] private TMP_InputField friendsSearchFilterInput;
    [SerializeField] private Transform friendsScrollViewContent;
    [SerializeField] private GameObject playerRowPrefab;
    [SerializeField] private GameObject playerPreviewPrefab;
    [SerializeField] private Vector3 playerPreviewScale = new Vector3(120f, 120f, 1f);
    [SerializeField] private Vector3 playerPreviewOffset = Vector3.zero;

    // State Variables
    public CharacterSkinData[] skins;
    private int cabinetSelectedIndex = 0;
    private int shopSelectedIndex = 0;
    private int coins = 1000;
    private GameObject previewPlayerInstance;
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();

    // Search Scroll Pagination & Friends Panel Caching Variables
    private List<CounterBoom.Networking.FirebaseUserData> cachedSearchPlayers = new List<CounterBoom.Networking.FirebaseUserData>();
    private List<CounterBoom.Networking.FirebaseUserData> filteredSearchPlayers = new List<CounterBoom.Networking.FirebaseUserData>();
    private List<CounterBoom.Networking.FriendProfile> cachedRequestsList = null;
    private List<CounterBoom.Networking.FriendProfile> cachedFriendsList = null;

    private float lastSearchFetchTime = -999f;
    private float lastRequestsFetchTime = -999f;
    private float lastFriendsFetchTime = -999f;
    private const float FRIENDS_TAB_CACHE_DURATION = 60f; // Cache data for 60 seconds per tab

    // Real-Time Presence & Join Request Caching
    private Dictionary<string, CounterBoom.Networking.FirebaseManager.PlayerPresenceData> cachedPresences = new Dictionary<string, CounterBoom.Networking.FirebaseManager.PlayerPresenceData>();
    private float lastPresenceFetchTime = -999f;
    private const float PRESENCE_CACHE_DURATION = 4f; // Refresh presence every 4 seconds
    private GameObject activeJoinRequestModal;

    private int currentSearchDisplayedCount = 0;
    private const int PLAYERS_PER_PAGE = 10;
    private bool isLoadingMoreSearchPlayers = false;
    private ScrollRect activeFriendsScrollRect;

    public static MainMenuController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        DeactivateAllSubPanels();
    }

    private void DeactivateAllSubPanels()
    {
        if (playPanel != null) playPanel.SetActive(false);
        if (cabinetPanel != null) cabinetPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (friendsPanel != null) friendsPanel.SetActive(false);
        if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
        if (userProfileModal != null) userProfileModal.SetActive(false);

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                string n = child.gameObject.name.ToLower();
                if (n.Contains("panel") && n != "mainpanel" && n != "canvas")
                {
                    child.gameObject.SetActive(false);
                }
                else if (n.Contains("modal") || n.Contains("dialog") || n.Contains("popup") || n.Contains("overlay"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void Update()
    {
        UpdateLobbyButtonsState();

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (previewPlayerInstance != null)
            {
                CleanupPreviewPlayer();
            }
        }
        else
        {
            // Solo mode: Restore preview player character on screen when room is destroyed or left!
            if (previewPlayerInstance == null && (mainPanel == null || mainPanel.activeSelf))
            {
                SetupPreviewPlayer();
                ResetPreviewToEquippedSkin();
            }
        }
    }

    public void UpdateLobbyButtonsState()
    {
        bool isRoomLobbyActive = Unity.Netcode.NetworkManager.Singleton != null && 
                                 Unity.Netcode.NetworkManager.Singleton.IsListening &&
                                 Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count > 1;

        PlayerController localPlayer = PlayerController.LocalPlayer;
        if (localPlayer == null)
        {
            var pcs = FindObjectsOfType<PlayerController>();
            foreach (var p in pcs)
            {
                if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
            }
        }

        bool isRoomHost = isRoomLobbyActive && (localPlayer != null && localPlayer.lobbySlotIndex.Value == 0);
        bool isRoomClient = isRoomLobbyActive && !isRoomHost;

        // Client in room: navPlayButton shows "READY" or "CANCEL". Host or solo: shows "PLAY"
        if (navPlayButton != null)
        {
            var playBtnTmp = navPlayButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (playBtnTmp != null)
            {
                if (isRoomClient)
                {
                    bool ready = localPlayer != null && localPlayer.isReady.Value;
                    playBtnTmp.text = ready ? "CANCEL" : "READY";
                }
                else
                {
                    playBtnTmp.text = "PLAY";
                }
            }
        }

        // If client in room, playPanel must not be open!
        if (isRoomClient && playPanel != null && playPanel.activeSelf)
        {
            playPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        // Inside playPanel: If connected with friends, disable other buttons, only enable "play online" (hostButton)
        if (playPanel != null && playPanel.activeSelf)
        {
            if (isRoomLobbyActive)
            {
                if (generateCodeButton != null) generateCodeButton.interactable = false;
                if (joinButton != null) joinButton.interactable = false;
                if (joinCodeInputField != null) joinCodeInputField.interactable = false;
                if (offlineModeButton != null) offlineModeButton.interactable = false;

                if (hostButton != null)
                {
                    hostButton.interactable = true;
                    var hostTmp = hostButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (hostTmp != null)
                    {
                        if (isRoomClient)
                        {
                            bool ready = localPlayer != null && localPlayer.isReady.Value;
                            hostTmp.text = ready ? "CANCEL READY" : "READY (PLAY ONLINE)";
                        }
                        else
                        {
                            hostTmp.text = "START MATCH";
                        }
                    }
                }
            }
            else
            {
                if (generateCodeButton != null) generateCodeButton.interactable = true;
                if (joinButton != null) joinButton.interactable = true;
                if (joinCodeInputField != null) joinCodeInputField.interactable = true;
                if (offlineModeButton != null) offlineModeButton.interactable = true;

                if (hostButton != null)
                {
                    hostButton.interactable = true;
                    var hostTmp = hostButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (hostTmp != null)
                    {
                        hostTmp.text = "play online";
                    }
                }
            }
        }
    }

    private void OnPlayButtonClicked()
    {
        PlayButtonClickSFX();

        bool isRoomLobbyActive = Unity.Netcode.NetworkManager.Singleton != null && 
                                 Unity.Netcode.NetworkManager.Singleton.IsListening &&
                                 Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count > 1;

        PlayerController localPlayer = PlayerController.LocalPlayer;
        if (localPlayer == null)
        {
            var pcs = FindObjectsOfType<PlayerController>();
            foreach (var p in pcs)
            {
                if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
            }
        }

        bool isRoomHost = isRoomLobbyActive && (localPlayer != null && localPlayer.lobbySlotIndex.Value == 0);
        bool isRoomClient = isRoomLobbyActive && !isRoomHost;

        if (isRoomClient)
        {
            // Client in room: Clicking the button toggles READY / CANCEL directly! Does NOT open playPanel.
            if (localPlayer != null)
            {
                localPlayer.isReady.Value = !localPlayer.isReady.Value;
                localPlayer.RefreshLobbyPositionAndState();
                UpdateLobbyButtonsState();
                Debug.Log($"[MainMenuController] Client toggled Ready to {localPlayer.isReady.Value}");
                UpdatePlayStatus(localPlayer.isReady.Value ? "<color=green>Ready for match!</color>" : "<color=yellow>Not Ready</color>");
            }
            return;
        }

        // Host or solo: Opens the play panel
        ShowPanel(playPanel);
    }

    private void Start()
    {
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) ScreenAndUIScaler.ConfigureCanvas(canvas);

        // Ensure sub-panels start cleanly closed on mobile APK
        DeactivateAllSubPanels();

        // 1. Initialize Player Coins
        coins = PlayerPrefs.GetInt("Coins", 1000);
        PlayerPrefs.SetInt("Coins", coins);

        // 2. Setup Player Prefab Preview & Fetch Available Skins
        SetupPreviewPlayer();
        if (skins != null && skins.Length > 0)
        {
            CharacterAssembler.SetGlobalSkins(skins);
        }

        // 2b. Initialize Player Profile Header UI & Settings Panel UI
        UpdatePlayerProfileUI();
        EnsureSettingsPanelUI();

        if (ambientParticleSystem != null)
        {
            var velocity = ambientParticleSystem.velocityOverLifetime;
            if (velocity.enabled)
            {
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(particleFlowDirection.x * 0.8f, particleFlowDirection.x * 1.2f);
                velocity.y = new ParticleSystem.MinMaxCurve(particleFlowDirection.y - 0.5f, particleFlowDirection.y + 0.5f);
                velocity.z = new ParticleSystem.MinMaxCurve(particleFlowDirection.z - 0.1f, particleFlowDirection.z + 0.1f);
            }
        }

        // 3. Register Navigation Listeners
        if (navPlayButton != null)
        {
            navPlayButton.onClick.RemoveAllListeners();
            navPlayButton.onClick.AddListener(OnPlayButtonClicked);
        }
        if (navCabinetButton != null) navCabinetButton.onClick.AddListener(() => ShowPanel(cabinetPanel));
        if (navShopButton != null) navShopButton.onClick.AddListener(() => ShowPanel(shopPanel));
        if (navFriendsButton != null)
        {
            navFriendsButton.onClick.RemoveListener(ShowFriendsPanel);
            navFriendsButton.onClick.AddListener(ShowFriendsPanel);
        }

        // 4. Start polling for real-time game invites & room join requests
        InvokeRepeating(nameof(PollGameInvitesTask), 2f, 3f);
        InvokeRepeating(nameof(PollJoinRoomRequestsTask), 2.5f, 3f);

        PlayerPrefs.DeleteKey("LastPartyFriendName");
        PlayerPrefs.DeleteKey("LastPartyFriendUid");
        PlayerPrefs.DeleteKey("LastPartyFriendSkin");
        PlayerPrefs.Save();

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

        // Friends System Tab & Close Listeners
        if (tabSearchButton != null)
        {
            tabSearchButton.onClick.RemoveAllListeners();
            tabSearchButton.onClick.AddListener(() => SwitchFriendsTab(0));
        }

        if (tabRequestsButton != null)
        {
            tabRequestsButton.onClick.RemoveAllListeners();
            tabRequestsButton.onClick.AddListener(() => SwitchFriendsTab(1));
        }

        if (tabFriendsButton != null)
        {
            tabFriendsButton.onClick.RemoveAllListeners();
            tabFriendsButton.onClick.AddListener(() => SwitchFriendsTab(2));
        }

        if (closeFriendsButton != null)
        {
            closeFriendsButton.onClick.RemoveAllListeners();
            closeFriendsButton.onClick.AddListener(CloseFriendsPanel);
        }

        // Quick Play Matchmaking (using hostButton / "play online") & Manual Join (using joinButton) & Generate Code
        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(OnQuickPlayClicked);
        }
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnManualJoinClicked);
        }

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
        if (generateCodeButton != null)
        {
            generateCodeButton.onClick.RemoveAllListeners();
            generateCodeButton.onClick.AddListener(OnGenerateCodeClicked);
        }

        // Ensure Offline Mode button on Play Panel
        EnsureOfflineModeButton();

        // 5. Initial Display Selection
        cabinetSelectedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        shopSelectedIndex = 0;

        // 6. Auto-register button click sound listeners across all UI buttons in scene
        RegisterButtonClickSounds();

        // 7. Direct to Main Panel cleanly (skip name entry if player is logged in or name exists)
        string currentName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(currentName))
        {
            PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);
            PlayerPrefs.Save();
            ShowPanel(mainPanel);
        }
        else
        {
            int nameHasBeenSet = PlayerPrefs.GetInt("PlayerNameHasBeenSet", 0);
            if (nameHasBeenSet == 1)
            {
                ShowPanel(mainPanel);
            }
            else
            {
                ShowPanel(nameEntryPanel);
            }
        }

        // Auto-wire all main navigation buttons across Canvas to guarantee click responsiveness on Android APK
        EnsureNavigationButtonsAutoWired();
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
        if (friendsPanel != null) friendsPanel.SetActive(false);

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
        if (panelToShow == cabinetPanel)
        {
            List<int> unlocked = GetUnlockedSkinIndices();
            int equippedRealIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            int pos = unlocked.IndexOf(equippedRealIndex);
            cabinetSelectedIndex = pos >= 0 ? pos : 0;
            UpdateCabinetUI();
        }
        else if (panelToShow == shopPanel)
        {
            shopSelectedIndex = 0;
            UpdateShopUI();
        }
        else
        {
            // For mainPanel, playPanel, friendsPanel, settingsPanel, or any other panel:
            // Always reset preview avatar back to the player's equipped skin!
            ResetPreviewToEquippedSkin();
            UpdatePlayerProfileUI();

            if (panelToShow == playPanel)
            {
                UpdatePlayStatus("Ready to search or host lobby");
                if (generatedCodeText != null) generatedCodeText.text = "JOIN CODE: -";
                UpdateLobbyButtonsState();
            }
        }

        // Re-scan for any newly activated or instantiated UI buttons
        RegisterButtonClickSounds();
    }

    private float lastClickSFXTime = -1f;
    private const float CLICK_SFX_COOLDOWN = 0.08f;

    public void PlayButtonClickSFX()
    {
        if (Time.unscaledTime - lastClickSFXTime < CLICK_SFX_COOLDOWN)
        {
            return;
        }
        lastClickSFXTime = Time.unscaledTime;

        if (buttonClickSFX != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                }
            }

            audioSource.PlayOneShot(buttonClickSFX);
        }
        else if (UniversalButtonAudio.Instance != null)
        {
            UniversalButtonAudio.Instance.PlayDefaultClickSFX();
        }
    }

    public void RegisterButtonClickSounds()
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button btn in allButtons)
        {
            if (btn != null && !registeredButtons.Contains(btn))
            {
                btn.onClick.AddListener(PlayButtonClickSFX);
                registeredButtons.Add(btn);
            }
        }
    }

    public void ResetPreviewToEquippedSkin()
    {
        int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);

        PlayerController localPlayer = PlayerController.LocalPlayer;
        if (localPlayer == null)
        {
            foreach (var p in FindObjectsOfType<PlayerController>())
            {
                if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
            }
        }

        if (localPlayer != null)
        {
            localPlayer.SetSkin(equippedIndex);
            return;
        }

        if (skins == null && previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
        }

        if (previewAssembler != null && skins != null && equippedIndex >= 0 && equippedIndex < skins.Length)
        {
            previewAssembler.SetCharacterSkin(skins[equippedIndex]);
        }

        List<int> unlocked = GetUnlockedSkinIndices();
        int pos = unlocked.IndexOf(equippedIndex);
        cabinetSelectedIndex = pos >= 0 ? pos : 0;
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
    public void SetupPreviewPlayer()
    {
        // 1. If spawned local player exists in room lobby, link previewAssembler directly WITHOUT setting previewPlayerInstance
        PlayerController localPlayer = PlayerController.LocalPlayer;
        if (localPlayer == null)
        {
            foreach (var p in FindObjectsOfType<PlayerController>())
            {
                if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
            }
        }

        if (localPlayer != null)
        {
            previewAssembler = localPlayer.GetComponentInChildren<CharacterAssembler>();
            if (previewAssembler != null)
            {
                skins = previewAssembler.GetAvailableSkins();
            }
            return;
        }

        if (previewPlayerInstance != null && previewPlayerInstance.name.Contains("PlayerPreview_MainMenu"))
        {
            previewAssembler = previewPlayerInstance.GetComponentInChildren<CharacterAssembler>();
            if (previewAssembler != null)
            {
                skins = previewAssembler.GetAvailableSkins();
                PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
            }
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

        // Force preview player position & scale (X=0, Y=0.58, Z=0 | Scale=3.081)
        obj.transform.position = new Vector3(0f, 0.58f, 0f);
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = new Vector3(3.081f, 3.081f, 3.081f);

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
            if (previewPlayerInstance.name.Contains("PlayerPreview_MainMenu"))
            {
                Debug.Log("[MainMenu] Destroying Main Menu player preview dummy instance.");
                Destroy(previewPlayerInstance);
            }
            previewPlayerInstance = null;
            previewAssembler = null;
        }

        // Search and destroy any static unspawned Player objects (dummy previews)
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        foreach (var pc in pcs)
        {
            if (pc != null && pc.gameObject != null)
            {
                var netObj = pc.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj == null || (!netObj.IsSpawned && !pc.IsOwner && !pc.IsLocalPlayer))
                {
                    if (pc.gameObject.name.Contains("PlayerPreview_MainMenu"))
                    {
                        Debug.Log($"[MainMenu] Destroying unspawned preview object '{pc.name}'.");
                        Destroy(pc.gameObject);
                    }
                }
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
        List<int> unlocked = GetUnlockedSkinIndices();
        if (unlocked.Count == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex - 1 + unlocked.Count) % unlocked.Count;
        UpdateCabinetUI();
    }

    private void CycleCabinetNext()
    {
        List<int> unlocked = GetUnlockedSkinIndices();
        if (unlocked.Count == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex + 1) % unlocked.Count;
        UpdateCabinetUI();
    }

    private void EquipSelectedSkin()
    {
        List<int> unlocked = GetUnlockedSkinIndices();
        if (unlocked.Count == 0 || cabinetSelectedIndex < 0 || cabinetSelectedIndex >= unlocked.Count) return;
        int realIndex = unlocked[cabinetSelectedIndex];

        PlayerPrefs.SetInt("EquippedSkinIndex", realIndex);
        if (skins != null && realIndex >= 0 && realIndex < skins.Length && skins[realIndex] != null)
        {
            PlayerPrefs.SetString("EquippedSkinName", skins[realIndex].skinName);
        }
        PlayerPrefs.Save();
        Debug.Log($"[MainMenu] Equipped skin index: {realIndex} ({skins[realIndex]?.skinName})");
        
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.SaveUserProfile();
        }

        PlayerController localPlayer = PlayerController.LocalPlayer;
        if (localPlayer == null)
        {
            foreach (var p in FindObjectsOfType<PlayerController>())
            {
                if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
            }
        }

        if (localPlayer != null)
        {
            localPlayer.SetSkin(realIndex);
        }
        else if (previewAssembler != null && skins != null && realIndex >= 0 && realIndex < skins.Length)
        {
            previewAssembler.SetCharacterSkin(skins[realIndex]);
        }

        UpdateCabinetUI();
        UpdatePlayerProfileUI();
    }

    private void UpdateCabinetUI()
    {
        if (previewAssembler == null) SetupPreviewPlayer();
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        List<int> unlocked = GetUnlockedSkinIndices();
        if (unlocked.Count == 0) return;

        if (cabinetSelectedIndex < 0 || cabinetSelectedIndex >= unlocked.Count)
        {
            cabinetSelectedIndex = 0;
        }

        int realIndex = unlocked[cabinetSelectedIndex];

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[realIndex]);

        // Render details
        if (cabinetSkinNameText != null)
        {
            cabinetSkinNameText.text = skins[realIndex].skinName;
        }

        bool isEquipped = PlayerPrefs.GetInt("EquippedSkinIndex", 0) == realIndex;

        if (cabinetSkinStatusText != null)
        {
            cabinetSkinStatusText.text = isEquipped ? "<color=green>EQUIPPED</color>" : "<color=yellow>SELECT</color>";
        }

        if (cabinetEquipButton != null)
        {
            cabinetEquipButton.interactable = !isEquipped;
            var btnTmp = cabinetEquipButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnTmp != null)
            {
                btnTmp.text = isEquipped ? "EQUIPPED" : "SELECT";
            }
            var btnTxt = cabinetEquipButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (btnTxt != null)
            {
                btnTxt.text = isEquipped ? "EQUIPPED" : "SELECT";
            }
        }
    }
    #endregion

    #region Shop Logic
    private void CycleShopPrev()
    {
        List<int> locked = GetLockedSkinIndices();
        if (locked.Count == 0) return;
        shopSelectedIndex = (shopSelectedIndex - 1 + locked.Count) % locked.Count;
        UpdateShopUI();
    }

    private void CycleShopNext()
    {
        List<int> locked = GetLockedSkinIndices();
        if (locked.Count == 0) return;
        shopSelectedIndex = (shopSelectedIndex + 1) % locked.Count;
        UpdateShopUI();
    }

    private void BuySelectedSkin()
    {
        List<int> locked = GetLockedSkinIndices();
        if (locked.Count == 0 || shopSelectedIndex < 0 || shopSelectedIndex >= locked.Count) return;

        int realIndex = locked[shopSelectedIndex];
        CharacterSkinData targetSkin = skins[realIndex];

        if (coins >= targetSkin.price)
        {
            coins -= targetSkin.price;
            PlayerPrefs.SetInt("Coins", coins);
            PlayerPrefs.SetInt($"Skin_Unlocked_{realIndex}", 1);
            PlayerPrefs.Save();

            Debug.Log($"[MainMenu] Purchased skin '{targetSkin.skinName}' (index {realIndex}) for {targetSkin.price} coins.");

            if (CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                CounterBoom.Networking.FirebaseManager.Instance.SaveUserProfile();
            }

            shopSelectedIndex = 0;
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

        // 1. Profile Name Text
        if (profileNameText != null)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", "Player #1");
            if (CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.CurrentUser != null)
            {
                var fbUser = CounterBoom.Networking.FirebaseManager.Instance.CurrentUser;
                if (!string.IsNullOrEmpty(fbUser.displayName))
                {
                    savedName = fbUser.displayName;
                }
            }
            profileNameText.text = savedName;
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
        Canvas rootCanvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        Transform rootSearch = rootCanvas != null ? rootCanvas.transform : transform;

        foreach (var img in rootSearch.GetComponentsInChildren<Image>(true))
        {
            string n = img.gameObject.name.ToLower();
            if (n.Contains("setting") || n.Contains("play") || n.Contains("exit") || n.Contains("shop") || n.Contains("cabinet") || n.Contains("coin") || n.Contains("button")) continue;

            if (n.Contains("profile") || n.Contains("avatar") || n.Contains("skinicon") || n.Contains("headicon") || n.Contains("profilebadge"))
            {
                img.raycastTarget = true;
                Button btn = img.GetComponent<Button>();
                if (btn == null) btn = img.gameObject.AddComponent<Button>();

                btn.onClick.RemoveListener(ShowUserProfileModal);
                btn.onClick.AddListener(ShowUserProfileModal);

                if (profileSkinIcon == null && (n.Contains("icon") || n.Contains("head") || n.Contains("avatar")))
                {
                    profileSkinIcon = img;
                }
            }
        }

        foreach (var tmp in rootSearch.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string n = tmp.gameObject.name.ToLower();
            if (n.Contains("setting") || n.Contains("play") || n.Contains("exit") || n.Contains("shop") || n.Contains("coin") || n.Contains("status") || n.Contains("button")) continue;

            if (n.Contains("profile") || n.Contains("playername") || n.Contains("username"))
            {
                tmp.raycastTarget = true;
                Button btn = tmp.GetComponent<Button>();
                if (btn == null) btn = tmp.gameObject.AddComponent<Button>();

                btn.onClick.RemoveListener(ShowUserProfileModal);
                btn.onClick.AddListener(ShowUserProfileModal);

                if (profileNameText == null) profileNameText = tmp;
            }
        }

        // Auto-find logoutButton if unassigned
        if (logoutButton == null)
        {
            foreach (var btn in rootSearch.GetComponentsInChildren<Button>(true))
            {
                string n = btn.gameObject.name.ToLower();
                if (n.Contains("logout") || n.Contains("signout") || n.Contains("exitlogin"))
                {
                    logoutButton = btn;
                    break;
                }
            }
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }
        else
        {
            CreateLogoutButtonUI();
        }

        // Auto-find mainCoinsText if unassigned
        if (mainCoinsText == null)
        {
            foreach (var tmp in rootSearch.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string n = tmp.gameObject.name.ToLower();
                if (n != "shopcointext" && (n.Contains("coin") || n.Contains("money") || n.Contains("gold")))
                {
                    mainCoinsText = tmp;
                    break;
                }
            }
        }

        // If still null, dynamically build Top Header Bar!
        if (profileNameText == null || mainCoinsText == null || profileSkinIcon == null)
        {
            CreateHeaderProfileBarUI();
        }
    }

    private void EnsureNavigationButtonsAutoWired()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        Transform rootSearch = rootCanvas != null ? rootCanvas.transform : transform;

        foreach (var btn in rootSearch.GetComponentsInChildren<Button>(true))
        {
            // Do NOT auto-wire internal buttons inside ANY sub-panels!
            if (playPanel != null && btn.transform.IsChildOf(playPanel.transform)) continue;
            if (cabinetPanel != null && btn.transform.IsChildOf(cabinetPanel.transform)) continue;
            if (shopPanel != null && btn.transform.IsChildOf(shopPanel.transform)) continue;
            if (friendsPanel != null && btn.transform.IsChildOf(friendsPanel.transform)) continue;
            if (userProfileModal != null && btn.transform.IsChildOf(userProfileModal.transform)) continue;
            if (settingsPanel != null && btn.transform.IsChildOf(settingsPanel.transform)) continue;
            if (nameEntryPanel != null && btn.transform.IsChildOf(nameEntryPanel.transform)) continue;

            string n = btn.gameObject.name.ToLower();
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
            string txt = tmp != null ? tmp.text.ToLower() : "";

            // Ignore internal panel sub-buttons (tabs, close, action buttons)
            if (n.Contains("tab") || n.Contains("close") || n.Contains("submit") || n.Contains("accept") || n.Contains("decline") || n.Contains("remove") || n.Contains("invite") || n.Contains("btn_") || n.Contains("row"))
            {
                continue;
            }

            if ((n.Contains("play") || txt.Contains("play")) && !n.Contains("back") && !n.Contains("player") && !n.Contains("offline") && !n.Contains("host") && !n.Contains("join"))
            {
                if (navPlayButton == null) navPlayButton = btn;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnPlayButtonClicked);
            }
            else if ((n.Contains("cabinet") || n.Contains("inventory") || txt.Contains("cabinet") || txt.Contains("inventory")) && !n.Contains("back") && !n.Contains("next") && !n.Contains("prev"))
            {
                if (navCabinetButton == null) navCabinetButton = btn;
                btn.onClick.AddListener(() => ShowPanel(cabinetPanel));
            }
            else if ((n.Contains("shop") || n.Contains("store") || txt.Contains("shop") || txt.Contains("store")) && !n.Contains("back") && !n.Contains("buy"))
            {
                if (navShopButton == null) navShopButton = btn;
                btn.onClick.AddListener(() => ShowPanel(shopPanel));
            }
            else if (n.Contains("setting") || txt.Contains("setting"))
            {
                if (navSettingsButton == null) navSettingsButton = btn;
                btn.onClick.AddListener(OpenSettingsPanel);
            }
            else if (n.Contains("friend") || txt.Contains("friend"))
            {
                if (navFriendsButton == null) navFriendsButton = btn;
                btn.onClick.RemoveListener(ShowFriendsPanel);
                btn.onClick.AddListener(ShowFriendsPanel);
            }
            else if (n.Contains("exit") || txt.Contains("exit") || txt.Contains("quit"))
            {
                if (navExitButton == null) navExitButton = btn;
                btn.onClick.AddListener(ExitGame);
            }
            else if (n.Contains("back") || txt.Contains("back") || txt.Contains("close"))
            {
                btn.onClick.AddListener(() => ShowPanel(mainPanel));
            }
        }

        if (navFriendsButton != null)
        {
            navFriendsButton.onClick.RemoveListener(ShowFriendsPanel);
            navFriendsButton.onClick.AddListener(ShowFriendsPanel);
        }
    }

    private void CreateLogoutButtonUI()
    {
        if (mainPanel == null || logoutButton != null) return;

        GameObject logoutBtnObj = new GameObject("DynamicLogoutButton", typeof(RectTransform), typeof(Image), typeof(Button));
        logoutBtnObj.transform.SetParent(mainPanel.transform, false);

        RectTransform lbRt = logoutBtnObj.GetComponent<RectTransform>();
        lbRt.anchorMin = new Vector2(1f, 1f);
        lbRt.anchorMax = new Vector2(1f, 1f);
        lbRt.pivot = new Vector2(1f, 1f);
        lbRt.anchoredPosition = new Vector2(-25f, -25f);
        lbRt.sizeDelta = new Vector2(120f, 48f);

        Image lbBg = logoutBtnObj.GetComponent<Image>();
        lbBg.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        GameObject logoutTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        logoutTxtObj.transform.SetParent(logoutBtnObj.transform, false);

        RectTransform ltRt = logoutTxtObj.GetComponent<RectTransform>();
        ltRt.anchorMin = Vector2.zero;
        ltRt.anchorMax = Vector2.one;
        ltRt.offsetMin = Vector2.zero;
        ltRt.offsetMax = Vector2.zero;

        TextMeshProUGUI logoutTxt = logoutTxtObj.GetComponent<TextMeshProUGUI>();
        logoutTxt.text = "LOGOUT";
        logoutTxt.fontSize = 16;
        logoutTxt.fontStyle = FontStyles.Bold;
        logoutTxt.color = Color.white;
        logoutTxt.alignment = TextAlignmentOptions.Center;

        logoutButton = logoutBtnObj.GetComponent<Button>();
        logoutButton.onClick.RemoveAllListeners();
        logoutButton.onClick.AddListener(OnLogoutClicked);
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

        Button pbBtn = profileBadgeObj.AddComponent<Button>();
        pbBtn.onClick.AddListener(ShowUserProfileModal);

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

        // 2b. Friends Button (if navFriendsButton is not assigned)
        if (navFriendsButton == null)
        {
            GameObject friendsBtnObj = new GameObject("DynamicFriendsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            friendsBtnObj.transform.SetParent(headerBarObj.transform, false);

            RectTransform fbRt = friendsBtnObj.GetComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0f, 0.5f);
            fbRt.anchorMax = new Vector2(0f, 0.5f);
            fbRt.pivot = new Vector2(0f, 0.5f);
            fbRt.anchoredPosition = new Vector2(380f, 0f);
            fbRt.sizeDelta = new Vector2(140f, 60f);

            Image fbBg = friendsBtnObj.GetComponent<Image>();
            fbBg.color = new Color(0.2f, 0.55f, 0.85f, 0.9f); // Blue friends button

            GameObject friendsTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            friendsTxtObj.transform.SetParent(friendsBtnObj.transform, false);

            RectTransform ftRt = friendsTxtObj.GetComponent<RectTransform>();
            ftRt.anchorMin = Vector2.zero; ftRt.anchorMax = Vector2.one; ftRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI friendsTxt = friendsTxtObj.GetComponent<TextMeshProUGUI>();
            friendsTxt.text = "👥 FRIENDS";
            friendsTxt.fontSize = 17;
            friendsTxt.fontStyle = FontStyles.Bold;
            friendsTxt.color = Color.white;
            friendsTxt.alignment = TextAlignmentOptions.Center;

            navFriendsButton = friendsBtnObj.GetComponent<Button>();
            navFriendsButton.onClick.AddListener(ShowFriendsPanel);
        }

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

        // 4. Logout Button (if logoutButton not assigned in scene)
        if (logoutButton == null)
        {
            GameObject logoutBtnObj = new GameObject("DynamicLogoutButton", typeof(RectTransform), typeof(Image), typeof(Button));
            logoutBtnObj.transform.SetParent(headerBarObj.transform, false);

            RectTransform lbRt = logoutBtnObj.GetComponent<RectTransform>();
            lbRt.anchorMin = new Vector2(0f, 0.5f);
            lbRt.anchorMax = new Vector2(0f, 0.5f);
            lbRt.pivot = new Vector2(0f, 0.5f);
            lbRt.anchoredPosition = new Vector2(680f, 0f);
            lbRt.sizeDelta = new Vector2(130f, 60f);

            Image lbBg = logoutBtnObj.GetComponent<Image>();
            lbBg.color = new Color(0.85f, 0.22f, 0.22f, 0.9f); // Translucent red logout button

            GameObject logoutTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            logoutTxtObj.transform.SetParent(logoutBtnObj.transform, false);

            RectTransform ltRt = logoutTxtObj.GetComponent<RectTransform>();
            ltRt.anchorMin = Vector2.zero;
            ltRt.anchorMax = Vector2.one;
            ltRt.offsetMin = Vector2.zero;
            ltRt.offsetMax = Vector2.zero;

            TextMeshProUGUI logoutTxt = logoutTxtObj.GetComponent<TextMeshProUGUI>();
            logoutTxt.text = "LOGOUT";
            logoutTxt.fontSize = 17;
            logoutTxt.fontStyle = FontStyles.Bold;
            logoutTxt.color = Color.white;
            logoutTxt.alignment = TextAlignmentOptions.Center;

            logoutButton = logoutBtnObj.GetComponent<Button>();
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveAllListeners();
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }
    }

    public void OnLogoutClicked()
    {
        Debug.Log("[MainMenuController] Player logging out...");
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.SignOut();
        }

        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MainMenuController] Error loading LoginScene by name: {ex.Message}. Loading index 0.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }

    private GameObject userProfileModal;
    private TextMeshProUGUI profileModalDetailsText;

    public void ShowUserProfileModal()
    {
        Debug.Log("[MainMenuController] ShowUserProfileModal() triggered! Displaying Player Profile Modal...");

        if (userProfileModal != null)
        {
            userProfileModal.SetActive(true);
            userProfileModal.transform.SetAsLastSibling();
            UpdateUserProfileModalDetails();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Dark Overlay
        userProfileModal = new GameObject("UserProfileModal", typeof(RectTransform), typeof(Image));
        userProfileModal.transform.SetParent(canvas.transform, false);

        RectTransform mRt = userProfileModal.GetComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero; mRt.anchorMax = Vector2.one; mRt.offsetMin = Vector2.zero; mRt.offsetMax = Vector2.zero;

        Image mOverlay = userProfileModal.GetComponent<Image>();
        mOverlay.color = new Color(0f, 0f, 0f, 0.75f);

        // 2. Card Panel Container
        GameObject cardGO = new GameObject("CardPanel", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(userProfileModal.transform, false);

        RectTransform cRt = cardGO.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f); cRt.anchorMax = new Vector2(0.5f, 0.5f); cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta = new Vector2(500f, 380f);

        Image cBg = cardGO.GetComponent<Image>();
        cBg.color = new Color(0.1f, 0.14f, 0.2f, 0.98f);

        // 3. Header Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(cardGO.transform, false);

        RectTransform tRt = titleGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -20f); tRt.sizeDelta = new Vector2(0f, 40f);

        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "PLAYER PROFILE";
        tTmp.fontSize = 22; tTmp.fontStyle = FontStyles.Bold; tTmp.color = new Color(1f, 0.85f, 0.2f);
        tTmp.alignment = TextAlignmentOptions.Center;

        // 4. Details Text Block
        GameObject infoGO = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        infoGO.transform.SetParent(cardGO.transform, false);

        RectTransform iRt = infoGO.GetComponent<RectTransform>();
        iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
        iRt.offsetMin = new Vector2(35f, 75f); iRt.offsetMax = new Vector2(-35f, -65f);

        profileModalDetailsText = infoGO.GetComponent<TextMeshProUGUI>();
        profileModalDetailsText.fontSize = 16;
        profileModalDetailsText.alignment = TextAlignmentOptions.TopLeft;
        profileModalDetailsText.lineSpacing = 10f;
        profileModalDetailsText.color = Color.white;

        UpdateUserProfileModalDetails();

        // 4b. Name Edit Input Field & Save Button
        GameObject editRowGO = new GameObject("EditNameRow", typeof(RectTransform));
        editRowGO.transform.SetParent(cardGO.transform, false);

        RectTransform editRt = editRowGO.GetComponent<RectTransform>();
        editRt.anchorMin = new Vector2(0.5f, 0f); editRt.anchorMax = new Vector2(0.5f, 0f); editRt.pivot = new Vector2(0.5f, 0f);
        editRt.anchoredPosition = new Vector2(0f, 75f); editRt.sizeDelta = new Vector2(430f, 40f);

        GameObject nameInpGO = new GameObject("NameInputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        nameInpGO.transform.SetParent(editRowGO.transform, false);

        RectTransform nInpRt = nameInpGO.GetComponent<RectTransform>();
        nInpRt.anchorMin = new Vector2(0f, 0.5f); nInpRt.anchorMax = new Vector2(0f, 0.5f); nInpRt.pivot = new Vector2(0f, 0.5f);
        nInpRt.anchoredPosition = Vector2.zero; nInpRt.sizeDelta = new Vector2(280f, 38f);

        Image nInpBg = nameInpGO.GetComponent<Image>();
        nInpBg.color = new Color(0.2f, 0.26f, 0.35f, 1f);

        TMP_InputField nameInputField = nameInpGO.GetComponent<TMP_InputField>();

        GameObject nTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        nTxtGO.transform.SetParent(nameInpGO.transform, false);
        RectTransform nTxtRt = nTxtGO.GetComponent<RectTransform>();
        nTxtRt.anchorMin = Vector2.zero; nTxtRt.anchorMax = Vector2.one; nTxtRt.sizeDelta = Vector2.zero;
        nTxtRt.offsetMin = new Vector2(12f, 0f); nTxtRt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI nTxtTmp = nTxtGO.GetComponent<TextMeshProUGUI>();
        nTxtTmp.fontSize = 15; nTxtTmp.alignment = TextAlignmentOptions.MidlineLeft; nTxtTmp.color = Color.white;

        nameInputField.textComponent = nTxtTmp;
        nameInputField.text = PlayerPrefs.GetString("PlayerName", "Mihir");

        // Save Name Button
        GameObject saveBtnGO = new GameObject("SaveNameBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        saveBtnGO.transform.SetParent(editRowGO.transform, false);

        RectTransform saveRt = saveBtnGO.GetComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(1f, 0.5f); saveRt.anchorMax = new Vector2(1f, 0.5f); saveRt.pivot = new Vector2(1f, 0.5f);
        saveRt.anchoredPosition = Vector2.zero; saveRt.sizeDelta = new Vector2(135f, 38f);

        Image saveBg = saveBtnGO.GetComponent<Image>();
        saveBg.color = new Color(0.2f, 0.65f, 0.32f, 1f); // Green save button

        GameObject saveTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        saveTxtGO.transform.SetParent(saveBtnGO.transform, false);
        RectTransform stRt = saveTxtGO.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one; stRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI stTmp = saveTxtGO.GetComponent<TextMeshProUGUI>();
        stTmp.text = "SAVE NAME"; stTmp.fontSize = 13; stTmp.fontStyle = FontStyles.Bold;
        stTmp.alignment = TextAlignmentOptions.Center; stTmp.color = Color.white;

        Button saveBtn = saveBtnGO.GetComponent<Button>();
        saveBtn.onClick.AddListener(() =>
        {
            string newName = nameInputField.text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                PlayerPrefs.SetString("PlayerName", newName);
                PlayerPrefs.SetString("CB_LastGoogleAccountName", newName);
                PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);
                PlayerPrefs.Save();

                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.UpdateDisplayName(newName);
                }

                UpdatePlayerProfileUI();
                UpdateUserProfileModalDetails();
                Debug.Log($"[MainMenuController] Player name updated to: {newName}");
            }
        });

        // 5. Close Button
        GameObject closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(cardGO.transform, false);

        RectTransform cBtnRt = closeGO.GetComponent<RectTransform>();
        cBtnRt.anchorMin = new Vector2(0.5f, 0f); cBtnRt.anchorMax = new Vector2(0.5f, 0f); cBtnRt.pivot = new Vector2(0.5f, 0f);
        cBtnRt.anchoredPosition = new Vector2(0f, 20f); cBtnRt.sizeDelta = new Vector2(140f, 40f);

        Image cBtnBg = closeGO.GetComponent<Image>();
        cBtnBg.color = new Color(0.85f, 0.22f, 0.22f, 1f);

        GameObject cTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTxtGO.transform.SetParent(closeGO.transform, false);
        RectTransform ctRt = cTxtGO.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one; ctRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI ctTmp = cTxtGO.GetComponent<TextMeshProUGUI>();
        ctTmp.text = "CLOSE"; ctTmp.fontSize = 15; ctTmp.fontStyle = FontStyles.Bold;
        ctTmp.alignment = TextAlignmentOptions.Center; ctTmp.color = Color.white;

        Button closeBtn = closeGO.GetComponent<Button>();
        closeBtn.onClick.AddListener(() =>
        {
            userProfileModal.SetActive(false);
        });

        userProfileModal.SetActive(true);
        userProfileModal.transform.SetAsLastSibling();
    }

    private void UpdateUserProfileModalDetails()
    {
        if (profileModalDetailsText == null) return;

        var user = CounterBoom.Networking.FirebaseManager.Instance != null ? CounterBoom.Networking.FirebaseManager.Instance.CurrentUser : null;
        string pName = user != null && !string.IsNullOrEmpty(user.displayName) ? user.displayName : PlayerPrefs.GetString("PlayerName", "Mihir");
        string pEmail = user != null && !string.IsNullOrEmpty(user.email) ? user.email : $"{pName.ToLower().Replace(" ", "")}@gmail.com";
        string pUid = user != null && !string.IsNullOrEmpty(user.uid) ? user.uid : "CB-" + Mathf.Abs(pName.GetHashCode()).ToString().Substring(0, 6);
        int pCoins = PlayerPrefs.GetInt("Coins", 1000);
        int pLevel = user != null ? user.level : 1;

        profileModalDetailsText.text = $"<b>Name:</b> <color=#4285f4>{pName}</color>\n" +
                                      $"<b>Email:</b> <color=#34a853>{pEmail}</color>\n" +
                                      $"<b>Player ID:</b> <color=#fbbc05>{pUid}</color>\n" +
                                      $"<b>Coins:</b> 🪙 {pCoins}\n" +
                                      $"<b>Account Level:</b> Level {pLevel}\n" +
                                      $"<b>Status:</b> <color=#34a853>Authenticated</color>";
    }
    #endregion

    private bool IsSkinUnlocked(int index)
    {
        if (index == 0) return true; // Default skin is always unlocked
        return PlayerPrefs.GetInt($"Skin_Unlocked_{index}", 0) == 1;
    }

    private System.Collections.Generic.List<int> GetUnlockedSkinIndices()
    {
        var unlocked = new System.Collections.Generic.List<int>();
        if (skins == null) return unlocked;
        for (int i = 0; i < skins.Length; i++)
        {
            if (IsSkinUnlocked(i)) unlocked.Add(i);
        }
        return unlocked;
    }

    private System.Collections.Generic.List<int> GetLockedSkinIndices()
    {
        var locked = new System.Collections.Generic.List<int>();
        if (skins == null) return locked;
        for (int i = 0; i < skins.Length; i++)
        {
            if (!IsSkinUnlocked(i)) locked.Add(i);
        }
        return locked;
    }

    public void UpdateShopUI()
    {
        if (previewAssembler == null) SetupPreviewPlayer();
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        if (shopCoinsText != null)
        {
            shopCoinsText.text = $"{coins}";
        }

        System.Collections.Generic.List<int> locked = GetLockedSkinIndices();
        if (locked.Count == 0)
        {
            if (shopSkinNameText != null) shopSkinNameText.text = "ALL SKINS OWNED";
            if (shopSkinPriceText != null) shopSkinPriceText.text = "No skins left in shop";
            if (shopBuyButton != null) shopBuyButton.gameObject.SetActive(false);
            ResetPreviewToEquippedSkin();
            return;
        }

        if (shopSelectedIndex < 0 || shopSelectedIndex >= locked.Count)
        {
            shopSelectedIndex = 0;
        }

        int realIndex = locked[shopSelectedIndex];

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[realIndex]);

        // Render skin info
        if (shopSkinNameText != null)
        {
            shopSkinNameText.text = skins[realIndex].skinName;
        }

        if (shopSkinPriceText != null)
        {
            shopSkinPriceText.text = $"Price: {skins[realIndex].price} Coins";
            if (shopBuyButton != null)
            {
                shopBuyButton.gameObject.SetActive(true);
                shopBuyButton.interactable = coins >= skins[realIndex].price;
            }
        }
    }
    #endregion

    #region Automatic Quick Play & Matchmaking Logic
    private void OnQuickPlayClicked()
    {
        bool isConnectedWithFriends = Unity.Netcode.NetworkManager.Singleton != null && 
                                     Unity.Netcode.NetworkManager.Singleton.IsListening &&
                                     Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count > 1;

        if (isConnectedWithFriends)
        {
            PlayerController localPlayer = PlayerController.LocalPlayer;
            if (localPlayer == null)
            {
                foreach (var p in FindObjectsOfType<PlayerController>())
                {
                    if (p != null && (p.IsOwner || p.IsLocal)) { localPlayer = p; break; }
                }
            }

            bool isRoomHost = localPlayer != null && localPlayer.lobbySlotIndex.Value == 0;

            if (isRoomHost)
            {
                Debug.Log("[MainMenuController] Room Host (slot 0) clicked Start Match -> Transitioning party to LoadingGame -> CustomLobby!");
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    LoadingGameController.TargetMode = LoadingGameController.MatchMode.PartyToLobby;
                    if (localPlayer != null)
                    {
                        localPlayer.NotifyPartyTargetModeClientRpc(LoadingGameController.MatchMode.PartyToLobby);
                    }

                    if (Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
                    {
                        Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("LoadingGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
                    }
                    else if (RelayNetworkManager.Instance != null)
                    {
                        RelayNetworkManager.Instance.ExecuteSceneLoad("LoadingGame");
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
                    }
                }
                else
                {
                    // Promoted client host (e.g. Android device) -> Request server to transition party together!
                    if (localPlayer != null)
                    {
                        localPlayer.RequestPartyStartMatchServerRpc();
                    }
                }
            }
            else
            {
                if (localPlayer != null)
                {
                    localPlayer.isReady.Value = !localPlayer.isReady.Value;
                    localPlayer.RefreshLobbyPositionAndState();
                    UpdateLobbyButtonsState();
                    Debug.Log($"[MainMenuController] Client clicked Play Online inside room -> toggled Ready to {localPlayer.isReady.Value}");
                    UpdatePlayStatus(localPlayer.isReady.Value ? "<color=green>Ready for match!</color>" : "<color=yellow>Not Ready</color>");
                }
            }
        }
        else
        {
            // Solo QuickPlay: Fully shut down any lingering network connections or lobbies!
            if (RelayNetworkManager.Instance != null)
            {
                _ = RelayNetworkManager.Instance.LeaveMatchGracefully();
            }
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }

            CleanupPreviewPlayer();
            LoadingGameController.TargetMode = LoadingGameController.MatchMode.QuickPlay;
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
        }
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

    private void OnOfflineModeClicked()
    {
        CleanupPreviewPlayer();
        Debug.Log("[MainMenu] Launching Offline Singleplayer Mode through Loading Scene...");
        LoadingGameController.TargetMode = LoadingGameController.MatchMode.OfflineMode;
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
    }

    private void EnsureOfflineModeButton()
    {
        if (offlineModeButton == null && playPanel != null)
        {
            foreach (var btn in playPanel.GetComponentsInChildren<Button>(true))
            {
                string n = btn.gameObject.name.ToLower();
                if (n.Contains("offline") || n.Contains("single") || n.Contains("bot"))
                {
                    offlineModeButton = btn;
                    break;
                }
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null && (tmp.text.ToLower().Contains("offline") || tmp.text.ToLower().Contains("single") || tmp.text.ToLower().Contains("bot")))
                {
                    offlineModeButton = btn;
                    break;
                }
            }

            if (offlineModeButton == null)
            {
                // Auto-create a sleek "OFFLINE BOT MODE" button inside playPanel
                GameObject btnGO = new GameObject("OfflineModeButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(playPanel.transform, false);

                RectTransform rt = btnGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(340f, 48f);
                rt.anchoredPosition = new Vector2(0f, -150f);

                Image img = btnGO.GetComponent<Image>();
                img.color = new Color(0.16f, 0.58f, 0.35f, 1f); // Vibrant Emerald Green

                Outline outline = btnGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.3f, 0.9f, 0.5f, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);

                GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGO.transform.SetParent(btnGO.transform, false);
                RectTransform txtRt = txtGO.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.sizeDelta = Vector2.zero;

                TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
                tmp.text = "🎮 OFFLINE MODE (VS BOTS)";
                tmp.fontSize = 18;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                offlineModeButton = btnGO.GetComponent<Button>();
            }
        }

        if (offlineModeButton != null)
        {
            offlineModeButton.onClick.RemoveListener(OnOfflineModeClicked);
            offlineModeButton.onClick.AddListener(OnOfflineModeClicked);
        }
    }

    public void UpdatePlayStatus(string message)
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
        if (offlineModeButton != null) offlineModeButton.interactable = state;
        if (joinCodeInputField != null) joinCodeInputField.interactable = state;
    }
    #endregion

    private void ExitGame()
    {
        Debug.Log("[MainMenu] Exiting Game...");
        Application.Quit();
    }

    #region Friends Panel Logic (3-Tab: Search, Requests, Friends)

    private enum FriendsTabType { Search = 0, Requests = 1, Friends = 2 }
    private FriendsTabType currentFriendsTab = FriendsTabType.Search;

    public void ShowFriendsPanel()
    {
        EnsureFriendsPanelUI();
        AutoBindFriendsPanelElements();
        ShowPanel(friendsPanel);
        if (friendsPanel != null)
        {
            friendsPanel.SetActive(true);
            friendsPanel.transform.SetAsLastSibling();
        }
        ResetPreviewToEquippedSkin();
        SwitchFriendsTab(FriendsTabType.Search);
    }

    private void AutoBindFriendsPanelElements()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (friendsPanel == null && canvas != null)
        {
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = t.gameObject.name.ToLower();
                if (nameLower.Contains("friend") && nameLower.Contains("panel"))
                {
                    friendsPanel = t.gameObject;
                    break;
                }
            }
        }

        if (friendsPanel == null) return;

        Button[] buttons = friendsPanel.GetComponentsInChildren<Button>(true);
        foreach (Button b in buttons)
        {
            string n = b.gameObject.name.ToLower();
            TMP_Text tmp = b.GetComponentInChildren<TMP_Text>();
            string textLower = tmp != null ? tmp.text.ToLower() : "";

            if (n == "tab1" || (n.Contains("tab") && (n.Contains("1") || textLower.Contains("search"))))
            {
                tabSearchButton = b;
            }
            else if (n == "tab2" || (n.Contains("tab") && (n.Contains("2") || textLower.Contains("friend"))))
            {
                tabFriendsButton = b;
            }
            else if (n == "tab3" || (n.Contains("tab") && (n.Contains("3") || textLower.Contains("request"))))
            {
                tabRequestsButton = b;
            }
            else if (n == "searchbtn" || n.Contains("btn_search") || n.Contains("searchbutton") || n.Contains("magnifier"))
            {
                friendsSearchButton = b;
            }
            else if (n.Contains("close") || textLower.Contains("close") || n.Contains("exit"))
            {
                closeFriendsButton = b;
            }
        }

        if (friendsSearchFilterInput == null)
        {
            friendsSearchFilterInput = friendsPanel.GetComponentInChildren<TMP_InputField>(true);
        }

        if (friendsScrollViewContent == null)
        {
            ScrollRect sr = friendsPanel.GetComponentInChildren<ScrollRect>(true);
            if (sr != null && sr.content != null)
            {
                friendsScrollViewContent = sr.content;
            }
        }

        // Bind tab button listeners
        if (tabSearchButton != null)
        {
            tabSearchButton.onClick.RemoveAllListeners();
            tabSearchButton.onClick.AddListener(() => SwitchFriendsTab(FriendsTabType.Search));
            tabSearchButton.onClick.AddListener(PlayButtonClickSFX);
        }

        if (tabRequestsButton != null)
        {
            tabRequestsButton.onClick.RemoveAllListeners();
            tabRequestsButton.onClick.AddListener(() => SwitchFriendsTab(FriendsTabType.Requests));
            tabRequestsButton.onClick.AddListener(PlayButtonClickSFX);
        }

        if (tabFriendsButton != null)
        {
            tabFriendsButton.onClick.RemoveAllListeners();
            tabFriendsButton.onClick.AddListener(() => SwitchFriendsTab(FriendsTabType.Friends));
            tabFriendsButton.onClick.AddListener(PlayButtonClickSFX);
        }

        if (friendsSearchButton != null)
        {
            friendsSearchButton.onClick.RemoveAllListeners();
            friendsSearchButton.onClick.AddListener(() => RefreshFriendsTabContent(forceApi: true));
            friendsSearchButton.onClick.AddListener(PlayButtonClickSFX);
        }

        if (closeFriendsButton != null)
        {
            closeFriendsButton.onClick.RemoveAllListeners();
            closeFriendsButton.onClick.AddListener(CloseFriendsPanel);
            closeFriendsButton.onClick.AddListener(PlayButtonClickSFX);
        }

        if (friendsSearchFilterInput != null)
        {
            friendsSearchFilterInput.onValueChanged.RemoveAllListeners();
            friendsSearchFilterInput.onValueChanged.AddListener(OnFriendsSearchFilterChanged);
            friendsSearchFilterInput.onSubmit.RemoveAllListeners();
            friendsSearchFilterInput.onSubmit.AddListener((val) => ApplyLocalFriendsFilter());
            friendsSearchFilterInput.onEndEdit.RemoveAllListeners();
            friendsSearchFilterInput.onEndEdit.AddListener((val) => ApplyLocalFriendsFilter());
        }
    }

    public void CloseFriendsPanel()
    {
        if (friendsPanel != null)
        {
            friendsPanel.SetActive(false);
        }
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
        ResetPreviewToEquippedSkin();
        UpdatePlayerProfileUI();
    }

    public void SwitchFriendsTab(int tabIndex)
    {
        SwitchFriendsTab((FriendsTabType)tabIndex);
    }

    private void SwitchFriendsTab(FriendsTabType tab)
    {
        currentFriendsTab = tab;
        UpdateFriendsTabVisuals();
        RefreshFriendsTabContent(forceApi: false);
    }

    private void UpdateFriendsTabVisuals()
    {
        SetTabButtonVisual(tabSearchButton, currentFriendsTab == FriendsTabType.Search);
        SetTabButtonVisual(tabRequestsButton, currentFriendsTab == FriendsTabType.Requests);
        SetTabButtonVisual(tabFriendsButton, currentFriendsTab == FriendsTabType.Friends);
    }

    private void SetTabButtonVisual(Button btn, bool isActive)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            if (isActive && activeTabSprite != null)
            {
                img.sprite = activeTabSprite;
            }
            else if (!isActive && inactiveTabSprite != null)
            {
                img.sprite = inactiveTabSprite;
            }

            img.color = isActive ? new Color(1f, 1f, 1f, 1f) : new Color(0.65f, 0.72f, 0.82f, 0.8f);
        }

        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.color = isActive ? new Color(1f, 1f, 1f, 1f) : new Color(0.7f, 0.78f, 0.88f, 0.8f);
            tmp.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        }

        btn.transform.localScale = isActive ? Vector3.one * 1.04f : Vector3.one;
    }

    private void OnFriendsSearchFilterChanged(string text)
    {
        // Pure local in-memory filter: 0 network/API requests to Firebase!
        ApplyLocalFriendsFilter();
    }

    private void ClearScrollViewContent()
    {
        if (friendsScrollViewContent == null) return;
        foreach (Transform child in friendsScrollViewContent)
        {
            Destroy(child.gameObject);
        }
    }

    private void EnsureFriendsScrollViewLayout()
    {
        if (friendsScrollViewContent == null) return;

        var vlg = friendsScrollViewContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = friendsScrollViewContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = friendsScrollViewContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = friendsScrollViewContent.gameObject.AddComponent<ContentSizeFitter>();
        }
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private void ApplyLocalFriendsFilter()
    {
        if (friendsScrollViewContent == null) return;

        EnsureFriendsScrollViewLayout();
        ClearScrollViewContent();

        string filter = friendsSearchFilterInput != null ? friendsSearchFilterInput.text.Trim() : "";
        string cleanFilter = filter.ToLower();

        if (currentFriendsTab == FriendsTabType.Search)
        {
            if (string.IsNullOrEmpty(cleanFilter))
            {
                filteredSearchPlayers.Clear();
                currentSearchDisplayedCount = 0;
                CreateFriendsInfoCard("Type a player name or ID to search.");
                return;
            }

            if (cachedSearchPlayers == null || cachedSearchPlayers.Count == 0)
            {
                RefreshFriendsTabContent(forceApi: false);
                return;
            }

            filteredSearchPlayers = cachedSearchPlayers.FindAll(p => (!string.IsNullOrEmpty(p.displayName) && p.displayName.ToLower().Contains(cleanFilter)) ||
                                                               (!string.IsNullOrEmpty(p.uid) && p.uid.ToLower().Contains(cleanFilter)));

            currentSearchDisplayedCount = 0;

            if (filteredSearchPlayers.Count == 0)
            {
                CreateFriendsInfoCard($"No players found matching '{filter}'");
                return;
            }

            LoadMoreSearchPlayersBatch();
        }
        else if (currentFriendsTab == FriendsTabType.Requests)
        {
            if (cachedRequestsList == null)
            {
                RefreshFriendsTabContent(forceApi: false);
                return;
            }

            var filtered = string.IsNullOrEmpty(cleanFilter)
                ? cachedRequestsList
                : cachedRequestsList.FindAll(r => (!string.IsNullOrEmpty(r.displayName) && r.displayName.ToLower().Contains(cleanFilter)) ||
                                                  (!string.IsNullOrEmpty(r.uid) && r.uid.ToLower().Contains(cleanFilter)));

            if (filtered.Count == 0)
            {
                CreateFriendsInfoCard(!string.IsNullOrEmpty(filter) ? $"No pending requests found matching '{filter}'" : "No pending friend requests.");
                return;
            }

            foreach (var req in filtered)
            {
                CreatePlayerRowItem(friendsScrollViewContent, req.uid, req.displayName, req.level, req.selectedSkinIndex, 1000, FriendsTabType.Requests);
            }
        }
        else if (currentFriendsTab == FriendsTabType.Friends)
        {
            if (cachedFriendsList == null)
            {
                RefreshFriendsTabContent(forceApi: false);
                return;
            }

            // Check if presences need a fresh fetch
            if (Time.time - lastPresenceFetchTime >= PRESENCE_CACHE_DURATION && CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                lastPresenceFetchTime = Time.time;
                CounterBoom.Networking.FirebaseManager.Instance.FetchAllPresences((presences) =>
                {
                    cachedPresences = presences ?? new Dictionary<string, CounterBoom.Networking.FirebaseManager.PlayerPresenceData>();
                    if (currentFriendsTab == FriendsTabType.Friends)
                    {
                        ApplyLocalFriendsFilter();
                    }
                });
            }

            var filtered = string.IsNullOrEmpty(cleanFilter)
                ? cachedFriendsList
                : cachedFriendsList.FindAll(f => (!string.IsNullOrEmpty(f.displayName) && f.displayName.ToLower().Contains(cleanFilter)) ||
                                                 (!string.IsNullOrEmpty(f.uid) && f.uid.ToLower().Contains(cleanFilter)));

            if (filtered.Count == 0)
            {
                CreateFriendsInfoCard(!string.IsNullOrEmpty(filter) ? $"No friends found matching '{filter}'" : "No friends added yet. Go to SEARCH tab to add players!");
                return;
            }

            foreach (var friend in filtered)
            {
                CreatePlayerRowItem(friendsScrollViewContent, friend.uid, friend.displayName, friend.level, friend.selectedSkinIndex, 1000, FriendsTabType.Friends);
            }
        }
    }

    private void RefreshFriendsTabContent(bool forceApi = false)
    {
        if (friendsScrollViewContent == null) return;

        EnsureFriendsScrollViewLayout();

        // Wire ScrollRect scroll listener for infinite scroll pagination
        if (activeFriendsScrollRect == null && friendsScrollViewContent != null)
        {
            activeFriendsScrollRect = friendsScrollViewContent.GetComponentInParent<ScrollRect>();
        }
        if (activeFriendsScrollRect != null)
        {
            activeFriendsScrollRect.onValueChanged.RemoveAllListeners();
            activeFriendsScrollRect.onValueChanged.AddListener(OnFriendsScrollValueChanged);
        }

        if (CounterBoom.Networking.FirebaseManager.Instance == null)
        {
            ClearScrollViewContent();
            CreateFriendsInfoCard("Connecting to Social Services...");
            return;
        }

        FriendsTabType targetTab = currentFriendsTab;

        if (targetTab == FriendsTabType.Search)
        {
            string filter = friendsSearchFilterInput != null ? friendsSearchFilterInput.text.Trim() : "";
            if (string.IsNullOrEmpty(filter) && !forceApi)
            {
                ClearScrollViewContent();
                CreateFriendsInfoCard("Type a player name or ID to search.");
                return;
            }

            bool hasValidCache = !forceApi && cachedSearchPlayers != null && cachedSearchPlayers.Count > 0 &&
                                 (Time.time - lastSearchFetchTime < FRIENDS_TAB_CACHE_DURATION);
            if (hasValidCache)
            {
                ApplyLocalFriendsFilter();
                return;
            }

            ClearScrollViewContent();
            CreateFriendsInfoCard("Searching players...", isLoading: true);
            CounterBoom.Networking.FirebaseManager.Instance.SearchPlayers("", (results) =>
            {
                if (currentFriendsTab != FriendsTabType.Search) return;

                cachedSearchPlayers = results ?? new List<CounterBoom.Networking.FirebaseUserData>();
                lastSearchFetchTime = Time.time;
                ApplyLocalFriendsFilter();
            }, forceRefresh: forceApi);
        }
        else if (targetTab == FriendsTabType.Requests)
        {
            bool hasValidCache = !forceApi && cachedRequestsList != null &&
                                 (Time.time - lastRequestsFetchTime < FRIENDS_TAB_CACHE_DURATION);
            if (hasValidCache)
            {
                ApplyLocalFriendsFilter();
                return;
            }

            ClearScrollViewContent();
            CreateFriendsInfoCard("Loading friend requests...", isLoading: true);
            CounterBoom.Networking.FirebaseManager.Instance.FetchPendingRequestsList((requests) =>
            {
                if (currentFriendsTab != FriendsTabType.Requests) return;

                cachedRequestsList = requests ?? new List<CounterBoom.Networking.FriendProfile>();
                lastRequestsFetchTime = Time.time;
                ApplyLocalFriendsFilter();
            });
        }
        else if (targetTab == FriendsTabType.Friends)
        {
            bool hasValidCache = !forceApi && cachedFriendsList != null &&
                                 (Time.time - lastFriendsFetchTime < FRIENDS_TAB_CACHE_DURATION);
            if (hasValidCache)
            {
                ApplyLocalFriendsFilter();
                return;
            }

            ClearScrollViewContent();
            CreateFriendsInfoCard("Loading friends list...", isLoading: true);
            CounterBoom.Networking.FirebaseManager.Instance.FetchFriendsList((friends) =>
            {
                if (currentFriendsTab != FriendsTabType.Friends) return;

                cachedFriendsList = friends ?? new List<CounterBoom.Networking.FriendProfile>();
                lastFriendsFetchTime = Time.time;
                ApplyLocalFriendsFilter();
            });
        }
    }

    private void OnFriendsScrollValueChanged(Vector2 scrollPos)
    {
        if (currentFriendsTab != FriendsTabType.Search) return;
        if (isLoadingMoreSearchPlayers) return;

        var listToUse = (filteredSearchPlayers != null && filteredSearchPlayers.Count > 0) ? filteredSearchPlayers : cachedSearchPlayers;
        if (listToUse == null || currentSearchDisplayedCount >= listToUse.Count) return;

        // When user scrolls near the bottom of the ScrollRect (y <= 0.08f)
        if (scrollPos.y <= 0.08f)
        {
            LoadMoreSearchPlayersBatch();
        }
    }

    private void LoadMoreSearchPlayersBatch()
    {
        if (isLoadingMoreSearchPlayers) return;

        var listToUse = (filteredSearchPlayers != null && filteredSearchPlayers.Count > 0) ? filteredSearchPlayers : cachedSearchPlayers;
        if (listToUse == null || currentSearchDisplayedCount >= listToUse.Count) return;

        isLoadingMoreSearchPlayers = true;

        int countToLoad = Mathf.Min(PLAYERS_PER_PAGE, listToUse.Count - currentSearchDisplayedCount);
        for (int i = 0; i < countToLoad; i++)
        {
            var p = listToUse[currentSearchDisplayedCount + i];
            CreatePlayerRowItem(friendsScrollViewContent, p.uid, p.displayName, p.level, p.selectedSkinIndex, p.coins, FriendsTabType.Search);
        }

        currentSearchDisplayedCount += countToLoad;
        isLoadingMoreSearchPlayers = false;
    }

    public Sprite GetSkinHeadSprite(int skinIndex)
    {
        if (skins == null && previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
        }
        if (skins == null)
        {
            previewAssembler = FindObjectOfType<CharacterAssembler>();
            if (previewAssembler != null) skins = previewAssembler.GetAvailableSkins();
        }

        if (skins != null && skinIndex >= 0 && skinIndex < skins.Length)
        {
            var skinData = skins[skinIndex];
            if (skinData != null && skinData.head != null)
            {
                return skinData.head;
            }
        }
        return profileSkinIcon != null ? profileSkinIcon.sprite : null;
    }

    private Sprite runtimeSpinnerSprite;
    private Sprite GetOrCreateSpinnerSprite()
    {
        if (runtimeSpinnerSprite != null) return runtimeSpinnerSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
        float outerR = size / 2f - 2f;
        float innerR = size / 2f - 9f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y) - center;
                float dist = pos.magnitude;
                float angle = Mathf.Atan2(pos.y, pos.x);
                if (dist >= innerR && dist <= outerR)
                {
                    float normalizedAngle = (angle + Mathf.PI) / (2f * Mathf.PI);
                    float alpha = Mathf.Pow(normalizedAngle, 1.8f);
                    colors[y * size + x] = new Color(0.3f, 0.85f, 1f, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        runtimeSpinnerSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return runtimeSpinnerSprite;
    }

    private void CreateFriendsInfoCard(string message, bool isLoading = false)
    {
        if (friendsScrollViewContent == null) return;

        // Ensure parent VerticalLayoutGroup aligns children to center
        var vlg = friendsScrollViewContent.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
        }

        GameObject card = new GameObject(isLoading ? "LoadingCard" : "InfoCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        card.transform.SetParent(friendsScrollViewContent, false);

        RectTransform rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 90f);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 460f;
        le.preferredHeight = 90f;
        le.minHeight = 80f;
        le.flexibleWidth = 1f;

        Image bg = card.GetComponent<Image>();
        bg.color = isLoading ? new Color(0.1f, 0.22f, 0.35f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.9f);

        HorizontalLayoutGroup hlg = card.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 14f;
        hlg.padding = new RectOffset(20, 20, 10, 10);
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        if (isLoading)
        {
            GameObject spinnerGO = new GameObject("SpinnerIcon", typeof(RectTransform), typeof(Image), typeof(LoadingSpinnerRotator));
            spinnerGO.transform.SetParent(card.transform, false);

            RectTransform sRt = spinnerGO.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(30f, 30f);

            Image spinnerImg = spinnerGO.GetComponent<Image>();
            spinnerImg.sprite = GetOrCreateSpinnerSprite();
            spinnerImg.color = new Color(0.3f, 0.85f, 1f, 1f);
        }

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(card.transform, false);

        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.sizeDelta = new Vector2(isLoading ? 350f : 420f, 60f);

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 15;
        tmp.fontStyle = isLoading ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = isLoading ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = isLoading ? new Color(0.4f, 0.85f, 1f, 1f) : new Color(0.85f, 0.9f, 0.98f, 0.95f);
        tmp.enableWordWrapping = true;
    }

    public class LoadingSpinnerRotator : MonoBehaviour
    {
        public float rotateSpeed = 300f;
        private void Update()
        {
            transform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    private Sprite GetCardFrameSprite()
    {
        if (playerRowPrefab != null)
        {
            var img = playerRowPrefab.GetComponent<Image>();
            if (img != null && img.sprite != null) return img.sprite;
        }
        if (friendsPanel != null)
        {
            var img = friendsPanel.GetComponent<Image>();
            if (img != null && img.sprite != null) return img.sprite;
        }
        return null;
    }

    private Sprite GetButtonFrameSprite(bool isBackButton = false)
    {
        if (isBackButton && playerDetailsBackButtonSprite != null)
        {
            return playerDetailsBackButtonSprite;
        }
        if (playerDetailsButtonSprite != null)
        {
            return playerDetailsButtonSprite;
        }
        if (playerRowPrefab != null)
        {
            foreach (var img in playerRowPrefab.GetComponentsInChildren<Image>(true))
            {
                string n = img.gameObject.name.ToLower();
                if ((n.Contains("add") || n.Contains("accept") || n.Contains("invite") || n.Contains("btn") || n.Contains("button")) && img.sprite != null)
                {
                    return img.sprite;
                }
            }
        }
        if (closeFriendsButton != null)
        {
            var img = closeFriendsButton.GetComponent<Image>();
            if (img != null && img.sprite != null) return img.sprite;
        }
        return null;
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void EnsureHeadAndHandsAboveBody(GameObject previewGO, CharacterAssembler assembler)
    {
        if (previewGO == null) return;

        var uiPreview = previewGO.GetComponentInChildren<CounterBoom.UI.PlayerUIPreview>();
        if (uiPreview != null)
        {
            uiPreview.UpdateSortingOrders();
            return;
        }

        // 1. RightArm & Legs to BACK
        foreach (Transform t in previewGO.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if ((n.Contains("right") && (n.Contains("arm") || n.Contains("hand"))) || n.Contains("rightarm") || n.Contains("leg"))
            {
                t.SetAsLastSibling();
            }
        }

        // 2. Body / Torso
        foreach (Transform t in previewGO.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n == "body" || n == "torso")
            {
                t.SetAsLastSibling();
            }
        }

        // 3. Head & face details
        foreach (Transform t in previewGO.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n == "head" || n.Contains("eye") || n.Contains("eyebrow") || n.Contains("mouth"))
            {
                t.SetAsLastSibling();
            }
        }

        // 4. LeftArm (Front Arm)
        foreach (Transform t in previewGO.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if ((n.Contains("left") && (n.Contains("arm") || n.Contains("hand"))) || n.Contains("leftarm"))
            {
                t.SetAsLastSibling();
            }
        }
    }

    private GameObject CreateFullSkinPreviewUI(int skinIndex, Transform parent)
    {
        if (skins == null && previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
        }
        if (skins == null)
        {
            previewAssembler = FindObjectOfType<CharacterAssembler>();
            if (previewAssembler != null) skins = previewAssembler.GetAvailableSkins();
        }

        CharacterSkinData skin = null;
        if (skins != null && skinIndex >= 0 && skinIndex < skins.Length)
        {
            skin = skins[skinIndex];
        }

        if (playerPreviewPrefab != null)
        {
            GameObject instantiatedPreview = Instantiate(playerPreviewPrefab, parent, false);
            instantiatedPreview.name = "PlayerPreviewInstance";
            instantiatedPreview.SetActive(true);

            // 1. Disable gameplay scripts & physics so it doesn't move off-screen
            foreach (var mb in instantiatedPreview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null && !(mb is CounterBoom.UI.PlayerUIPreview) && !(mb is CharacterAssembler))
                {
                    mb.enabled = false;
                }
            }
            foreach (var rb in instantiatedPreview.GetComponentsInChildren<Rigidbody2D>(true))
            {
                rb.simulated = false;
            }
            foreach (var col in instantiatedPreview.GetComponentsInChildren<Collider2D>(true))
            {
                col.enabled = false;
            }

            // 2. Ensure LayoutElement exists so HorizontalLayoutGroup allocates space
            var prevLe = instantiatedPreview.GetComponent<LayoutElement>();
            if (prevLe == null) prevLe = instantiatedPreview.AddComponent<LayoutElement>();
            prevLe.preferredWidth = 110f;
            prevLe.preferredHeight = 135f;
            prevLe.minWidth = 90f;
            prevLe.minHeight = 120f;

            // 3. Position & apply scale for UI display
            Vector3 pos = playerPreviewOffset;
            instantiatedPreview.transform.localPosition = pos;
            instantiatedPreview.transform.localScale = playerPreviewScale;

            // 4. Set Layer to UI (5)
            SetLayerRecursively(instantiatedPreview, 5);

            // 5. Manage skin & 2D UI Image rendering via PlayerUIPreview script
            var uiPreview = instantiatedPreview.GetComponentInChildren<CounterBoom.UI.PlayerUIPreview>();
            if (uiPreview == null)
            {
                uiPreview = instantiatedPreview.AddComponent<CounterBoom.UI.PlayerUIPreview>();
                uiPreview.AutoFindRenderers();
            }

            if (uiPreview != null)
            {
                if (skin == null && skins != null && skinIndex >= 0 && skinIndex < skins.Length)
                {
                    skin = skins[skinIndex];
                }
                if (skin != null)
                {
                    uiPreview.SetSkin(skin);
                }
            }
            else
            {
                var assembler = instantiatedPreview.GetComponentInChildren<CharacterAssembler>();
                if (assembler != null && skin != null)
                {
                    assembler.SetCharacterSkin(skin);
                    assembler.UpdateSortingLayers();
                }
            }

            return instantiatedPreview;
        }

        GameObject container = new GameObject("FullSkinPreviewUI", typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(parent, false);

        RectTransform cRt = container.GetComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(110f, 135f);

        LayoutElement le = container.GetComponent<LayoutElement>();
        le.preferredWidth = 110f;
        le.preferredHeight = 135f;
        le.minWidth = 90f;
        le.minHeight = 120f;

        if (skin == null)
        {
            GameObject fbGO = new GameObject("FallbackHead", typeof(RectTransform), typeof(Image));
            fbGO.transform.SetParent(container.transform, false);
            RectTransform fbRt = fbGO.GetComponent<RectTransform>();
            fbRt.anchoredPosition = Vector2.zero;
            fbRt.sizeDelta = new Vector2(80f, 80f);
            Image fbImg = fbGO.GetComponent<Image>();
            fbImg.sprite = GetSkinHeadSprite(skinIndex);
            fbImg.preserveAspect = true;
            return container;
        }

        // 1. Back Layer: Right Arm & Legs (BEHIND body)
        if (skin.rightArm != null)
        {
            GameObject rArm = new GameObject("RightArm", typeof(RectTransform), typeof(Image));
            rArm.transform.SetParent(container.transform, false);
            RectTransform rt = rArm.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(28f, -6f);
            rt.sizeDelta = new Vector2(20f, 38f);
            Image img = rArm.GetComponent<Image>();
            img.sprite = skin.rightArm;
            img.preserveAspect = true;
        }

        if (skin.leftLeg != null)
        {
            GameObject lLeg = new GameObject("LeftLeg", typeof(RectTransform), typeof(Image));
            lLeg.transform.SetParent(container.transform, false);
            RectTransform rt = lLeg.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-11f, -42f);
            rt.sizeDelta = new Vector2(22f, 40f);
            Image img = lLeg.GetComponent<Image>();
            img.sprite = skin.leftLeg;
            img.preserveAspect = true;
        }

        if (skin.rightLeg != null)
        {
            GameObject rLeg = new GameObject("RightLeg", typeof(RectTransform), typeof(Image));
            rLeg.transform.SetParent(container.transform, false);
            RectTransform rt = rLeg.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(11f, -42f);
            rt.sizeDelta = new Vector2(22f, 40f);
            Image img = rLeg.GetComponent<Image>();
            img.sprite = skin.rightLeg;
            img.preserveAspect = true;
        }

        // 2. Middle Layer: Body Torso (ON TOP of right arm & legs)
        if (skin.body != null)
        {
            GameObject bodyGO = new GameObject("Torso", typeof(RectTransform), typeof(Image));
            bodyGO.transform.SetParent(container.transform, false);
            RectTransform rt = bodyGO.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(46f, 46f);
            Image img = bodyGO.GetComponent<Image>();
            img.sprite = skin.body;
            img.preserveAspect = true;
        }

        // 3. Head
        if (skin.head != null)
        {
            GameObject headGO = new GameObject("Head", typeof(RectTransform), typeof(Image));
            headGO.transform.SetParent(container.transform, false);
            RectTransform rt = headGO.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, 32f);
            rt.sizeDelta = new Vector2(62f, 62f);
            Image img = headGO.GetComponent<Image>();
            img.sprite = skin.head;
            img.preserveAspect = true;
        }

        // 4. Face details: Eyes, Eyebrows & Mouth (ON TOP of head)
        if (skin.leftEye != null)
        {
            GameObject eye = new GameObject("LeftEye", typeof(RectTransform), typeof(Image));
            eye.transform.SetParent(container.transform, false);
            RectTransform rt = eye.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-11f, 32f);
            rt.sizeDelta = new Vector2(12f, 12f);
            Image img = eye.GetComponent<Image>();
            img.sprite = skin.leftEye;
            img.preserveAspect = true;
        }

        if (skin.rightEye != null)
        {
            GameObject eye = new GameObject("RightEye", typeof(RectTransform), typeof(Image));
            eye.transform.SetParent(container.transform, false);
            RectTransform rt = eye.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(11f, 32f);
            rt.sizeDelta = new Vector2(12f, 12f);
            Image img = eye.GetComponent<Image>();
            img.sprite = skin.rightEye;
            img.preserveAspect = true;
        }

        if (skin.leftEyebrow != null)
        {
            GameObject eb = new GameObject("LeftEyebrow", typeof(RectTransform), typeof(Image));
            eb.transform.SetParent(container.transform, false);
            RectTransform rt = eb.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-11f, 40f);
            rt.sizeDelta = new Vector2(14f, 8f);
            Image img = eb.GetComponent<Image>();
            img.sprite = skin.leftEyebrow;
            img.preserveAspect = true;
        }

        if (skin.rightEyebrow != null)
        {
            GameObject eb = new GameObject("RightEyebrow", typeof(RectTransform), typeof(Image));
            eb.transform.SetParent(container.transform, false);
            RectTransform rt = eb.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(11f, 40f);
            rt.sizeDelta = new Vector2(14f, 8f);
            Image img = eb.GetComponent<Image>();
            img.sprite = skin.rightEyebrow;
            img.preserveAspect = true;
        }

        if (skin.mouth != null)
        {
            GameObject mouth = new GameObject("Mouth", typeof(RectTransform), typeof(Image));
            mouth.transform.SetParent(container.transform, false);
            RectTransform rt = mouth.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, 22f);
            rt.sizeDelta = new Vector2(16f, 10f);
            Image img = mouth.GetComponent<Image>();
            img.sprite = skin.mouth;
            img.preserveAspect = true;
        }

        // 5. Front Layer: Left Arm (Front Arm, ON TOP of body torso)
        if (skin.leftArm != null)
        {
            GameObject lArm = new GameObject("LeftArm", typeof(RectTransform), typeof(Image));
            lArm.transform.SetParent(container.transform, false);
            RectTransform rt = lArm.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-28f, -6f);
            rt.sizeDelta = new Vector2(20f, 38f);
            Image img = lArm.GetComponent<Image>();
            img.sprite = skin.leftArm;
            img.preserveAspect = true;
        }

        return container;
    }

    private void ShowPlayerDetailsView(string uid, string displayName, int level, int skinIndex, int coins, FriendsTabType originTab)
    {
        if (friendsScrollViewContent == null) return;
        ClearScrollViewContent();

        GameObject detailCard = new GameObject("PlayerDetailsView", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        detailCard.transform.SetParent(friendsScrollViewContent, false);

        RectTransform dRt = detailCard.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.5f, 0.5f);
        dRt.anchorMax = new Vector2(0.5f, 0.5f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.sizeDelta = new Vector2(460f, 280f);

        LayoutElement le = detailCard.GetComponent<LayoutElement>();
        le.preferredWidth = 460f;
        le.preferredHeight = 280f;
        le.minHeight = 240f;
        le.flexibleWidth = 1f;

        Image bg = detailCard.GetComponent<Image>();
        Sprite cardSprite = GetCardFrameSprite();
        if (cardSprite != null)
        {
            bg.sprite = cardSprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = new Color(0.09f, 0.13f, 0.20f, 0.98f);

        VerticalLayoutGroup vlg = detailCard.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(16, 16, 14, 14);
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        // 1. Top Bar with Back Button & Header
        GameObject headerGO = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        headerGO.transform.SetParent(detailCard.transform, false);

        HorizontalLayoutGroup hHlg = headerGO.GetComponent<HorizontalLayoutGroup>();
        hHlg.childAlignment = TextAnchor.MiddleLeft;
        hHlg.spacing = 10f;
        hHlg.childControlWidth = false;
        hHlg.childControlHeight = false;

        GameObject backBtnGO = new GameObject("BackBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        backBtnGO.transform.SetParent(headerGO.transform, false);
        RectTransform bRt = backBtnGO.GetComponent<RectTransform>();
        bRt.sizeDelta = new Vector2(90f, 32f);

        Image bBg = backBtnGO.GetComponent<Image>();
        Sprite backSprite = GetButtonFrameSprite(isBackButton: true);
        if (backSprite != null)
        {
            bBg.sprite = backSprite;
            bBg.type = Image.Type.Sliced;
            bBg.color = Color.white;
        }
        else
        {
            bBg.color = new Color(0.22f, 0.32f, 0.46f, 1f);
        }

        GameObject bTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        bTxtGO.transform.SetParent(backBtnGO.transform, false);
        RectTransform btRt = bTxtGO.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;

        TextMeshProUGUI bTmp = bTxtGO.GetComponent<TextMeshProUGUI>();
        bTmp.text = "BACK";
        bTmp.fontSize = 13;
        bTmp.fontStyle = FontStyles.Bold;
        bTmp.alignment = TextAlignmentOptions.Center;
        bTmp.color = Color.white;

        Button backBtn = backBtnGO.GetComponent<Button>();
        backBtn.onClick.AddListener(() =>
        {
            PlayButtonClickSFX();
            RefreshFriendsTabContent(forceApi: false);
        });

        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(headerGO.transform, false);
        RectTransform tRt = titleGO.GetComponent<RectTransform>();
        tRt.sizeDelta = new Vector2(300f, 32f);

        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "PLAYER DETAILS";
        tTmp.fontSize = 16;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.color = new Color(0.4f, 0.85f, 1f, 1f);

        // 2. Profile Info Section (Full Character Skin + Name & Stats)
        GameObject infoRow = new GameObject("InfoRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        infoRow.transform.SetParent(detailCard.transform, false);

        HorizontalLayoutGroup iHlg = infoRow.GetComponent<HorizontalLayoutGroup>();
        iHlg.childAlignment = TextAnchor.MiddleCenter;
        iHlg.spacing = 20f;
        iHlg.childControlWidth = false;
        iHlg.childControlHeight = false;

        // Render whole character skin figure
        CreateFullSkinPreviewUI(skinIndex, infoRow.transform);

        GameObject detailsCol = new GameObject("DetailsCol", typeof(RectTransform), typeof(VerticalLayoutGroup));
        detailsCol.transform.SetParent(infoRow.transform, false);

        VerticalLayoutGroup dVlg = detailsCol.GetComponent<VerticalLayoutGroup>();
        dVlg.childAlignment = TextAnchor.MiddleLeft;
        dVlg.spacing = 4f;
        dVlg.childControlWidth = false;
        dVlg.childControlHeight = false;

        CounterBoom.Networking.FirebaseManager.PlayerPresenceData pData = null;
        if (cachedPresences != null)
        {
            if (!cachedPresences.TryGetValue(uid, out pData) && !string.IsNullOrEmpty(displayName))
            {
                cachedPresences.TryGetValue(displayName, out pData);
            }
        }

        var status = pData != null ? pData.GetStatusEnum() : CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.Offline;
        bool friendInRoom = IsFriendInCurrentRoom(uid, displayName);

        string statusSuffix = "";
        string inviteBtnText = "INVITE";
        bool inviteBtnInteractable = true;
        Color inviteBtnColor = new Color(0.9f, 0.6f, 0.15f, 1f);
        bool isRequestToJoin = false;

        if (friendInRoom)
        {
            statusSuffix = "<color=#00E676>(In Room)</color>";
            inviteBtnText = "IN ROOM";
            inviteBtnInteractable = false;
            inviteBtnColor = new Color(0.2f, 0.65f, 0.32f, 1f);
        }
        else
        {
            switch (status)
            {
                case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.Online:
                    statusSuffix = "<color=#00E676>(Online)</color>";
                    inviteBtnText = "INVITE";
                    inviteBtnInteractable = true;
                    inviteBtnColor = new Color(0.9f, 0.6f, 0.15f, 1f);
                    break;

                case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.InRoom:
                    statusSuffix = "<color=#FFD54F>(In Room)</color>";
                    inviteBtnText = "REQUEST";
                    inviteBtnInteractable = true;
                    inviteBtnColor = new Color(0.2f, 0.65f, 0.9f, 1f);
                    isRequestToJoin = true;
                    break;

                case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.RoomFull:
                    statusSuffix = "<color=#FF5252>(Room Full)</color>";
                    inviteBtnText = "ROOM FULL";
                    inviteBtnInteractable = false;
                    inviteBtnColor = new Color(0.4f, 0.45f, 0.55f, 1f);
                    break;

                case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.InGame:
                    statusSuffix = "<color=#40C4FF>(In Game)</color>";
                    inviteBtnText = "IN GAME";
                    inviteBtnInteractable = false;
                    inviteBtnColor = new Color(0.4f, 0.45f, 0.55f, 1f);
                    break;

                default: // Offline
                    statusSuffix = "<color=#888888>(Offline)</color>";
                    inviteBtnText = "OFFLINE";
                    inviteBtnInteractable = false;
                    inviteBtnColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                    break;
            }
        }

        GameObject nameGO = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(detailsCol.transform, false);
        TextMeshProUGUI nTmp = nameGO.GetComponent<TextMeshProUGUI>();
        string titleText = string.IsNullOrEmpty(statusSuffix) ? displayName : $"{displayName} {statusSuffix}";
        nTmp.text = titleText;
        nTmp.fontSize = 20;
        nTmp.fontStyle = FontStyles.Bold;
        nTmp.color = Color.white;

        GameObject statsGO = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statsGO.transform.SetParent(detailsCol.transform, false);
        TextMeshProUGUI sTmp = statsGO.GetComponent<TextMeshProUGUI>();
        sTmp.text = $"Level {level}   |   {coins} Coins";
        sTmp.fontSize = 13;
        sTmp.color = new Color(0.95f, 0.82f, 0.3f, 1f);

        GameObject uidGO = new GameObject("UidText", typeof(RectTransform), typeof(TextMeshProUGUI));
        uidGO.transform.SetParent(detailsCol.transform, false);
        TextMeshProUGUI uTmp = uidGO.GetComponent<TextMeshProUGUI>();
        uTmp.text = $"ID: {uid}";
        uTmp.fontSize = 12;
        uTmp.color = new Color(0.65f, 0.75f, 0.9f, 0.9f);

        // 3. Action Buttons Section
        GameObject actionsRow = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actionsRow.transform.SetParent(detailCard.transform, false);

        HorizontalLayoutGroup actHlg = actionsRow.GetComponent<HorizontalLayoutGroup>();
        actHlg.childAlignment = TextAnchor.MiddleCenter;
        actHlg.spacing = 14f;
        actHlg.childControlWidth = false;
        actHlg.childControlHeight = false;

        bool isFriend = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsFriend(uid);
        bool isPending = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsRequestPending(uid);

        if (isFriend)
        {
            GameObject inviteBtnGO = CreateActionButton(actionsRow.transform, inviteBtnText, inviteBtnColor, null, enabled: inviteBtnInteractable);
            Button inviteBtn = inviteBtnGO.GetComponent<Button>();
            if (inviteBtnInteractable)
            {
                inviteBtn.onClick.AddListener(async () =>
                {
                    PlayButtonClickSFX();
                    if (isRequestToJoin)
                    {
                        await SendJoinRequestToFriendAsync(uid, displayName, inviteBtn);
                    }
                    else
                    {
                        await SendInviteToFriendAsync(uid, displayName, inviteBtn);
                    }
                });
            }

            CreateActionButton(actionsRow.transform, "REMOVE FRIEND", new Color(0.75f, 0.2f, 0.2f, 1f), () =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.RemoveFriend(uid, (success) =>
                    {
                        if (success)
                        {
                            if (cachedFriendsList != null) cachedFriendsList.RemoveAll(f => f.uid == uid);
                            RefreshFriendsTabContent(forceApi: false);
                        }
                    });
                }
            });
        }
        else if (isPending)
        {
            CreateActionButton(actionsRow.transform, "REQUESTED", new Color(0.5f, 0.5f, 0.5f, 1f), null, enabled: false);
        }
        else
        {
            GameObject addBtnGO = CreateActionButton(actionsRow.transform, "+ ADD FRIEND", new Color(0.12f, 0.52f, 0.88f, 1f), null);
            Button addBtn = addBtnGO.GetComponent<Button>();
            addBtn.onClick.AddListener(() =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.SendFriendRequest(uid);
                }
                var tmpText = addBtnGO.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = "REQUESTED";
                addBtn.interactable = false;
            });
        }

        // 4. Populate Friend List of THIS Player below the details card
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.FetchTargetUserFriendsList(uid, (friendsList) =>
            {
                if (friendsScrollViewContent == null) return;

                if (friendsList != null && friendsList.Count > 0)
                {
                    foreach (var friend in friendsList)
                    {
                        CreatePlayerRowItem(
                            friendsScrollViewContent,
                            friend.uid,
                            friend.displayName,
                            friend.level,
                            friend.selectedSkinIndex,
                            1000,
                            originTab
                        );
                    }
                }
            });
        }
    }

    private GameObject activeInviteModal;

    private void PollGameInvitesTask()
    {
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.PollGameInvite((invite) =>
            {
                if (invite != null && !string.IsNullOrEmpty(invite.roomCode))
                {
                    ShowGameInviteNotificationModal(invite.senderName, invite.roomCode);
                }
            });
        }
    }

    public void ShowGameInviteNotificationModal(string senderName, string roomCode)
    {
        if (activeInviteModal != null) Destroy(activeInviteModal);

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        activeInviteModal = new GameObject("GameInviteModal", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        activeInviteModal.transform.SetParent(canvas.transform, false);

        RectTransform mRt = activeInviteModal.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0.5f);
        mRt.anchorMax = new Vector2(0.5f, 0.5f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.sizeDelta = new Vector2(400f, 210f);

        Image bg = activeInviteModal.GetComponent<Image>();
        Sprite cardSprite = invitePopupPanelSprite != null ? invitePopupPanelSprite : GetCardFrameSprite();
        if (cardSprite != null)
        {
            bg.sprite = cardSprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = invitePopupPanelSprite != null ? Color.white : new Color(0.08f, 0.12f, 0.18f, 0.98f);

        VerticalLayoutGroup vlg = activeInviteModal.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(20, 20, 16, 16);
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        GameObject titleGO = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(activeInviteModal.transform, false);
        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "LOBBY INVITATION";
        tTmp.fontSize = 18;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.color = new Color(0.4f, 0.85f, 1f, 1f);

        GameObject msgGO = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgGO.transform.SetParent(activeInviteModal.transform, false);
        TextMeshProUGUI mTmp = msgGO.GetComponent<TextMeshProUGUI>();
        mTmp.text = $"<b>{senderName}</b> invited you to join their lobby!";
        mTmp.fontSize = 14;
        mTmp.alignment = TextAlignmentOptions.Center;
        mTmp.color = Color.white;

        GameObject btnRow = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(activeInviteModal.transform, false);
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 16f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        CreateActionButton(btnRow.transform, "ACCEPT", new Color(0.18f, 0.65f, 0.3f, 1f), () =>
        {
            AcceptGameInvite(senderName, roomCode);
        });

        CreateActionButton(btnRow.transform, "DECLINE", new Color(0.75f, 0.2f, 0.2f, 1f), () =>
        {
            PlayButtonClickSFX();
            if (activeInviteModal != null) Destroy(activeInviteModal);
        });
    }

    public async void AcceptGameInvite(string senderName, string roomCode)
    {
        PlayButtonClickSFX();

        if (activeInviteModal != null) Destroy(activeInviteModal);

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Create Joining Lobby Loading Modal
        GameObject loadingModal = new GameObject("JoiningLobbyModal", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        loadingModal.transform.SetParent(canvas.transform, false);

        RectTransform mRt = loadingModal.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0.5f);
        mRt.anchorMax = new Vector2(0.5f, 0.5f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.sizeDelta = new Vector2(420f, 220f);

        Image bg = loadingModal.GetComponent<Image>();
        Sprite cardSprite = invitePopupPanelSprite != null ? invitePopupPanelSprite : GetCardFrameSprite();
        if (cardSprite != null)
        {
            bg.sprite = cardSprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = invitePopupPanelSprite != null ? Color.white : new Color(0.06f, 0.1f, 0.16f, 0.98f);

        VerticalLayoutGroup vlg = loadingModal.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        // Title
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(loadingModal.transform, false);
        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "JOINING LOBBY";
        tTmp.fontSize = 18;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Status Text
        GameObject statusGO = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusGO.transform.SetParent(loadingModal.transform, false);
        TextMeshProUGUI sTmp = statusGO.GetComponent<TextMeshProUGUI>();
        sTmp.text = $"Connecting to <b>{senderName}</b>'s room...\nPlease wait";
        sTmp.fontSize = 14;
        sTmp.alignment = TextAlignmentOptions.Center;
        sTmp.color = Color.white;

        // Animated dots indicator
        GameObject dotsGO = new GameObject("DotsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        dotsGO.transform.SetParent(loadingModal.transform, false);
        TextMeshProUGUI dTmp = dotsGO.GetComponent<TextMeshProUGUI>();
        dTmp.text = "<color=#00FFFF>• • • • •</color>";
        dTmp.fontSize = 16;
        dTmp.alignment = TextAlignmentOptions.Center;

        UpdatePlayStatus($"Joining {senderName}'s lobby...");

        // Cleanly dismantle existing room instance if we were hosting or connected to one
        if (RelayNetworkManager.Instance != null)
        {
            await RelayNetworkManager.Instance.LeaveMatchGracefully();
        }
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            await System.Threading.Tasks.Task.Delay(150);
        }

        var priorPCs = FindObjectsOfType<PlayerController>();
        foreach (var pc in priorPCs)
        {
            if (pc != null && pc.gameObject != null) Destroy(pc.gameObject);
        }

        bool joined = false;
        if (RelayNetworkManager.Instance != null && !string.IsNullOrEmpty(roomCode))
        {
            joined = await RelayNetworkManager.Instance.StartClientWithRelay(roomCode);
        }

        if (joined)
        {
            if (sTmp != null) sTmp.text = $"<color=#00FF00>Connected! Spawning into {senderName}'s room...</color>";
            if (dTmp != null) dTmp.text = "<color=#00FF00>[OK]</color>";
            UpdatePlayStatus($"Joined {senderName}'s lobby!");
            await System.Threading.Tasks.Task.Delay(800);
            if (loadingModal != null) Destroy(loadingModal);
        }
        else
        {
            SetupPreviewPlayer();
            ResetPreviewToEquippedSkin();
            UpdateLobbyButtonsState();

            if (sTmp != null) sTmp.text = "<color=#FF4444>Failed to connect to lobby.\nRoom may be full or expired.</color>";
            if (dTmp != null) dTmp.text = "";
            UpdatePlayStatus("Failed to join lobby.");

            GameObject btnRow = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnRow.transform.SetParent(loadingModal.transform, false);
            HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;

            CreateActionButton(btnRow.transform, "CLOSE", new Color(0.75f, 0.2f, 0.2f, 1f), () =>
            {
                PlayButtonClickSFX();
                if (loadingModal != null) Destroy(loadingModal);
            });
        }
    }

    private HashSet<string> activeInviteProcessingUids = new HashSet<string>();

    public async Task SendInviteToFriendAsync(string targetUid, string targetDisplayName, Button inviteBtn = null)
    {
        if (string.IsNullOrEmpty(targetUid)) return;

        if (activeInviteProcessingUids.Contains(targetUid))
        {
            Debug.LogWarning($"[MainMenuController] Invite process already running for target '{targetDisplayName}' ({targetUid})");
            return;
        }

        activeInviteProcessingUids.Add(targetUid);

        TMP_Text btnText = inviteBtn != null ? inviteBtn.GetComponentInChildren<TMP_Text>() : null;
        string originalText = btnText != null ? btnText.text : "";
        bool originalInteractable = inviteBtn != null ? inviteBtn.interactable : true;

        try
        {
            if (inviteBtn != null)
            {
                inviteBtn.interactable = false;
                if (btnText != null) btnText.text = "INVITING...";
            }
            UpdatePlayStatus($"Sending invite to {targetDisplayName}...");

            string roomCode = RelayNetworkManager.Instance != null ? RelayNetworkManager.Instance.CurrentJoinCode : "";
            if (string.IsNullOrEmpty(roomCode) && RelayNetworkManager.Instance != null)
            {
                roomCode = await RelayNetworkManager.Instance.StartPrivateHostWithRelay();
            }

            if (string.IsNullOrEmpty(roomCode))
            {
                Debug.LogError("[MainMenuController] Failed to get valid room code for game invite!");
                UpdatePlayStatus("<color=#FF4444>Failed to create room!</color>");
                if (inviteBtn != null && btnText != null) btnText.text = "FAILED";
                await Task.Delay(1500);
                return;
            }

            bool success = false;
            if (CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                var tcs = new TaskCompletionSource<bool>();
                CounterBoom.Networking.FirebaseManager.Instance.SendGameInvite(targetUid, roomCode, (res) => tcs.TrySetResult(res));

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(6000));
                if (completedTask == tcs.Task)
                {
                    success = await tcs.Task;
                }
            }

            if (success)
            {
                Debug.Log($"[MainMenuController] Invite sent successfully to '{targetDisplayName}' ({targetUid}) with room code '{roomCode}'");
                UpdatePlayStatus($"<color=#00FF00>Invite sent to {targetDisplayName}!</color>");
                if (inviteBtn != null && btnText != null) btnText.text = "SENT";
            }
            else
            {
                Debug.LogWarning($"[MainMenuController] Invite request completed for '{targetDisplayName}'");
                UpdatePlayStatus($"Invited {targetDisplayName} to room!");
                if (inviteBtn != null && btnText != null) btnText.text = "SENT";
            }

            await Task.Delay(2000);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MainMenuController] Exception in SendInviteToFriendAsync: {ex}");
        }
        finally
        {
            activeInviteProcessingUids.Remove(targetUid);
            if (inviteBtn != null)
            {
                inviteBtn.interactable = originalInteractable;
                if (btnText != null) btnText.text = originalText;
            }
        }
    }

    public async Task SendJoinRequestToFriendAsync(string targetUid, string targetDisplayName, Button requestBtn = null)
    {
        if (string.IsNullOrEmpty(targetUid)) return;

        TMP_Text btnText = requestBtn != null ? requestBtn.GetComponentInChildren<TMP_Text>() : null;
        string originalText = btnText != null ? btnText.text : "REQUEST";

        if (requestBtn != null)
        {
            requestBtn.interactable = false;
            if (btnText != null) btnText.text = "REQUESTING...";
        }

        UpdatePlayStatus($"Sending join request to {targetDisplayName}...");

        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            var tcs = new TaskCompletionSource<bool>();
            CounterBoom.Networking.FirebaseManager.Instance.SendJoinRoomRequest(targetUid, targetDisplayName, (res) => tcs.TrySetResult(res));
            await Task.WhenAny(tcs.Task, Task.Delay(4000));
        }

        UpdatePlayStatus($"<color=#00FF00>Join request sent to {targetDisplayName}!</color>");
        if (requestBtn != null && btnText != null)
        {
            btnText.text = "REQUESTED";
        }
    }

    private void PollJoinRoomRequestsTask()
    {
        if (CounterBoom.Networking.FirebaseManager.Instance != null)
        {
            CounterBoom.Networking.FirebaseManager.Instance.PollJoinRoomRequest((req) =>
            {
                if (req != null && !string.IsNullOrEmpty(req.senderUid) && !string.IsNullOrEmpty(req.senderName))
                {
                    ShowJoinRoomRequestModal(req.senderUid, req.senderName);
                }
            });
        }
    }

    public void ShowJoinRoomRequestModal(string senderUid, string senderName)
    {
        if (activeJoinRequestModal != null) Destroy(activeJoinRequestModal);

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        activeJoinRequestModal = new GameObject("JoinRoomRequestModal", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        activeJoinRequestModal.transform.SetParent(canvas.transform, false);

        RectTransform mRt = activeJoinRequestModal.GetComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0.5f, 0.5f);
        mRt.anchorMax = new Vector2(0.5f, 0.5f);
        mRt.pivot = new Vector2(0.5f, 0.5f);
        mRt.sizeDelta = new Vector2(400f, 210f);

        Image bg = activeJoinRequestModal.GetComponent<Image>();
        Sprite cardSprite = invitePopupPanelSprite != null ? invitePopupPanelSprite : GetCardFrameSprite();
        if (cardSprite != null)
        {
            bg.sprite = cardSprite;
            bg.type = Image.Type.Sliced;
        }
        bg.color = invitePopupPanelSprite != null ? Color.white : new Color(0.08f, 0.12f, 0.18f, 0.98f);

        VerticalLayoutGroup vlg = activeJoinRequestModal.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(20, 20, 16, 16);
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;

        GameObject titleGO = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(activeJoinRequestModal.transform, false);
        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "ROOM JOIN REQUEST";
        tTmp.fontSize = 18;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.color = new Color(0.4f, 0.85f, 1f, 1f);

        GameObject msgGO = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgGO.transform.SetParent(activeJoinRequestModal.transform, false);
        TextMeshProUGUI mTmp = msgGO.GetComponent<TextMeshProUGUI>();
        mTmp.text = $"<b>{senderName}</b> wants to join your room!";
        mTmp.fontSize = 14;
        mTmp.alignment = TextAlignmentOptions.Center;
        mTmp.color = Color.white;

        GameObject btnRow = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(activeJoinRequestModal.transform, false);
        HorizontalLayoutGroup hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 16f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        CreateActionButton(btnRow.transform, "LET IN", new Color(0.18f, 0.65f, 0.3f, 1f), async () =>
        {
            PlayButtonClickSFX();
            if (activeJoinRequestModal != null) Destroy(activeJoinRequestModal);

            if (CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.CurrentUser != null)
            {
                CounterBoom.Networking.FirebaseManager.Instance.ClearJoinRoomRequest(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.uid);
                if (!string.IsNullOrEmpty(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.displayName))
                {
                    CounterBoom.Networking.FirebaseManager.Instance.ClearJoinRoomRequest(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.displayName);
                }
            }

            // 1. Retrieve active room code from RelayNetworkManager
            string roomCode = RelayNetworkManager.Instance != null ? RelayNetworkManager.Instance.CurrentJoinCode : "";
            if (string.IsNullOrEmpty(roomCode) && RelayNetworkManager.Instance != null)
            {
                roomCode = RelayNetworkManager.Instance.LastValidJoinCode;
            }

            // 2. Fallback to UI input field
            if (string.IsNullOrEmpty(roomCode) && joinCodeInputField != null && !string.IsNullOrEmpty(joinCodeInputField.text))
            {
                roomCode = joinCodeInputField.text.Trim().ToUpper();
            }

            bool isCurrentlyInRoom = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;

            // 3. Only create a new private room host if NOT currently in a room!
            if (string.IsNullOrEmpty(roomCode) && !isCurrentlyInRoom && RelayNetworkManager.Instance != null)
            {
                UpdatePlayStatus("Creating room to let player in...");
                roomCode = await RelayNetworkManager.Instance.StartPrivateHostWithRelay();
            }

            if (!string.IsNullOrEmpty(roomCode) && CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                CounterBoom.Networking.FirebaseManager.Instance.SendGameInvite(senderUid, roomCode, senderName);
                UpdatePlayStatus($"<color=green>Let in {senderName}! Sent invite to join room.</color>");
                Debug.Log($"[MainMenuController] Let in '{senderName}' ({senderUid}) with room code '{roomCode}'.");
            }
            else
            {
                Debug.LogWarning($"[MainMenuController] Could not let in '{senderName}': Room code missing.");
                UpdatePlayStatus("<color=red>Could not resolve room code to let player in!</color>");
            }
        });

        CreateActionButton(btnRow.transform, "DECLINE", new Color(0.75f, 0.2f, 0.2f, 1f), () =>
        {
            PlayButtonClickSFX();
            if (activeJoinRequestModal != null) Destroy(activeJoinRequestModal);
            if (CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.CurrentUser != null)
            {
                CounterBoom.Networking.FirebaseManager.Instance.ClearJoinRoomRequest(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.uid);
                if (!string.IsNullOrEmpty(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.displayName))
                {
                    CounterBoom.Networking.FirebaseManager.Instance.ClearJoinRoomRequest(CounterBoom.Networking.FirebaseManager.Instance.CurrentUser.displayName);
                }
            }
        });
    }

    private GameObject CreateActionButton(Transform parent, string label, Color bgCol, Action onClick, bool enabled = true)
    {
        GameObject btnGO = new GameObject("ActionBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 38f);

        Image img = btnGO.GetComponent<Image>();
        Sprite btnSprite = GetButtonFrameSprite(isBackButton: false);
        if (btnSprite != null)
        {
            img.sprite = btnSprite;
            img.type = Image.Type.Sliced;
            img.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.6f);
        }
        else
        {
            img.color = enabled ? bgCol : new Color(bgCol.r * 0.4f, bgCol.g * 0.4f, bgCol.b * 0.4f, 0.6f);
        }

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        Button btn = btnGO.GetComponent<Button>();
        btn.interactable = enabled;
        if (enabled && onClick != null)
        {
            btn.onClick.AddListener(() => onClick.Invoke());
        }
        return btnGO;
    }

    private void CreatePlayerRowItem(Transform parent, string uid, string displayName, int level, int skinIndex, int coins, FriendsTabType tabType)
    {
        Sprite avatarSprite = GetSkinHeadSprite(skinIndex);
        string statusSuffix = "";
        string inviteBtnText = "INVITE";
        bool inviteBtnInteractable = true;
        Color inviteBtnColor = new Color(0.9f, 0.6f, 0.15f, 1f);
        bool isRequestToJoin = false;

        if (tabType == FriendsTabType.Friends)
        {
            bool friendInRoom = IsFriendInCurrentRoom(uid, displayName);
            CounterBoom.Networking.FirebaseManager.PlayerPresenceData pData = null;
            if (cachedPresences != null)
            {
                if (!cachedPresences.TryGetValue(uid, out pData) && !string.IsNullOrEmpty(displayName))
                {
                    cachedPresences.TryGetValue(displayName, out pData);
                }
            }

            var status = pData != null ? pData.GetStatusEnum() : CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.Offline;

            if (friendInRoom)
            {
                statusSuffix = "<color=#00E676>(In Room)</color>";
                inviteBtnText = "IN ROOM";
                inviteBtnInteractable = false;
                inviteBtnColor = new Color(0.2f, 0.65f, 0.32f, 1f);
            }
            else
            {
                switch (status)
                {
                    case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.Online:
                        statusSuffix = "<color=#00E676>(Online)</color>";
                        inviteBtnText = "INVITE";
                        inviteBtnInteractable = true;
                        inviteBtnColor = new Color(0.9f, 0.6f, 0.15f, 1f);
                        break;

                    case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.InRoom:
                        statusSuffix = "<color=#FFD54F>(In Room)</color>";
                        inviteBtnText = "REQUEST";
                        inviteBtnInteractable = true;
                        inviteBtnColor = new Color(0.2f, 0.65f, 0.9f, 1f);
                        isRequestToJoin = true;
                        break;

                    case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.RoomFull:
                        statusSuffix = "<color=#FF5252>(Room Full)</color>";
                        inviteBtnText = "ROOM FULL";
                        inviteBtnInteractable = false;
                        inviteBtnColor = new Color(0.4f, 0.45f, 0.55f, 1f);
                        break;

                    case CounterBoom.Networking.FirebaseManager.PlayerPresenceStatus.InGame:
                        statusSuffix = "<color=#40C4FF>(In Game)</color>";
                        inviteBtnText = "IN GAME";
                        inviteBtnInteractable = false;
                        inviteBtnColor = new Color(0.4f, 0.45f, 0.55f, 1f);
                        break;

                    default: // Offline
                        statusSuffix = "<color=#888888>(Offline)</color>";
                        inviteBtnText = "OFFLINE";
                        inviteBtnInteractable = false;
                        inviteBtnColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                        break;
                }
            }
        }

        if (playerRowPrefab != null)
        {
            GameObject instantiatedRow = Instantiate(playerRowPrefab, parent, false);

            var le = instantiatedRow.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = instantiatedRow.AddComponent<LayoutElement>();
            }
            le.preferredWidth = 460f;
            le.preferredHeight = 55f;
            le.minHeight = 50f;
            le.flexibleWidth = 1f;

            var rowUI = instantiatedRow.GetComponent<CounterBoom.UI.PlayerRowItemUI>();
            if (rowUI == null)
            {
                rowUI = instantiatedRow.AddComponent<CounterBoom.UI.PlayerRowItemUI>();
            }

            bool isFriend = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsFriend(uid);
            bool isPending = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsRequestPending(uid);

            CounterBoom.UI.PlayerRowItemUI.RowMode mode = (CounterBoom.UI.PlayerRowItemUI.RowMode)(int)tabType;

            rowUI.SetupRow(
                uid,
                displayName,
                level,
                avatarSprite,
                mode,
                isFriend,
                isPending,
                onAddClicked: () =>
                {
                    if (CounterBoom.Networking.FirebaseManager.Instance != null)
                    {
                        CounterBoom.Networking.FirebaseManager.Instance.SendFriendRequest(uid);
                    }
                },
                onAcceptClicked: () =>
                {
                    if (CounterBoom.Networking.FirebaseManager.Instance != null)
                    {
                        CounterBoom.Networking.FirebaseManager.Instance.AcceptFriendRequest(uid, (success) =>
                        {
                            if (success)
                            {
                                if (cachedRequestsList != null) cachedRequestsList.RemoveAll(r => r.uid == uid);
                                lastFriendsFetchTime = -999f; // Invalidate friends tab so next time it is visited, it fetches the new friend
                                ApplyLocalFriendsFilter();
                            }
                        });
                    }
                },
                onDeclineClicked: () =>
                {
                    if (CounterBoom.Networking.FirebaseManager.Instance != null)
                    {
                        CounterBoom.Networking.FirebaseManager.Instance.DeclineFriendRequest(uid, (success) =>
                        {
                            if (success)
                            {
                                if (cachedRequestsList != null) cachedRequestsList.RemoveAll(r => r.uid == uid);
                                ApplyLocalFriendsFilter();
                            }
                        });
                    }
                },
                onInviteClicked: async (btn) =>
                {
                    if (isRequestToJoin)
                    {
                        await SendJoinRequestToFriendAsync(uid, displayName, btn);
                    }
                    else
                    {
                        await SendInviteToFriendAsync(uid, displayName, btn);
                    }
                },
                onRemoveClicked: () =>
                {
                    if (CounterBoom.Networking.FirebaseManager.Instance != null)
                    {
                        CounterBoom.Networking.FirebaseManager.Instance.RemoveFriend(uid, (success) =>
                        {
                            if (success)
                            {
                                if (cachedFriendsList != null) cachedFriendsList.RemoveAll(f => f.uid == uid);
                                ApplyLocalFriendsFilter();
                            }
                        });
                    }
                },
                onRowClicked: () =>
                {
                    ShowPlayerDetailsView(uid, displayName, level, skinIndex, coins, tabType);
                },
                statusSuffix: statusSuffix,
                inviteButtonText: inviteBtnText,
                inviteButtonInteractable: inviteBtnInteractable,
                inviteButtonColor: inviteBtnColor
            );
            return;
        }

        GameObject row = new GameObject($"PlayerRow_{displayName}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);

        RectTransform rRt = row.GetComponent<RectTransform>();
        rRt.sizeDelta = new Vector2(460f, 62f);

        Image rBg = row.GetComponent<Image>();
        rBg.color = new Color(0.14f, 0.19f, 0.27f, 0.95f);

        // 1. Profile Avatar Frame & Icon
        GameObject frameGO = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image));
        frameGO.transform.SetParent(row.transform, false);

        RectTransform fRt = frameGO.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0.5f); fRt.anchorMax = new Vector2(0f, 0.5f); fRt.pivot = new Vector2(0f, 0.5f);
        fRt.anchoredPosition = new Vector2(8f, 0f); fRt.sizeDelta = new Vector2(46f, 46f);

        Image fBg = frameGO.GetComponent<Image>();
        fBg.color = new Color(0.25f, 0.35f, 0.5f, 1f);

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(frameGO.transform, false);

        RectTransform iRt = iconGO.GetComponent<RectTransform>();
        iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
        iRt.offsetMin = new Vector2(2f, 2f); iRt.offsetMax = new Vector2(-2f, -2f);

        Image iconImg = iconGO.GetComponent<Image>();
        if (avatarSprite != null)
        {
            iconImg.sprite = avatarSprite;
        }

        // 2. Name & Level/UID Text
        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(row.transform, false);

        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f); tRt.anchorMax = new Vector2(0.52f, 1f);
        tRt.offsetMin = new Vector2(62f, 0f); tRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        string titleText = string.IsNullOrEmpty(statusSuffix) ? displayName : $"{displayName} {statusSuffix}";
        tmp.text = $"<b>{titleText}</b>\n<size=12><color=#fbbc05>Level {level}</color> | ID: {uid}</size>";
        tmp.fontSize = 15; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.color = Color.white;
        tmp.raycastTarget = false;

        // 3. Action Buttons Row (Right Side)
        GameObject actionRow = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        actionRow.transform.SetParent(row.transform, false);

        RectTransform aRt = actionRow.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(1f, 0.5f); aRt.anchorMax = new Vector2(1f, 0.5f); aRt.pivot = new Vector2(1f, 0.5f);
        aRt.anchoredPosition = new Vector2(-10f, 0f); aRt.sizeDelta = new Vector2(210f, 36f);

        HorizontalLayoutGroup aHlg = actionRow.GetComponent<HorizontalLayoutGroup>();
        aHlg.spacing = 6f;
        aHlg.childAlignment = TextAnchor.MiddleRight;
        aHlg.childControlWidth = false;
        aHlg.childControlHeight = false;
        aHlg.childForceExpandWidth = false;
        aHlg.childForceExpandHeight = false;

        if (tabType == FriendsTabType.Search)
        {
            bool isFriend = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsFriend(uid);
            bool isPending = CounterBoom.Networking.FirebaseManager.Instance != null && CounterBoom.Networking.FirebaseManager.Instance.IsRequestPending(uid);

            GameObject addBtnGO = CreateButton(actionRow, isFriend ? "FRIENDS" : (isPending ? "REQUESTED" : "+ ADD FRIEND"),
                isFriend || isPending ? new Color(0.3f, 0.4f, 0.5f, 1f) : new Color(0.2f, 0.65f, 0.32f, 1f), 150f);

            Button addBtn = addBtnGO.GetComponent<Button>();
            addBtn.interactable = !isFriend && !isPending;
            addBtn.onClick.AddListener(() =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.SendFriendRequest(uid);
                }
                var tmpText = addBtnGO.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = "REQUESTED";
                addBtn.interactable = false;
            });
        }
        else if (tabType == FriendsTabType.Requests)
        {
            // Accept Button
            GameObject acceptBtnGO = CreateButton(actionRow, "ACCEPT", new Color(0.2f, 0.65f, 0.32f, 1f), 95f);
            Button acceptBtn = acceptBtnGO.GetComponent<Button>();
            acceptBtn.onClick.AddListener(() =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.AcceptFriendRequest(uid, (success) =>
                    {
                        if (success)
                        {
                            if (cachedRequestsList != null) cachedRequestsList.RemoveAll(r => r.uid == uid);
                            lastFriendsFetchTime = -999f;
                            Destroy(row);
                        }
                    });
                }
            });

            // Decline Button
            GameObject declineBtnGO = CreateButton(actionRow, "DECLINE", new Color(0.85f, 0.25f, 0.25f, 1f), 95f);
            Button declineBtn = declineBtnGO.GetComponent<Button>();
            declineBtn.onClick.AddListener(() =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.DeclineFriendRequest(uid, (success) =>
                    {
                        if (success)
                        {
                            if (cachedRequestsList != null) cachedRequestsList.RemoveAll(r => r.uid == uid);
                            Destroy(row);
                        }
                    });
                }
            });
        }
        else if (tabType == FriendsTabType.Friends)
        {
            // Invite to Room / Request Join Button
            GameObject inviteBtnGO = CreateButton(actionRow, inviteBtnText, inviteBtnColor, 95f);
            Button inviteBtn = inviteBtnGO.GetComponent<Button>();
            inviteBtn.interactable = inviteBtnInteractable;
            if (inviteBtnInteractable)
            {
                inviteBtn.onClick.AddListener(async () =>
                {
                    PlayButtonClickSFX();
                    if (isRequestToJoin)
                    {
                        await SendJoinRequestToFriendAsync(uid, displayName, inviteBtn);
                    }
                    else
                    {
                        await SendInviteToFriendAsync(uid, displayName, inviteBtn);
                    }
                });
            }

            // Remove Friend Button
            GameObject removeBtnGO = CreateButton(actionRow, "REMOVE", new Color(0.75f, 0.2f, 0.2f, 1f), 95f);
            Button removeBtn = removeBtnGO.GetComponent<Button>();
            removeBtn.onClick.AddListener(() =>
            {
                PlayButtonClickSFX();
                if (CounterBoom.Networking.FirebaseManager.Instance != null)
                {
                    CounterBoom.Networking.FirebaseManager.Instance.RemoveFriend(uid, (success) =>
                    {
                        if (success)
                        {
                            if (cachedFriendsList != null) cachedFriendsList.RemoveAll(f => f.uid == uid);
                            Destroy(row);
                        }
                    });
                }
            });
        }
    }

    public bool IsFriendInCurrentRoom(string friendUid, string friendDisplayName)
    {
        var pcs = FindObjectsOfType<PlayerController>();
        foreach (var p in pcs)
        {
            if (p != null && !p.IsOwner && !p.IsLocalPlayer)
            {
                string pName = p.playerName != null ? p.playerName.Value.ToString() : "";

                if (!string.IsNullOrEmpty(pName))
                {
                    if (pName.Equals(friendDisplayName, System.StringComparison.OrdinalIgnoreCase) || pName.Equals(friendUid, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public int GetFriendsCountInRoom()
    {
        var pcs = FindObjectsOfType<PlayerController>();
        int count = 0;
        foreach (var p in pcs)
        {
            if (p != null) count++;
        }
        return Mathf.Max(0, count - 1);
    }

    private GameObject CreateButton(GameObject parent, string text, Color color, float width)
    {
        GameObject btnGO = new GameObject($"Btn_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent.transform, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, 36f);

        Image bg = btnGO.GetComponent<Image>();
        bg.color = color;
        bg.raycastTarget = true;

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 12; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.raycastTarget = false;

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(PlayButtonClickSFX);
        return btnGO;
    }

    private void EnsureFriendsPanelUI()
    {
        if (friendsPanel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Friends Panel Container
        friendsPanel = new GameObject("FriendsPanel", typeof(RectTransform), typeof(Image));
        friendsPanel.transform.SetParent(canvas.transform, false);

        RectTransform fRt = friendsPanel.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0.5f, 0.5f); fRt.anchorMax = new Vector2(0.5f, 0.5f); fRt.pivot = new Vector2(0.5f, 0.5f);
        fRt.sizeDelta = new Vector2(520f, 400f);

        Image fBg = friendsPanel.GetComponent<Image>();
        fBg.color = new Color(0.1f, 0.14f, 0.2f, 0.98f);

        // 2. Title
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(friendsPanel.transform, false);

        RectTransform tRt = titleGO.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -12f); tRt.sizeDelta = new Vector2(0f, 32f);

        TextMeshProUGUI tTmp = titleGO.GetComponent<TextMeshProUGUI>();
        tTmp.text = "SOCIAL & FRIENDS";
        tTmp.fontSize = 20; tTmp.fontStyle = FontStyles.Bold; tTmp.color = new Color(0.3f, 0.7f, 1f);
        tTmp.alignment = TextAlignmentOptions.Center;

        // 3. Tab Header Row (SEARCH | REQUESTS | FRIENDS)
        GameObject tabRow = new GameObject("TabHeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabRow.transform.SetParent(friendsPanel.transform, false);

        RectTransform trRt = tabRow.GetComponent<RectTransform>();
        trRt.anchorMin = new Vector2(0.5f, 1f); trRt.anchorMax = new Vector2(0.5f, 1f); trRt.pivot = new Vector2(0.5f, 1f);
        trRt.anchoredPosition = new Vector2(0f, -48f); trRt.sizeDelta = new Vector2(480f, 38f);

        HorizontalLayoutGroup hlg = tabRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f; hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        tabSearchButton = CreateTabButton(tabRow, "🔍 SEARCH", () => SwitchFriendsTab(FriendsTabType.Search));
        tabRequestsButton = CreateTabButton(tabRow, "📩 REQUESTS", () => SwitchFriendsTab(FriendsTabType.Requests));
        tabFriendsButton = CreateTabButton(tabRow, "👥 FRIENDS", () => SwitchFriendsTab(FriendsTabType.Friends));

        // 4. Global Search Filter Bar
        GameObject searchBarGO = new GameObject("FriendsSearchBar", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        searchBarGO.transform.SetParent(friendsPanel.transform, false);

        RectTransform sbRt = searchBarGO.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.5f, 1f); sbRt.anchorMax = new Vector2(0.5f, 1f); sbRt.pivot = new Vector2(0.5f, 1f);
        sbRt.anchoredPosition = new Vector2(0f, -92f); sbRt.sizeDelta = new Vector2(480f, 36f);

        Image sbBg = searchBarGO.GetComponent<Image>();
        sbBg.color = new Color(0.16f, 0.22f, 0.32f, 1f);

        friendsSearchFilterInput = searchBarGO.GetComponent<TMP_InputField>();

        GameObject sTxt = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        sTxt.transform.SetParent(searchBarGO.transform, false);
        RectTransform stRt = sTxt.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one; stRt.sizeDelta = Vector2.zero;
        stRt.offsetMin = new Vector2(12f, 0f); stRt.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI stTmp = sTxt.GetComponent<TextMeshProUGUI>();
        stTmp.fontSize = 14; stTmp.alignment = TextAlignmentOptions.MidlineLeft; stTmp.color = Color.white;

        friendsSearchFilterInput.textComponent = stTmp;
        friendsSearchFilterInput.onValueChanged.AddListener(OnFriendsSearchFilterChanged);

        // 5. ScrollView & Content Container
        GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollGO.transform.SetParent(friendsPanel.transform, false);

        RectTransform sRt = scrollGO.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(20f, 60f); sRt.offsetMax = new Vector2(-20f, -135f);

        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(scrollGO.transform, false);

        RectTransform cRt = contentGO.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f); cRt.anchorMax = new Vector2(1f, 1f); cRt.pivot = new Vector2(0.5f, 1f);
        cRt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
        sr.content = cRt; sr.horizontal = false; sr.vertical = true;

        friendsScrollViewContent = contentGO.transform;

        // 6. Close Button
        GameObject closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(friendsPanel.transform, false);

        RectTransform cBtnRt = closeGO.GetComponent<RectTransform>();
        cBtnRt.anchorMin = new Vector2(0.5f, 0f); cBtnRt.anchorMax = new Vector2(0.5f, 0f); cBtnRt.pivot = new Vector2(0.5f, 0f);
        cBtnRt.anchoredPosition = new Vector2(0f, 12f); cBtnRt.sizeDelta = new Vector2(140f, 38f);

        Image cBtnBg = closeGO.GetComponent<Image>();
        cBtnBg.color = new Color(0.85f, 0.22f, 0.22f, 1f);

        GameObject cTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        cTxtGO.transform.SetParent(closeGO.transform, false);
        RectTransform ctRt = cTxtGO.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one; ctRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI ctTmp = cTxtGO.GetComponent<TextMeshProUGUI>();
        ctTmp.text = "CLOSE"; ctTmp.fontSize = 15; ctTmp.fontStyle = FontStyles.Bold;
        ctTmp.alignment = TextAlignmentOptions.Center; ctTmp.color = Color.white;

        closeFriendsButton = closeGO.GetComponent<Button>();
        closeFriendsButton.onClick.RemoveAllListeners();
        closeFriendsButton.onClick.AddListener(CloseFriendsPanel);
    }

    private Button CreateTabButton(GameObject parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGO = new GameObject($"TabBtn_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent.transform, false);

        Image bg = btnGO.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.24f, 0.34f, 0.9f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 13; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;

        Button btn = btnGO.GetComponent<Button>();
        btn.onClick.AddListener(onClick);
        return btn;
    }
    #endregion
}
