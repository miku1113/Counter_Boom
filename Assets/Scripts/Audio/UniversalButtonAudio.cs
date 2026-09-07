using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class UniversalButtonAudio : MonoBehaviour
{
    public static UniversalButtonAudio Instance { get; private set; }

    private static AudioClip proceduralClickClip;
    private AudioSource audioSource;
    private HashSet<Button> registeredButtons = new HashSet<Button>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("UniversalButtonAudio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<UniversalButtonAudio>();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D UI sound
        audioSource.volume = 0.8f;

        EnsureProceduralClickClip();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        registeredButtons.Clear();
        RegisterAllButtonsInScene();
        CancelInvoke(nameof(RegisterAllButtonsInScene));
        InvokeRepeating(nameof(RegisterAllButtonsInScene), 0.2f, 1f);
    }

    public void RegisterAllButtonsInScene()
    {
        registeredButtons.RemoveWhere(b => b == null);
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button btn in allButtons)
        {
            if (btn != null && !registeredButtons.Contains(btn))
            {
                btn.onClick.RemoveListener(PlayClickSFX);
                btn.onClick.AddListener(PlayClickSFX);
                registeredButtons.Add(btn);
            }
        }
    }

    private static float lastGlobalClickSFXTime = -1f;
    private const float GLOBAL_CLICK_COOLDOWN = 0.08f;

    public static void PlayClickSFX()
    {
        if (Time.unscaledTime - lastGlobalClickSFXTime < GLOBAL_CLICK_COOLDOWN)
        {
            return;
        }
        lastGlobalClickSFXTime = Time.unscaledTime;

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.PlayButtonClickSFX();
            return;
        }

        if (Instance != null)
        {
            Instance.PlayDefaultClickSFX();
        }
    }

    public void PlayDefaultClickSFX()
    {
        if (audioSource == null) return;
        EnsureProceduralClickClip();
        if (proceduralClickClip != null)
        {
            audioSource.PlayOneShot(proceduralClickClip, 0.7f);
        }
    }

    private static void EnsureProceduralClickClip()
    {
        if (proceduralClickClip != null) return;

        // Generate a crisp, pleasant 15ms UI click blip
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.015f); // 15 ms
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 300f); // Sharp exponential decay
            float wave = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.4f;
            data[i] = wave * envelope;
        }

        proceduralClickClip = AudioClip.Create("UniversalClickSFX", samples, 1, sampleRate, false);
        proceduralClickClip.SetData(data, 0);
    }
}
