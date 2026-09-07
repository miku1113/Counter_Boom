using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LoadingGameController : MonoBehaviour
{
    public enum MatchMode { QuickPlay, JoinCode, PrivateHost, InGameLoading, OfflineMode, PartyToLobby, ReturnToMainMenu }
    public static MatchMode TargetMode = MatchMode.QuickPlay;
    public static string JoinCodeToUse = "";

    [Header("UI Elements")]
    [SerializeField] private Image loadingBackgroundImage;
    [SerializeField] private Image loadingSpinnerImage;
    [SerializeField] private TextMeshProUGUI loadingStatusText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitInLoadingGameScene()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndInitLoadingScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        CheckAndInitLoadingScene(scene);
    }

    private static void CheckAndInitLoadingScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.name == "LoadingGame")
        {
            var controller = FindObjectOfType<LoadingGameController>();
            if (controller == null)
            {
                GameObject go = new GameObject("LoadingGameController", typeof(LoadingGameController));
                Debug.Log("[LoadingGame] Auto-spawned LoadingGameController in LoadingGame scene.");
                controller = go.GetComponent<LoadingGameController>();
            }
            if (controller != null)
            {
                controller.EnsureUI();
            }
        }
    }

    private void Awake()
    {
        EnsureUI();
    }

    private void OnEnable()
    {
        EnsureUI();
    }

    private void Start()
    {
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        EnsureUI();

        // Guarantee all unspawned/preview player objects are destroyed
        RelayNetworkManager.DestroyUnspawnedPreviewPlayers();

        // Start matchmaking flow asynchronously
        ExecuteMatchmaking();
    }

    private void Update()
    {
        if (loadingSpinnerImage != null)
        {
            loadingSpinnerImage.transform.Rotate(0f, 0f, -220f * Time.deltaTime);
        }
    }

    private async void ExecuteMatchmaking()
    {
        // If TargetMode is PartyToLobby, keep the connected party intact:
        if (TargetMode == MatchMode.PartyToLobby)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[LoadingGameController] PartyToLobby mode: maintaining connected party connection.");
            }
        }
        else
        {
            // Solo modes (QuickPlay, JoinCode, PrivateHost, Offline):
            // ALWAYS cleanly shut down any lingering party connections from Main Menu!
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[LoadingGameController] Shutting down prior party connection for solo matchmaking...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(250);
            }
        }

        if (TargetMode == MatchMode.ReturnToMainMenu)
        {
            UpdateStatus("Returning to Main Menu...");
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            if (CounterBoom.Networking.FirebaseManager.Instance != null)
            {
                CounterBoom.Networking.FirebaseManager.Instance.SetForcedPresenceStatus("online");
                CounterBoom.Networking.FirebaseManager.Instance.ForceSendPresenceUpdate();
            }
            await Task.Delay(800);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            return;
        }

        if (TargetMode == MatchMode.OfflineMode)
        {
            UpdateStatus("Initializing Offline Singleplayer Mode...");
            await Task.Delay(400);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            UpdateStatus("Loading Offline Mode Scene, AI Bots & Rooms...");
            await Task.Delay(600);
            UnityEngine.SceneManagement.SceneManager.LoadScene("OfflineMode");
            return;
        }

        UpdateStatus("Connecting to Relay & Unity Services...");
        await Task.Delay(200);

        if (RelayNetworkManager.Instance == null)
        {
            UpdateStatus("<color=red>Error: RelayNetworkManager missing!</color>");
            return;
        }

        bool success = false;

        switch (TargetMode)
        {
            case MatchMode.QuickPlay:
                UpdateStatus("Searching for active lobbies...");
                success = await RelayNetworkManager.Instance.QuickPlayMatchmaking();
                break;

            case MatchMode.JoinCode:
                UpdateStatus($"Connecting to room {JoinCodeToUse}...");
                success = await RelayNetworkManager.Instance.StartClientWithRelay(JoinCodeToUse);
                break;

            case MatchMode.PrivateHost:
                UpdateStatus("Creating private Relay room...");
                string code = await RelayNetworkManager.Instance.StartPrivateHostWithRelay();
                success = !string.IsNullOrEmpty(code);
                break;

            case MatchMode.InGameLoading:
                UpdateStatus("Preparing match, spawn points & 1:3 Thief/Hostage roles...");
                await Task.Delay(800);
                if (MatchRoleManager.Instance == null)
                {
                    GameObject go = new GameObject("MatchRoleManager", typeof(MatchRoleManager));
                }
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    if (MatchRoleManager.Instance != null)
                    {
                        MatchRoleManager.Instance.AssignRolesForConnectedPlayers();
                    }
                    UpdateStatus("Loading gameplay scene...");
                    await Task.Delay(400);
                    NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
                else
                {
                    // Non-host clients wait for the server to load GameScene via Netcode SceneManager
                    UpdateStatus("Waiting for host to launch gameplay scene...");
                }
                return;

            case MatchMode.PartyToLobby:
                UpdateStatus("Loading resources & synchronizing lobby with friends...");
                await Task.Delay(1000);
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    UpdateStatus("Entering lobby scene...");
                    await Task.Delay(500);
                    if (NetworkManager.Singleton.SceneManager != null)
                    {
                        NetworkManager.Singleton.SceneManager.LoadScene("CustomLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
                    }
                    else if (RelayNetworkManager.Instance != null)
                    {
                        RelayNetworkManager.Instance.ExecuteSceneLoad("CustomLobby");
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene("CustomLobby");
                    }
                }
                else
                {
                    UpdateStatus("Waiting for host to enter lobby scene...");
                }
                return;
        }

        if (success)
        {
            UpdateStatus("<color=green>Connected! Spawning player and loading lobby...</color>");
            await Task.Delay(500);
            UnityEngine.SceneManagement.SceneManager.LoadScene("CustomLobby");
        }
        else
        {
            UpdateStatus("<color=red>Connection failed. Returning to Main Menu...</color>");
            await Task.Delay(2000);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }
    }

    private void UpdateStatus(string message)
    {
        if (loadingStatusText != null)
        {
            loadingStatusText.text = message;
        }
        Debug.Log($"[LoadingGame] {message}");
    }

    private static TMP_FontAsset cachedFontAsset;
    public static TMP_FontAsset GetFontAsset()
    {
        if (cachedFontAsset != null) return cachedFontAsset;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (fonts != null && fonts.Length > 0)
        {
            foreach (var f in fonts)
            {
                if (f != null && !f.name.Contains("Liberation"))
                {
                    cachedFontAsset = f;
                    return cachedFontAsset;
                }
            }
            cachedFontAsset = fonts[0];
            return cachedFontAsset;
        }
        return null;
    }

    public void EnsureUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateLoadingUI(canvas);
    }

    private void CreateLoadingUI(Canvas canvas)
    {
        if (canvas == null) return;

        Transform existingPanel = canvas.transform.Find("LoadingPanel");
        if (existingPanel != null)
        {
            loadingStatusText = existingPanel.GetComponentInChildren<TextMeshProUGUI>();
            loadingSpinnerImage = existingPanel.Find("Container/LoadingSpinner")?.GetComponent<Image>();
            existingPanel.SetAsLastSibling();
            return;
        }

        // 1. Fullscreen dark backdrop
        GameObject panelObj = new GameObject("LoadingPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvas.transform, false);
        panelObj.transform.SetAsLastSibling();

        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        loadingBackgroundImage = panelObj.GetComponent<Image>();
        loadingBackgroundImage.color = new Color(0.04f, 0.07f, 0.12f, 0.96f);

        // 2. Central Card / Frame
        GameObject containerObj = new GameObject("Container", typeof(RectTransform), typeof(Image));
        containerObj.transform.SetParent(panelObj.transform, false);

        RectTransform cRt = containerObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(600f, 280f);

        Image cBg = containerObj.GetComponent<Image>();
        cBg.color = new Color(0.08f, 0.12f, 0.20f, 0.98f);

        Outline cOutline = containerObj.AddComponent<Outline>();
        cOutline.effectColor = new Color(0.2f, 0.65f, 1f, 0.6f);
        cOutline.effectDistance = new Vector2(2f, -2f);

        // 3. Header Title ("LOADING MATCH")
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(containerObj.transform, false);

        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 0.85f);
        tRt.anchorMax = new Vector2(0.5f, 0.85f);
        tRt.pivot = new Vector2(0.5f, 0.5f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(500f, 40f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        var font = GetFontAsset();
        if (font != null) titleTmp.font = font;
        titleTmp.fontSize = 26;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.4f, 0.85f, 1f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.text = "LOADING MATCH";

        // 4. Spinner Loader
        GameObject spinnerObj = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
        spinnerObj.transform.SetParent(containerObj.transform, false);

        RectTransform sRt = spinnerObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.52f);
        sRt.anchorMax = new Vector2(0.5f, 0.52f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = Vector2.zero;
        sRt.sizeDelta = new Vector2(56f, 56f);

        loadingSpinnerImage = spinnerObj.GetComponent<Image>();
        loadingSpinnerImage.color = new Color(1f, 0.85f, 0.2f, 1f);

        // 5. Status Text
        GameObject statusObj = new GameObject("LoadingStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(containerObj.transform, false);

        RectTransform stRt = statusObj.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0.5f, 0.22f);
        stRt.anchorMax = new Vector2(0.5f, 0.22f);
        stRt.pivot = new Vector2(0.5f, 0.5f);
        stRt.anchoredPosition = Vector2.zero;
        stRt.sizeDelta = new Vector2(540f, 50f);

        loadingStatusText = statusObj.GetComponent<TextMeshProUGUI>();
        if (font != null) loadingStatusText.font = font;
        loadingStatusText.fontSize = 18;
        loadingStatusText.fontStyle = FontStyles.Bold;
        loadingStatusText.color = Color.white;
        loadingStatusText.alignment = TextAlignmentOptions.Center;
        loadingStatusText.text = "Preparing match...";
    }
}
