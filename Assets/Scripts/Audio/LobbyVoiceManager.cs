using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

public class LobbyVoiceManager : NetworkBehaviour
{
    public static LobbyVoiceManager Instance { get; private set; }

    [Header("Voice Settings")]
    [SerializeField] private int sampleRate = 11025; // 11.025kHz crisp low-overhead voice audio
    [SerializeField] private float sendInterval = 0.1f; // Send audio packet every 100ms
    [SerializeField] private bool isMicMuted = false;

    private string micDevice;
    private AudioClip micClip;
    private int lastSamplePosition = 0;
    private float timer = 0f;
    private Button micToggleButton;
    private TextMeshProUGUI micStatusText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            StartMicrophone();
            EnsureMicUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        StopMicrophone();
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 10, sampleRate);
            Debug.Log($"[LobbyVoice] Started microphone on device: {micDevice}");
        }
        else
        {
            Debug.LogWarning("[LobbyVoice] No microphone devices found!");
        }
    }

    private void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
            Debug.Log("[LobbyVoice] Stopped microphone.");
        }
    }

    private void Update()
    {
        if (!IsOwner || isMicMuted || micClip == null || string.IsNullOrEmpty(micDevice)) return;

        timer += Time.deltaTime;
        if (timer >= sendInterval)
        {
            timer = 0f;
            ProcessAndSendAudio();
        }
    }

    private void ProcessAndSendAudio()
    {
        int currentPos = Microphone.GetPosition(micDevice);
        if (currentPos < 0 || currentPos == lastSamplePosition) return;

        int sampleCount = 0;
        if (currentPos > lastSamplePosition)
        {
            sampleCount = currentPos - lastSamplePosition;
        }
        else
        {
            sampleCount = (micClip.samples - lastSamplePosition) + currentPos;
        }

        if (sampleCount <= 0 || sampleCount > 44100)
        {
            lastSamplePosition = currentPos;
            return;
        }

        float[] pcmData = new float[sampleCount];
        micClip.GetData(pcmData, lastSamplePosition);
        lastSamplePosition = currentPos;

        // Check if there is actual voice audio volume (RMS check)
        float sum = 0f;
        for (int i = 0; i < pcmData.Length; i++) sum += pcmData[i] * pcmData[i];
        float rms = Mathf.Sqrt(sum / pcmData.Length);

        if (rms > 0.015f) // Voice activity threshold
        {
            SendVoiceAudioServerRpc(OwnerClientId, pcmData);
        }
    }

    [ServerRpc]
    private void SendVoiceAudioServerRpc(ulong senderClientId, float[] pcmData)
    {
        SendVoiceAudioClientRpc(senderClientId, pcmData);
    }

    [ClientRpc]
    private void SendVoiceAudioClientRpc(ulong senderClientId, float[] pcmData)
    {
        if (senderClientId == NetworkManager.Singleton.LocalClientId) return; // Don't loop back to self

        // Play voice audio on remote player's character in the scene
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var player in players)
        {
            if (player != null && player.OwnerClientId == senderClientId)
            {
                AudioSource source = player.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = player.gameObject.AddComponent<AudioSource>();
                    source.spatialBlend = 0.5f;
                    source.minDistance = 2f;
                    source.maxDistance = 25f;
                }

                AudioClip clip = AudioClip.Create("RemoteVoice", pcmData.Length, 1, sampleRate, false);
                clip.SetData(pcmData, 0);
                source.PlayOneShot(clip);
                break;
            }
        }
    }

    public void ToggleMic()
    {
        isMicMuted = !isMicMuted;
        UpdateMicUI();
        Debug.Log($"[LobbyVoice] Mic Muted State: {isMicMuted}");
    }

    private void EnsureMicUI()
    {
        if (micToggleButton != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Create Mic Toggle button on Canvas
        GameObject btnGO = new GameObject("MicToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -80f);
        rt.sizeDelta = new Vector2(120f, 40f);

        Image img = btnGO.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.24f, 0.85f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.sizeDelta = Vector2.zero;

        micStatusText = txtGO.GetComponent<TextMeshProUGUI>();
        micStatusText.fontSize = 16;
        micStatusText.fontStyle = FontStyles.Bold;
        micStatusText.alignment = TextAlignmentOptions.Center;

        micToggleButton = btnGO.GetComponent<Button>();
        micToggleButton.onClick.AddListener(ToggleMic);

        UpdateMicUI();
    }

    private void UpdateMicUI()
    {
        if (micStatusText != null)
        {
            micStatusText.text = isMicMuted ? "🔇 MIC OFF" : "🎙️ MIC ON";
            micStatusText.color = isMicMuted ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.3f, 0.9f, 0.4f);
        }
    }
}
