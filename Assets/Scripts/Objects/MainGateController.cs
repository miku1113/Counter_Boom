using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class MainGateController : NetworkBehaviour
{
    public static MainGateController Instance { get; private set; }

    [Header("Gate Settings")]
    public Vector3 groundFloorGatePosition = new Vector3(0f, -10f, 0f);
    public bool isUnlocked = false;
    public int requiredKeyCount = 2;

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer gateSpriteRenderer;
    [SerializeField] private Collider2D solidGateCollider;
    private TextMeshPro statusLabel;

    private GameObject buttonGO;
    private Button button;
    private TextMeshProUGUI buttonLabel;
    private PlayerController localPlayer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Preserve scene position unless mainGateTransform is explicitly assigned
        if (MatchRoleManager.Instance != null && MatchRoleManager.Instance.mainGateTransform != null)
        {
            transform.position = MatchRoleManager.Instance.mainGateTransform.position;
        }

        EnsureGateComponents();
        BuildButton();

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.KeysCollected.OnValueChanged += OnKeysCollectedChanged;
            UpdateGateStatus(MatchRoleManager.Instance.KeysCollected.Value);
        }
        else
        {
            UpdateGateStatus(0);
        }
    }

    private int lastCachedKeyCount = -1;

    private void Update()
    {
        int currentKeys = MatchRoleManager.Instance != null ? MatchRoleManager.Instance.KeysCollected.Value : 0;
        if (currentKeys != lastCachedKeyCount)
        {
            lastCachedKeyCount = currentKeys;
            UpdateGateStatus(currentKeys);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.KeysCollected.OnValueChanged -= OnKeysCollectedChanged;
        }
        if (buttonGO != null) Destroy(buttonGO);
    }

    private void EnsureGateComponents()
    {
        if (solidGateCollider == null)
        {
            solidGateCollider = GetComponent<Collider2D>();
            if (solidGateCollider == null)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(3.5f, 1.2f);
                box.isTrigger = false; // Solid barrier until unlocked
                solidGateCollider = box;
            }
        }

        // Trigger collider for interaction/escape when unlocked
        BoxCollider2D triggerBox = gameObject.AddComponent<BoxCollider2D>();
        triggerBox.size = new Vector2(4.5f, 2.0f);
        triggerBox.isTrigger = true;

        // Label above gate
        Transform lblTrans = transform.Find("GateStatusLabel");
        if (lblTrans == null)
        {
            GameObject txtGO = new GameObject("GateStatusLabel");
            txtGO.transform.SetParent(transform, false);
            txtGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            statusLabel = txtGO.AddComponent<TextMeshPro>();
            statusLabel.fontSize = 2.4f;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.sortingOrder = 200;
        }
        else
        {
            statusLabel = lblTrans.GetComponent<TextMeshPro>();
        }
    }

    private void OnKeysCollectedChanged(int oldVal, int newVal)
    {
        UpdateGateStatus(newVal);
    }

    public void UpdateGateStatus(int keysCollected)
    {
        if (isUnlocked)
        {
            if (solidGateCollider != null) solidGateCollider.enabled = false; // Open gate barrier
            if (statusLabel != null)
            {
                statusLabel.text = "<color=green>🔓 MAIN GATE UNLOCKED!\nHOSTAGES ESCAPE HERE!</color>";
            }
        }
        else
        {
            if (solidGateCollider != null) solidGateCollider.enabled = true; // Lock gate barrier
            if (statusLabel != null)
            {
                if (keysCollected >= requiredKeyCount)
                    statusLabel.text = $"<color=yellow>🔒 MAIN GATE READY ({keysCollected}/{requiredKeyCount})\nPRESS BUTTON TO UNLOCK!</color>";
                else
                    statusLabel.text = $"<color=red>🔒 MAIN GATE (LOCKED)\nKEYS NEEDED: {keysCollected}/{requiredKeyCount}</color>";
            }
        }
    }

    // ── Runtime UI Button ─────────────────────────────────────────────────────

    private void BuildButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        buttonGO = new GameObject($"GateActionBtn_{gameObject.name}", typeof(RectTransform));
        buttonGO.transform.SetParent(canvas.transform, false);
        buttonGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rt  = buttonGO.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(180f, 60f);
        rt.anchorMin        = new Vector2(0.5f, 0.18f);
        rt.anchorMax        = new Vector2(0.5f, 0.18f);
        rt.anchoredPosition = Vector2.zero;

        Image bg  = buttonGO.AddComponent<Image>();
        bg.color  = new Color(0.05f, 0.05f, 0.05f, 0.88f);

        button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(OnGateButtonPressed);

        ColorBlock cb       = button.colors;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.pressedColor     = new Color(0.5f, 0.5f, 0.5f, 1f);
        button.colors       = cb;

        GameObject lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(buttonGO.transform, false);
        lblGO.layer = LayerMask.NameToLayer("UI");

        RectTransform lrt    = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin        = Vector2.zero;
        lrt.anchorMax        = Vector2.one;
        lrt.sizeDelta        = Vector2.zero;
        lrt.anchoredPosition = Vector2.zero;

        buttonLabel           = lblGO.AddComponent<TextMeshProUGUI>();
        buttonLabel.text      = "OPEN GATE";
        buttonLabel.fontSize  = 20f;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color     = Color.white;

        Outline o        = buttonGO.AddComponent<Outline>();
        o.effectColor    = new Color(0.1f, 0.9f, 0.2f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);

        buttonGO.SetActive(false);
    }

    private void SetButtonVisible(bool show)
    {
        if (buttonGO != null)
        {
            if (show)
            {
                buttonGO.transform.SetAsLastSibling();
                if (buttonLabel != null)
                {
                    buttonLabel.text = isUnlocked ? "ESCAPE" : "OPEN GATE";
                }
            }
            buttonGO.SetActive(show);
        }
    }

    private void OnGateButtonPressed()
    {
        if (localPlayer == null) return;

        int currentKeys = MatchRoleManager.Instance != null ? MatchRoleManager.Instance.KeysCollected.Value : 0;

        if (!isUnlocked)
        {
            if (currentKeys >= requiredKeyCount)
            {
                isUnlocked = true;
                if (solidGateCollider != null) solidGateCollider.enabled = false;
                UpdateGateStatus(currentKeys);

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("<color=green>🔓 MAIN GATE UNLOCKED! Walk through to Escape!</color>");
                }
                Debug.Log("[MainGateController] Player manually pressed button to UNLOCK Main Gate!");
                SetButtonVisible(true); // Updates label to ESCAPE
            }
            else
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification($"<color=red>🔒 MAIN GATE IS LOCKED! NEED ALL KEYS ({currentKeys}/{requiredKeyCount}) TO OPEN!</color>");
                }
            }
            return;
        }

        // Handle Escape when gate is unlocked
        TriggerPlayerEscape(localPlayer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) player = other.GetComponent<PlayerController>();

        if (player != null && (player.IsOwner || player.IsLocal))
        {
            localPlayer = player;
            SetButtonVisible(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) player = other.GetComponent<PlayerController>();

        if (player != null && (player.IsOwner || player.IsLocal))
        {
            SetButtonVisible(false);
            localPlayer = null;
        }
    }

    private void TriggerPlayerEscape(PlayerController player)
    {
        if (player == null) return;

        if (player.playerRole.Value == PlayerRole.Hostage)
        {
            Debug.Log($"[MainGateController] Hostage '{player.playerName.Value}' escaped through Main Gate! ESCAPE SUCCESSFUL!");
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("<color=yellow>🏆 ESCAPE SUCCESSFUL! Hostages Win!</color>");
            }
        }
        else if (player.playerRole.Value == PlayerRole.Thief)
        {
            bool treasureStolen = MatchRoleManager.Instance != null && MatchRoleManager.Instance.TreasureStolen.Value;
            if (treasureStolen)
            {
                Debug.Log($"[MainGateController] Thief '{player.playerName.Value}' escaped with Treasure! ESCAPE SUCCESSFUL!");
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("<color=gold>🏆 ESCAPE SUCCESSFUL! Thief Escaped with Treasure! Thieves Win!</color>");
                }
            }
        }
    }
}
