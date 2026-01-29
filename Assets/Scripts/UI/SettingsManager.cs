using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private Slider audioSlider;
    [SerializeField] private TMP_Dropdown graphicsDropdown;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject settingsPanel;
    
    private void Start()
    {
        LoadSettings();
        
        audioSlider.onValueChanged.AddListener(OnAudioChanged);
        graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);
        backButton.onClick.AddListener(OnBack);
    }
    
    private void LoadSettings()
    {
        audioSlider.value = PlayerPrefs.GetFloat("AudioVolume", 1f);
        graphicsDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 2);
        
        // Apply loaded settings
        AudioListener.volume = audioSlider.value;
        QualitySettings.SetQualityLevel(graphicsDropdown.value);
    }
    
    private void OnAudioChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("AudioVolume", value);
    }
    
    private void OnGraphicsChanged(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt("GraphicsQuality", index);
    }
    
    private void OnBack()
    {
        settingsPanel.SetActive(false);
    }
}
