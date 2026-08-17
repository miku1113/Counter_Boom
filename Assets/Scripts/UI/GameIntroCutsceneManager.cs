using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class GameIntroCutsceneManager : NetworkBehaviour
{
    public static GameIntroCutsceneManager Instance { get; private set; }

    [Header("Cutscene UI Panels")]
    [SerializeField] private GameObject cutscenePanel;
    [SerializeField] private TextMeshProUGUI cutsceneBannerText;
    [SerializeField] private TextMeshProUGUI speechBubbleText;
    [SerializeField] private Image speechBubbleBg;

    private GameObject guardThiefNPC;

    private bool hasCutsceneStarted = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            hasCutsceneStarted = false;
            StartCutscene();
        }
    }

    private void Start()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
        {
            StartCutscene();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            TriggerIntroCutsceneClientRpc();
        }
    }

    [ClientRpc]
    private void TriggerIntroCutsceneClientRpc()
    {
        StartCutscene();
    }

    public void StartCutscene()
    {
        if (hasCutsceneStarted) return;
        hasCutsceneStarted = true;
        Debug.Log("[GameIntroCutsceneManager] Starting intro cutscene sequence...");
        StopAllCoroutines();
        StartCoroutine(PlayIntroCutsceneSequence());
    }

    private IEnumerator PlayIntroCutsceneSequence()
    {
        // 0. Ensure Cutscene Canvas & UI exist
        EnsureCutsceneUI();
        if (cutscenePanel != null) cutscenePanel.SetActive(true);

        // Lock mobile controllers
        if (MobileInputManager.Instance != null)
        {
            MobileInputManager.Instance.SetControlsActive(false);
        }

        // Ground Hall location for Hostages & Guard Thief
        Vector3 groundPos;
        if (MatchRoleManager.Instance != null && MatchRoleManager.Instance.groundHallTransform != null)
        {
            groundPos = MatchRoleManager.Instance.groundHallTransform.position;
        }
        else
        {
            groundPos = MatchRoleManager.Instance != null ? MatchRoleManager.Instance.groundFloorCenter : new Vector3(0f, -6f, 0f);
        }

        // Spawn temporary Guard Thief NPC in Ground Hall for cutscene
        guardThiefNPC = new GameObject("CutsceneGuardThiefNPC", typeof(SpriteRenderer));
        guardThiefNPC.transform.position = groundPos + new Vector3(1.2f, 0.5f, 0f);
        SpriteRenderer sr = guardThiefNPC.GetComponent<SpriteRenderer>();
        sr.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Red Thief uniform
        sr.sortingOrder = 100;

        Camera mainCam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

        // Phase 1: Thieves searching rooms
        if (cutsceneBannerText != null)
        {
            cutsceneBannerText.text = "🔴 <color=red>THIEVES ARE RAIDING ROOMS FOR LOOT...</color>";
        }
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetTarget(null);
        }
        Vector3 roomPos = Vector3.up * 6f;
        if (MatchRoleManager.Instance != null)
        {
            Transform rndRoom = MatchRoleManager.Instance.GetRandomRoomTransform();
            if (rndRoom != null) roomPos = rndRoom.position;
        }
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(roomPos.x, roomPos.y, -10f); // Room view
        }
        yield return new WaitForSeconds(1.2f);

        // Phase 2: Hostages gathered on Ground Floor with Guard Thief
        if (cutsceneBannerText != null)
        {
            cutsceneBannerText.text = "🔵 <color=yellow>HOSTAGES TRAPPED IN GROUND HALL, GUARDED BY THIEF!</color>";
        }
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(groundPos.x, groundPos.y, -10f); // Ground hall view
        }
        yield return new WaitForSeconds(1.2f);

        // Phase 3: Guard Thief Washroom Emergency!
        if (cutsceneBannerText != null)
        {
            cutsceneBannerText.text = "🚽 <color=orange>THE GUARD THIEF HAS AN EMERGENCY WASHROOM BREAK!</color>";
        }
        if (speechBubbleBg != null && speechBubbleText != null)
        {
            speechBubbleBg.gameObject.SetActive(true);
            speechBubbleText.text = "😱 OH NO!! WASHROOM EMERGENCY!! GOTTA RUN!";
        }

        // Animate Guard Thief dashing away out of the hall room
        float dashTime = 0f;
        Vector3 startGuardPos = guardThiefNPC.transform.position;
        Vector3 exitGuardPos = startGuardPos + new Vector3(12f, 0f, 0f); // Run right off-screen
        while (dashTime < 1.0f)
        {
            dashTime += Time.deltaTime;
            if (guardThiefNPC != null)
            {
                guardThiefNPC.transform.position = Vector3.Lerp(startGuardPos, exitGuardPos, dashTime / 1.0f);
            }
            yield return null;
        }

        if (speechBubbleBg != null) speechBubbleBg.gameObject.SetActive(false);

        // Relocate Guard Thief to a random room just like other thieves
        if (guardThiefNPC != null)
        {
            if (MatchRoleManager.Instance != null)
            {
                Transform randomRoom = MatchRoleManager.Instance.GetRandomRoomTransform();
                if (randomRoom != null)
                {
                    guardThiefNPC.transform.position = randomRoom.position;
                    Debug.Log($"[GameIntroCutsceneManager] Guard Thief relocated to random room '{randomRoom.name}'.");
                }
            }
            Destroy(guardThiefNPC, 1.5f);
        }

        // Phase 4: Game Begins & Controls Unlock!
        if (cutsceneBannerText != null)
        {
            cutsceneBannerText.text = "⚡ <color=green>THE GUARD IS GONE! MATCH BEGUN!</color>\n<size=80%>Hostages: Find 2 Keys to unlock Main Gate! Thieves: Stop them!</size>";
        }

        yield return new WaitForSeconds(0.8f);

        EndCutsceneAndEnableControls();
    }

    public void SkipCutscene()
    {
        StopAllCoroutines();
        if (speechBubbleBg != null) speechBubbleBg.gameObject.SetActive(false);
        if (guardThiefNPC != null) Destroy(guardThiefNPC);
        EndCutsceneAndEnableControls();
    }

    private void EndCutsceneAndEnableControls()
    {
        if (cutscenePanel != null) cutscenePanel.SetActive(false);

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null)
            {
                p.RestoreGameplayComponents();
                if (p.IsOwner || p.IsLocal)
                {
                    if (CameraController.Instance != null) CameraController.Instance.SetTarget(p.transform);
                }
            }
        }

        if (MobileInputManager.Instance != null)
        {
            MobileInputManager.Instance.SetControlsActive(true);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("🎮 Controls Active! Find the 2 Keys on Ground Floor!");
            HUDManager.Instance.UpdateRoleBadgeDisplay();
        }
    }

    private void EnsureCutsceneUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (cutscenePanel == null)
        {
            // Panel overlay
            cutscenePanel = new GameObject("CutsceneBannerPanel", typeof(RectTransform), typeof(Image));
            cutscenePanel.transform.SetParent(canvas.transform, false);

            RectTransform pRt = cutscenePanel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.1f, 0.75f);
            pRt.anchorMax = new Vector2(0.9f, 0.95f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;

            Image pImg = cutscenePanel.GetComponent<Image>();
            pImg.color = new Color(0.05f, 0.08f, 0.15f, 0.9f); // Dark banner

            // Banner Text
            GameObject txtGO = new GameObject("BannerText", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(cutscenePanel.transform, false);

            RectTransform tRt = txtGO.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(10f, 5f);
            tRt.offsetMax = new Vector2(-110f, -5f);

            cutsceneBannerText = txtGO.GetComponent<TextMeshProUGUI>();
            cutsceneBannerText.fontSize = 22;
            cutsceneBannerText.fontStyle = FontStyles.Bold;
            cutsceneBannerText.alignment = TextAlignmentOptions.Center;
            cutsceneBannerText.color = Color.white;

            // Skip Button
            GameObject skipBtnGO = new GameObject("SkipCutsceneButton", typeof(RectTransform), typeof(Image), typeof(Button));
            skipBtnGO.transform.SetParent(cutscenePanel.transform, false);

            RectTransform skipRt = skipBtnGO.GetComponent<RectTransform>();
            skipRt.anchorMin = new Vector2(1f, 0.5f);
            skipRt.anchorMax = new Vector2(1f, 0.5f);
            skipRt.pivot = new Vector2(1f, 0.5f);
            skipRt.anchoredPosition = new Vector2(-15f, 0f);
            skipRt.sizeDelta = new Vector2(90f, 40f);

            Image skipImg = skipBtnGO.GetComponent<Image>();
            skipImg.color = new Color(0.9f, 0.3f, 0.1f, 0.95f);

            GameObject skipTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            skipTxtGO.transform.SetParent(skipBtnGO.transform, false);
            RectTransform stRt = skipTxtGO.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero; stRt.anchorMax = Vector2.one; stRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI skipTmp = skipTxtGO.GetComponent<TextMeshProUGUI>();
            skipTmp.text = "SKIP ⏩";
            skipTmp.fontSize = 16;
            skipTmp.fontStyle = FontStyles.Bold;
            skipTmp.alignment = TextAlignmentOptions.Center;
            skipTmp.color = Color.white;

            Button btn = skipBtnGO.GetComponent<Button>();
            btn.onClick.AddListener(SkipCutscene);

            // Speech Bubble for Guard Thief
            GameObject bubbleGO = new GameObject("SpeechBubbleBg", typeof(RectTransform), typeof(Image));
            bubbleGO.transform.SetParent(canvas.transform, false);

            RectTransform bRt = bubbleGO.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.45f);
            bRt.anchorMax = new Vector2(0.5f, 0.45f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.sizeDelta = new Vector2(380f, 60f);
            bRt.anchoredPosition = Vector2.zero;

            speechBubbleBg = bubbleGO.GetComponent<Image>();
            speechBubbleBg.color = new Color(1f, 1f, 1f, 0.95f); // White speech bubble background

            GameObject bTxtGO = new GameObject("BubbleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bTxtGO.transform.SetParent(bubbleGO.transform, false);

            RectTransform btRt = bTxtGO.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;

            speechBubbleText = bTxtGO.GetComponent<TextMeshProUGUI>();
            speechBubbleText.fontSize = 18;
            speechBubbleText.fontStyle = FontStyles.Bold;
            speechBubbleText.color = new Color(0.8f, 0.1f, 0.1f, 1f); // Dark red bold text
            speechBubbleText.alignment = TextAlignmentOptions.Center;

            speechBubbleBg.gameObject.SetActive(false);
        }
    }
}
