using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("UI Controls")]
    [SerializeField] private Slider audioSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle muteToggle;
    [SerializeField] private TMP_Dropdown graphicsDropdown;
    [SerializeField] private TMP_Dropdown fpsDropdown;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button saveNameButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private GameObject settingsPanel;

    [Header("Text Display Readouts")]
    [SerializeField] private TextMeshProUGUI audioValText;
    [SerializeField] private TextMeshProUGUI musicValText;
    [SerializeField] private TextMeshProUGUI sfxValText;
    [SerializeField] private TextMeshProUGUI sensitivityValText;

    private bool isInitializing = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySettingsOnStartup()
    {
        // 1. Audio
        float masterVol = PlayerPrefs.GetFloat("AudioVolume", 1.0f);
        bool isMuted = PlayerPrefs.GetInt("MuteAudio", 0) == 1;
        AudioListener.volume = isMuted ? 0f : masterVol;

        // 2. Graphics Quality
        int gfxQuality = PlayerPrefs.GetInt("GraphicsQuality", 2);
        int maxGfx = QualitySettings.names != null && QualitySettings.names.Length > 0 ? QualitySettings.names.Length - 1 : 2;
        gfxQuality = Mathf.Clamp(gfxQuality, 0, maxGfx);
        QualitySettings.SetQualityLevel(gfxQuality, true);

        // 3. Target Frame Rate
        int targetFpsIndex = PlayerPrefs.GetInt("TargetFPS", 1); // 0=30, 1=60, 2=120, 3=Unlimited
        int targetFps = targetFpsIndex switch
        {
            0 => 30,
            1 => 60,
            2 => 120,
            _ => -1
        };
        Application.targetFrameRate = targetFps;
    }

    private void Awake()
    {
        EnsureUIReferences();
    }

    private void Start()
    {
        EnsureUIReferences();
        LoadSettings();
        RegisterListeners();
    }

    private void OnEnable()
    {
        EnsureUIReferences();
        LoadSettings();
    }

    private void EnsureUIReferences()
    {
        if (settingsPanel == null)
        {
            settingsPanel = gameObject;
        }

        // Try finding components in existing children if unassigned
        if (audioSlider == null) audioSlider = FindChildComponent<Slider>("AudioSlider", "MasterSlider", "VolumeSlider");
        if (musicSlider == null) musicSlider = FindChildComponent<Slider>("MusicSlider", "BGM");
        if (sfxSlider == null) sfxSlider = FindChildComponent<Slider>("SFXSlider", "EffectSlider");
        if (muteToggle == null) muteToggle = FindChildComponent<Toggle>("MuteToggle", "Mute");
        if (graphicsDropdown == null) graphicsDropdown = FindChildComponent<TMP_Dropdown>("GraphicsDropdown", "QualityDropdown");
        if (fpsDropdown == null) fpsDropdown = FindChildComponent<TMP_Dropdown>("FPSDropdown", "FrameRateDropdown");
        if (sensitivitySlider == null) sensitivitySlider = FindChildComponent<Slider>("SensitivitySlider", "AimSlider");
        if (nameInputField == null) nameInputField = FindChildComponent<TMP_InputField>("NameInputField", "PlayerNameInput", "NameInput");
        if (backButton == null) backButton = FindChildComponent<Button>("BackButton", "CloseButton", "ExitButton");
        if (resetButton == null) resetButton = FindChildComponent<Button>("ResetButton", "DefaultButton");
    }

    private T FindChildComponent<T>(params string[] keywords) where T : Component
    {
        if (settingsPanel == null) return null;
        T[] components = settingsPanel.GetComponentsInChildren<T>(true);
        foreach (var c in components)
        {
            string n = c.gameObject.name.ToLower();
            foreach (var kw in keywords)
            {
                if (n.Contains(kw.ToLower())) return c;
            }
        }
        return components.Length > 0 ? components[0] : null;
    }

    private void LoadSettings()
    {
        isInitializing = true;

        // 1. Audio
        float masterVol = PlayerPrefs.GetFloat("AudioVolume", 1.0f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        bool isMuted = PlayerPrefs.GetInt("MuteAudio", 0) == 1;

        if (audioSlider != null) audioSlider.value = masterVol;
        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;
        if (muteToggle != null) muteToggle.isOn = isMuted;

        UpdateReadout(audioValText, $"{Mathf.RoundToInt(masterVol * 100)}%");
        UpdateReadout(musicValText, $"{Mathf.RoundToInt(musicVol * 100)}%");
        UpdateReadout(sfxValText, $"{Mathf.RoundToInt(sfxVol * 100)}%");

        AudioListener.volume = isMuted ? 0f : masterVol;

        // 2. Graphics
        int gfxIndex = PlayerPrefs.GetInt("GraphicsQuality", 2);
        if (graphicsDropdown != null && graphicsDropdown.options.Count > 0)
        {
            graphicsDropdown.value = Mathf.Clamp(gfxIndex, 0, graphicsDropdown.options.Count - 1);
        }
        QualitySettings.SetQualityLevel(gfxIndex, true);

        // 3. FPS Cap
        int fpsIndex = PlayerPrefs.GetInt("TargetFPS", 1);
        if (fpsDropdown != null && fpsDropdown.options.Count > 0)
        {
            fpsDropdown.value = Mathf.Clamp(fpsIndex, 0, fpsDropdown.options.Count - 1);
        }
        int targetFps = fpsIndex switch { 0 => 30, 1 => 60, 2 => 120, _ => -1 };
        Application.targetFrameRate = targetFps;

        // 4. Sensitivity
        float sensitivity = PlayerPrefs.GetFloat("AimSensitivity", 1.0f);
        if (sensitivitySlider != null) sensitivitySlider.value = sensitivity;
        UpdateReadout(sensitivityValText, $"{sensitivity:F1}x");

        // 5. Player Name
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        if (nameInputField != null) nameInputField.text = playerName;

        isInitializing = false;
    }

    private void RegisterListeners()
    {
        if (audioSlider != null)
        {
            audioSlider.onValueChanged.RemoveAllListeners();
            audioSlider.onValueChanged.AddListener(OnAudioChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }

        if (muteToggle != null)
        {
            muteToggle.onValueChanged.RemoveAllListeners();
            muteToggle.onValueChanged.AddListener(OnMuteToggled);
        }

        if (graphicsDropdown != null)
        {
            graphicsDropdown.onValueChanged.RemoveAllListeners();
            graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);
        }

        if (fpsDropdown != null)
        {
            fpsDropdown.onValueChanged.RemoveAllListeners();
            fpsDropdown.onValueChanged.AddListener(OnFPSChanged);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveAllListeners();
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.RemoveAllListeners();
            saveNameButton.onClick.AddListener(OnSaveNameClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBack);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetDefaults);
        }
    }

    private void UpdateReadout(TextMeshProUGUI label, string text)
    {
        if (label != null) label.text = text;
    }

    private void OnAudioChanged(float value)
    {
        if (isInitializing) return;
        PlayerPrefs.SetFloat("AudioVolume", value);
        PlayerPrefs.Save();

        bool isMuted = muteToggle != null && muteToggle.isOn;
        AudioListener.volume = isMuted ? 0f : value;
        UpdateReadout(audioValText, $"{Mathf.RoundToInt(value * 100)}%");
    }

    private void OnMusicChanged(float value)
    {
        if (isInitializing) return;
        PlayerPrefs.SetFloat("MusicVolume", value);
        PlayerPrefs.Save();
        UpdateReadout(musicValText, $"{Mathf.RoundToInt(value * 100)}%");
    }

    private void OnSFXChanged(float value)
    {
        if (isInitializing) return;
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();
        UpdateReadout(sfxValText, $"{Mathf.RoundToInt(value * 100)}%");
    }

    private void OnMuteToggled(bool isMuted)
    {
        if (isInitializing) return;
        PlayerPrefs.SetInt("MuteAudio", isMuted ? 1 : 0);
        PlayerPrefs.Save();
        float currentVol = audioSlider != null ? audioSlider.value : 1.0f;
        AudioListener.volume = isMuted ? 0f : currentVol;
    }

    private void OnGraphicsChanged(int index)
    {
        if (isInitializing) return;
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt("GraphicsQuality", index);
        PlayerPrefs.Save();
        Debug.Log($"[Settings] Graphics Quality changed to level: {index}");
    }

    private void OnFPSChanged(int index)
    {
        if (isInitializing) return;
        PlayerPrefs.SetInt("TargetFPS", index);
        PlayerPrefs.Save();

        int targetFps = index switch { 0 => 30, 1 => 60, 2 => 120, _ => -1 };
        Application.targetFrameRate = targetFps;
        Debug.Log($"[Settings] Target FPS set to: {targetFps}");
    }

    private void OnSensitivityChanged(float value)
    {
        if (isInitializing) return;
        PlayerPrefs.SetFloat("AimSensitivity", value);
        PlayerPrefs.Save();
        UpdateReadout(sensitivityValText, $"{value:F1}x");
    }

    private void OnSaveNameClicked()
    {
        if (nameInputField == null || string.IsNullOrEmpty(nameInputField.text.Trim())) return;
        string cleanName = nameInputField.text.Trim();
        PlayerPrefs.SetString("PlayerName", cleanName);
        PlayerPrefs.SetInt("PlayerNameHasBeenSet", 1);
        PlayerPrefs.Save();

        Debug.Log($"[Settings] Player name updated to: {cleanName}");

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.UpdatePlayerProfileUI();
        }
    }

    private void OnResetDefaults()
    {
        PlayerPrefs.SetFloat("AudioVolume", 1.0f);
        PlayerPrefs.SetFloat("MusicVolume", 0.8f);
        PlayerPrefs.SetFloat("SFXVolume", 1.0f);
        PlayerPrefs.SetInt("MuteAudio", 0);
        PlayerPrefs.SetInt("GraphicsQuality", 2);
        PlayerPrefs.SetInt("TargetFPS", 1);
        PlayerPrefs.SetFloat("AimSensitivity", 1.0f);
        PlayerPrefs.Save();

        LoadSettings();
        Debug.Log("[Settings] All settings reset to defaults.");
    }

    public void OnBack()
    {
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text.Trim()))
        {
            OnSaveNameClicked();
        }

        // Return to main panel via MainMenuController
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ResetPreviewToEquippedSkin();
            MainMenuController.Instance.ShowMainPanel();
        }
        else if (settingsPanel != null)
        {
            // Fallback: just hide the settings panel
            settingsPanel.SetActive(false);
        }
    }
}
