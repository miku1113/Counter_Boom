using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;

public class InteractiveLobbyController : MonoBehaviour
{
    public static InteractiveLobbyController Instance { get; private set; }

    [Header("UI Buttons")]
    [SerializeField] private Button menuToggleButton;       // 3-Dots Button in top right
    [SerializeField] private Button closeMenuButton;
    [SerializeField] private Button startGameButton;        // Host only
    [SerializeField] private Button leaveLobbyButton;

    [Header("UI Overlay & Text")]
    [SerializeField] private GameObject playerListOverlayPanel;
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Transform playerListContentContainer;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private TextMeshProUGUI pingText;

    private List<GameObject> spawnedListItems = new List<GameObject>();
    private UnityEngine.Ping systemPingFallback;
    private int cachedPingMs = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 0. Validate EventSystem & GraphicRaycaster in active scene
        EnsureEventSystemAndRaycaster();

        // 0. Auto-Find & Fix Unassigned or Misconfigured UI Elements
        AutoResolveUIButtons();

        // 1. Wire Button Click Listeners
        if (menuToggleButton != null)
        {
            menuToggleButton.onClick.RemoveAllListeners();
            menuToggleButton.onClick.AddListener(TogglePlayerListOverlay);
            menuToggleButton.transform.SetAsLastSibling();
        }
        if (closeMenuButton != null)
        {
            closeMenuButton.onClick.RemoveAllListeners();
            closeMenuButton.onClick.AddListener(() => SetOverlayState(false));
        }
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.RemoveAllListeners();
            leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        }

        // 2. Hide overlay by default
        SetOverlayState(false);

        // 3. Subscribe to Netcode Callbacks for Live Player List updates
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        RefreshLobbyUI();

        // 4. Setup Internet Speed / Ping display UI and Real-Time Voice Chat
        EnsurePingUI();
        EnsureVoiceManager();
        InvokeRepeating(nameof(UpdatePingDisplay), 0.1f, 0.5f);
    }

    private void EnsureVoiceManager()
    {
        if (LobbyVoiceManager.Instance == null)
        {
            GameObject vGO = new GameObject("LobbyVoiceManager", typeof(LobbyVoiceManager));
            Debug.Log("[InteractiveLobby] Real-time LobbyVoiceManager initialized.");
        }
    }

    private void EnsureEventSystemAndRaycaster()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.Log("[InteractiveLobby] EventSystem missing in scene — creating dynamic EventSystem.");
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        Canvas mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas == null) mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas != null)
        {
            var raycaster = mainCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                mainCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            else if (!raycaster.enabled)
            {
                raycaster.enabled = true;
            }
        }
    }

    private void AutoResolveUIButtons()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 1. Auto-find Overlay Panel if unassigned or invalid
        if (playerListOverlayPanel == null || !playerListOverlayPanel.scene.IsValid())
        {
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == null || t.gameObject == gameObject) continue;
                string tName = t.name.ToLower();
                if (tName.Contains("overlay") || tName.Contains("playerlist") || tName.Contains("customlobbypanel") || tName.Contains("lobbypanel") || tName.Contains("menuoverlay") || tName.Contains("popup"))
                {
                    if (t.GetComponent<Canvas>() == null)
                    {
                        playerListOverlayPanel = t.gameObject;
                        Debug.Log($"[InteractiveLobby] Auto-resolved playerListOverlayPanel to: '{t.name}'");
                        break;
                    }
                }
            }
        }

        // 2. Resolve playerListContentContainer to a valid SCENE object
        if (playerListContentContainer == null || !playerListContentContainer.gameObject.scene.IsValid())
        {
            if (playerListOverlayPanel != null)
            {
                Transform[] panelChildren = playerListOverlayPanel.GetComponentsInChildren<Transform>(true);
                foreach (var child in panelChildren)
                {
                    if (child == playerListOverlayPanel.transform) continue;
                    string cName = child.name.ToLower();
                    if (cName.Contains("content") || cName.Contains("container") || cName.Contains("list") || cName.Contains("playerlist"))
                    {
                        playerListContentContainer = child;
                        Debug.Log($"[InteractiveLobby] Auto-resolved playerListContentContainer to scene object: '{child.name}'");
                        break;
                    }
                }
            }
        }

        // 3. Auto-find Buttons
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b == null) continue;
            string bName = b.gameObject.name.ToLower();
            TMP_Text tmp = b.GetComponentInChildren<TMP_Text>();
            string bText = tmp != null ? tmp.text.ToLower() : "";

            if (menuToggleButton == null && (bName.Contains("threedots") || bName.Contains("toggle") || bName.Contains("menu") || bText.Contains("...") || bName.Contains("dots")))
            {
                menuToggleButton = b;
            }
            else if (closeMenuButton == null && (bName.Contains("close") || bText.Contains("close") || bText.Contains("x")))
            {
                closeMenuButton = b;
            }
            else if (startGameButton == null && (bName.Contains("start") || bName.Contains("launch") || bText.Contains("start")))
            {
                startGameButton = b;
            }
            else if (leaveLobbyButton == null && (bName.Contains("leave") || bName.Contains("exit") || bText.Contains("leave")))
            {
                leaveLobbyButton = b;
            }
        }

        // Fallback for menuToggleButton: top-right anchor position
        if (menuToggleButton == null && buttons.Length > 0)
        {
            foreach (var b in buttons)
            {
                RectTransform rt = b.GetComponent<RectTransform>();
                if (rt != null && rt.anchorMin.x >= 0.7f && rt.anchorMin.y >= 0.7f)
                {
                    menuToggleButton = b;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (menuToggleButton == null || playerListOverlayPanel == null || playerListContentContainer == null || !playerListContentContainer.gameObject.scene.IsValid())
        {
            AutoResolveUIButtons();
            if (menuToggleButton != null)
            {
                menuToggleButton.onClick.RemoveAllListeners();
                menuToggleButton.onClick.AddListener(TogglePlayerListOverlay);
                menuToggleButton.transform.SetAsLastSibling();
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[InteractiveLobby] Player connected: {clientId}");
        RefreshLobbyUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[InteractiveLobby] Player disconnected: {clientId}");
        RefreshLobbyUI();
    }

    public void TogglePlayerListOverlay()
    {
        if (playerListOverlayPanel == null)
        {
            AutoResolveUIButtons();
        }

        if (playerListOverlayPanel != null)
        {
            bool newState = !playerListOverlayPanel.activeSelf;
            SetOverlayState(newState);
            Debug.Log($"[InteractiveLobby] Toggled player list overlay active state to: {newState}");
        }
        else
        {
            Debug.LogWarning("[InteractiveLobby] TogglePlayerListOverlay called but playerListOverlayPanel could not be found!");
        }
    }

    public void SetOverlayState(bool active)
    {
        if (playerListOverlayPanel != null)
        {
            playerListOverlayPanel.SetActive(active);
        }
        if (active)
        {
            RefreshLobbyUI();
        }
    }

    /// <summary>
    /// Refreshes room join code, host launch button visibility, and player list items.
    /// Fixes text position overlaps.
    /// </summary>
    public void RefreshLobbyUI()
    {
        // Fix text position overlaps
        if (playerCountText != null)
        {
            RectTransform rt = playerCountText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -20f);
            }
        }

        if (joinCodeText != null)
        {
            RectTransform rt = joinCodeText.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -55f);
            }
        }

        // 1. Join Code Text
        if (joinCodeText != null)
        {
            string code = RelayNetworkManager.Instance != null ? RelayNetworkManager.Instance.CurrentJoinCode : "";
            joinCodeText.text = !string.IsNullOrEmpty(code) ? $"ROOM CODE: {code}" : "ROOM CODE: -";
        }

        // 2. Host Start Game Button
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
        }

        // 3. Player Count & List Items
        int count = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsIds.Count : 1;
        int max = RelayNetworkManager.Instance != null ? RelayNetworkManager.Instance.MaxPlayers : 10;
        if (playerCountText != null)
        {
            playerCountText.text = $"PLAYERS IN LOBBY ({count}/{max})";
        }

        RebuildPlayerList();
    }

    private void RebuildPlayerList()
    {
        // Clear old item gameobjects
        foreach (var item in spawnedListItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedListItems.Clear();

        // Auto-resolve container if null or invalid
        if (playerListContentContainer == null || !playerListContentContainer.gameObject.scene.IsValid())
        {
            AutoResolveUIButtons();
        }

        if (playerListContentContainer == null || !playerListContentContainer.gameObject.scene.IsValid())
        {
            Debug.LogWarning("[InteractiveLobby] playerListContentContainer is null or not in scene!");
            return;
        }

        // Ensure VerticalLayoutGroup exists on container for clean vertical list stacking
        VerticalLayoutGroup vlg = playerListContentContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = playerListContentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        IReadOnlyList<ulong> clientIds;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
        {
            clientIds = NetworkManager.Singleton.ConnectedClientsIds;
        }
        else
        {
            clientIds = new ulong[] { 0 }; // Fallback to local player 1 if network manager is initializing
        }

        foreach (ulong clientId in clientIds)
        {
            bool isServerHost = (NetworkManager.Singleton != null && (clientId == NetworkManager.ServerClientId || clientId == 0));

            string pName = "Player";
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            foreach (var p in players)
            {
                if (p != null && (p.OwnerClientId == clientId || p.IsLocal && clientId == 0))
                {
                    string netName = p.playerName.Value.ToString();
                    if (!string.IsNullOrEmpty(netName))
                    {
                        pName = netName;
                        break;
                    }
                }
            }

            if (pName == "Player" || pName == "You" || string.IsNullOrEmpty(pName))
            {
                pName = PlayerController.GetOrGeneratePlayerName();
            }

            string displayName = isServerHost ? $"{pName} (Host)" : pName;

            GameObject listItemObj = null;

            if (playerListItemPrefab != null && playerListItemPrefab.scene.IsValid())
            {
                listItemObj = Instantiate(playerListItemPrefab, playerListContentContainer, false);
            }
            else
            {
                // Create a beautiful, robust player item UI row dynamically inside the container
                listItemObj = new GameObject($"PlayerItem_{clientId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                listItemObj.transform.SetParent(playerListContentContainer, false);

                Image bg = listItemObj.GetComponent<Image>();
                bg.color = new Color(0.1f, 0.15f, 0.22f, 0.85f); // Dark translucent blue-gray background

                LayoutElement le = listItemObj.GetComponent<LayoutElement>();
                le.minHeight = 45f;
                le.preferredHeight = 45f;
                le.flexibleWidth = 1f;

                // Add text label inside row
                GameObject txtObj = new GameObject("PlayerNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtObj.transform.SetParent(listItemObj.transform, false);

                RectTransform txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = new Vector2(15f, 0f); // Left padding
                txtRt.offsetMax = new Vector2(-15f, 0f);

                TextMeshProUGUI txt = txtObj.GetComponent<TextMeshProUGUI>();
                txt.text = displayName;
                txt.fontSize = 20;
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.Left;
                txt.color = isServerHost ? new Color(1f, 0.85f, 0.2f) : Color.white; // Gold color for Host
            }

            if (listItemObj != null)
            {
                spawnedListItems.Add(listItemObj);

                TextMeshProUGUI txt = listItemObj.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = displayName;
                }
            }
        }
    }

    private void OnStartGameClicked()
    {
        Debug.Log("[InteractiveLobby] Start Game clicked by Host!");
        if (RelayNetworkManager.Instance != null)
        {
            RelayNetworkManager.Instance.StartMatchFromLobby();
        }
    }

    private async void OnLeaveLobbyClicked()
    {
        Debug.Log("[InteractiveLobby] Leaving lobby gracefully...");
        if (RelayNetworkManager.Instance != null)
        {
            await RelayNetworkManager.Instance.LeaveMatchGracefully();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void EnsurePingUI()
    {
        if (pingText != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        TextMeshProUGUI[] tmps = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            if (tmp == null) continue;
            string n = tmp.gameObject.name.ToLower();
            if (n.Contains("ping") || n.Contains("speed") || n.Contains("internet") || n.Contains("latency"))
            {
                pingText = tmp;
                break;
            }
        }

        if (pingText == null)
        {
            GameObject pingGO = new GameObject("PingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pingGO.transform.SetParent(canvas.transform, false);

            RectTransform rect = pingGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-25f, 25f);
            rect.sizeDelta = new Vector2(320f, 40f);

            pingText = pingGO.GetComponent<TextMeshProUGUI>();
            pingText.fontSize = 20;
            pingText.alignment = TextAlignmentOptions.BottomRight;
            pingText.raycastTarget = false;
            pingText.fontStyle = FontStyles.Bold;
        }
    }

    private void UpdatePingDisplay()
    {
        EnsurePingUI();
        if (pingText == null) return;

        int pingMs = GetCurrentPingMs();

        if (pingMs <= 0)
        {
            pingText.text = "Internet Speed: <color=#CCCCCC>Measuring...</color>";
            return;
        }

        string qualityStr;
        string colorHex;

        if (pingMs < 80)
        {
            qualityStr = "Strong";
            colorHex = "#00FF66";
        }
        else if (pingMs < 200)
        {
            qualityStr = "Good";
            colorHex = "#FFCC00";
        }
        else if (pingMs < 400)
        {
            qualityStr = "Weak";
            colorHex = "#FF8800";
        }
        else
        {
            qualityStr = "Poor";
            colorHex = "#FF3333";
        }

        pingText.text = $"Internet Speed: <color={colorHex}>{pingMs}ms ({qualityStr})</color>";
    }

    private int GetCurrentPingMs()
    {
        if (Photon.Pun.PhotonNetwork.IsConnected)
        {
            return Photon.Pun.PhotonNetwork.GetPing();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            try
            {
                var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                if (transport != null)
                {
                    ulong targetId = NetworkManager.Singleton.IsServer ? 0 : NetworkManager.ServerClientId;
                    int rtt = (int)transport.GetCurrentRtt(targetId);
                    if (rtt > 0) return rtt;
                }
            }
            catch { }
        }

        if (systemPingFallback == null)
        {
            systemPingFallback = new UnityEngine.Ping("8.8.8.8");
        }
        else if (systemPingFallback.isDone)
        {
            cachedPingMs = systemPingFallback.time;
            systemPingFallback = new UnityEngine.Ping("8.8.8.8");
        }

        return cachedPingMs;
    }
}
