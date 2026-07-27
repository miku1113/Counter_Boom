using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Enforces mandatory Landscape orientation across all mobile devices
/// and ensures resolution-independent UI/Controller sizing by auto-configuring
/// all scene Canvases to ScaleWithScreenSize (1920x1080, Match 0.5).
/// </summary>
public class ScreenAndUIScaler : MonoBehaviour
{
    private static ScreenAndUIScaler instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitOnLoad()
    {
        EnforceLandscapeOrientation();
        EnsureInstanceExists();
    }

    public static void EnsureInstanceExists()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("[ScreenAndUIScaler]");
            instance = go.AddComponent<ScreenAndUIScaler>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        EnforceLandscapeOrientation();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureAllCanvases();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceLandscapeOrientation();
        ConfigureAllCanvases();
    }

    private void Start()
    {
        EnforceLandscapeOrientation();
        ConfigureAllCanvases();
    }

    private void Update()
    {
        // Keep landscape orientation active in case of device rotate events
        EnforceLandscapeOrientation();
    }

    /// <summary>
    /// Enforces landscape orientation on mobile devices (Left & Right landscape allowed, Portrait disabled).
    /// </summary>
    public static void EnforceLandscapeOrientation()
    {
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        if (Screen.orientation != ScreenOrientation.LandscapeLeft && 
            Screen.orientation != ScreenOrientation.LandscapeRight && 
            Screen.orientation != ScreenOrientation.AutoRotation)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
    }

    /// <summary>
    /// Finds and configures all Canvases in the active scene to scale uniformly across resolutions.
    /// </summary>
    public static void ConfigureAllCanvases()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            ConfigureCanvas(canvas);
        }
    }

    /// <summary>
    /// Configures a CanvasScaler component to maintain identical UI / controller proportions across resolutions.
    /// </summary>
    public static void ConfigureCanvas(Canvas canvas)
    {
        if (canvas == null) return;

        // Skip WorldSpace canvases (e.g. overhead health bars or in-world indicators)
        if (canvas.renderMode == RenderMode.WorldSpace) return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }
}
