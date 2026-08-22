using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class LoadingGameController : MonoBehaviour
{
    public enum MatchMode { QuickPlay, JoinCode, PrivateHost, InGameLoading, OfflineMode }
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
            if (FindObjectOfType<LoadingGameController>() == null)
            {
                GameObject go = new GameObject("LoadingGameController", typeof(LoadingGameController));
                Debug.Log("[LoadingGame] Auto-spawned LoadingGameController in LoadingGame scene.");
            }
        }
    }

    private void Awake()
    {
        if (TargetMode != MatchMode.OfflineMode && NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            TargetMode = MatchMode.InGameLoading;
        }
        EnsureUI();
    }

    private void Start()
    {
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) ScreenAndUIScaler.ConfigureCanvas(canvas);

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
        if (TargetMode != MatchMode.OfflineMode && NetworkManager.Singleton != null && (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            TargetMode = MatchMode.InGameLoading;
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
        }

        if (success)
        {
            UpdateStatus("<color=green>Connected! Spawning player and loading lobby...</color>");
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

    private void EnsureUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (loadingStatusText == null || loadingSpinnerImage == null)
        {
            CreateLoadingUI(canvas);
        }
    }

    private void CreateLoadingUI(Canvas canvas)
    {
        // Fullscreen background panel
        GameObject panelObj = new GameObject("LoadingPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        loadingBackgroundImage = panelObj.GetComponent<Image>();
        loadingBackgroundImage.color = new Color(0.05f, 0.08f, 0.14f, 0.98f); // Deep dark background panel

        // Container
        GameObject containerObj = new GameObject("Container", typeof(RectTransform));
        containerObj.transform.SetParent(panelObj.transform, false);

        RectTransform cRt = containerObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(450f, 250f);

        // Spinner Loader
        GameObject spinnerObj = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
        spinnerObj.transform.SetParent(containerObj.transform, false);

        RectTransform sRt = spinnerObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.5f, 0.65f);
        sRt.anchorMax = new Vector2(0.5f, 0.65f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = Vector2.zero;
        sRt.sizeDelta = new Vector2(90f, 90f);

        loadingSpinnerImage = spinnerObj.GetComponent<Image>();
        loadingSpinnerImage.color = new Color(0.1f, 0.75f, 1f, 0.95f); // Cyan loading spinner

        // Status Text
        GameObject statusObj = new GameObject("LoadingStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObj.transform.SetParent(containerObj.transform, false);

        RectTransform stRt = statusObj.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0.5f, 0.2f);
        stRt.anchorMax = new Vector2(0.5f, 0.2f);
        stRt.pivot = new Vector2(0.5f, 0.5f);
        stRt.anchoredPosition = Vector2.zero;
        stRt.sizeDelta = new Vector2(420f, 60f);

        loadingStatusText = statusObj.GetComponent<TextMeshProUGUI>();
        loadingStatusText.fontSize = 22;
        loadingStatusText.fontStyle = FontStyles.Bold;
        loadingStatusText.color = Color.white;
        loadingStatusText.alignment = TextAlignmentOptions.Center;
        loadingStatusText.text = "PREPARING MATCH...";
    }
}
