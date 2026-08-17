using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class SafeController : NetworkBehaviour
{
    public static SafeController Instance { get; private set; }

    public enum SafeState
    {
        Closed,     // Locked, closed safe door
        OpenFilled, // Opened safe containing shiny treasure inside
        OpenEmpty   // Opened safe after treasure has been stolen
    }

    [Header("Safe State")]
    public SafeState currentState = SafeState.Closed;
    public bool isUnlocked = false;

    [Header("Custom Sprites (Drag & Drop in Inspector)")]
    [Tooltip("Sprite for Closed / Locked Safe. If null, procedural sprite is generated.")]
    public Sprite closedSafeSprite;

    [Tooltip("Sprite for Open Safe containing Treasure. If null, procedural sprite is generated.")]
    public Sprite openFilledSafeSprite;

    [Tooltip("Sprite for Open Safe empty after treasure is stolen. If null, procedural sprite is generated.")]
    public Sprite openEmptySafeSprite;

    private SpriteRenderer safeSpriteRenderer;
    private TextMeshPro statusLabel;
    private Collider2D triggerCollider;

    private GameObject buttonGO;
    private Button button;
    private TextMeshProUGUI buttonLabel;
    private PlayerController localPlayer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        EnsureSafeComponents();
        BuildButton();

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.TreasureStolen.OnValueChanged += OnTreasureStolenChanged;
            MatchRoleManager.Instance.IsSafeOpened.OnValueChanged += OnSafeOpenedChanged;

            EvaluateInitialState();
        }
        else
        {
            SetSafeState(currentState);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.TreasureStolen.OnValueChanged -= OnTreasureStolenChanged;
            MatchRoleManager.Instance.IsSafeOpened.OnValueChanged -= OnSafeOpenedChanged;
        }
        if (buttonGO != null) Destroy(buttonGO);
    }

    private void EvaluateInitialState()
    {
        if (MatchRoleManager.Instance == null)
        {
            SetSafeState(currentState);
            return;
        }

        if (MatchRoleManager.Instance.TreasureStolen.Value)
        {
            SetSafeState(SafeState.OpenEmpty);
        }
        else if (MatchRoleManager.Instance.IsSafeOpened.Value)
        {
            SetSafeState(SafeState.OpenFilled);
        }
        else
        {
            SetSafeState(SafeState.Closed);
        }
    }

    private void OnTreasureStolenChanged(bool oldVal, bool newVal)
    {
        if (newVal) SetSafeState(SafeState.OpenEmpty);
    }

    private void OnSafeOpenedChanged(bool oldVal, bool newVal)
    {
        if (newVal && currentState != SafeState.OpenEmpty)
        {
            SetSafeState(SafeState.OpenFilled);
        }
    }

    public void SetSafeState(SafeState newState)
    {
        currentState = newState;
        isUnlocked = (newState != SafeState.Closed);

        if (safeSpriteRenderer == null) safeSpriteRenderer = GetComponent<SpriteRenderer>();
        if (safeSpriteRenderer != null)
        {
            safeSpriteRenderer.sprite = GetSpriteForState(newState);
        }

        if (statusLabel != null)
        {
            switch (newState)
            {
                case SafeState.Closed:
                    statusLabel.text = "<color=gold>🔒 SAFE (LOCKED)\n🔑 NEEDS SAFE KEY</color>";
                    break;
                case SafeState.OpenFilled:
                    statusLabel.text = "<color=yellow>🔓 SAFE OPENED!\n💎 TREASURE INSIDE</color>";
                    break;
                case SafeState.OpenEmpty:
                    statusLabel.text = "<color=green>🔓 SAFE OPENED (EMPTY)\n💎 TREASURE STOLEN!</color>";
                    break;
            }
        }

        if (currentState == SafeState.OpenEmpty)
        {
            SetButtonVisible(false);
        }
    }

    private Sprite GetSpriteForState(SafeState state)
    {
        switch (state)
        {
            case SafeState.Closed:
                return closedSafeSprite != null ? closedSafeSprite : CreateProceduralClosedSafeSprite();
            case SafeState.OpenFilled:
                return openFilledSafeSprite != null ? openFilledSafeSprite : CreateProceduralOpenFilledSafeSprite();
            case SafeState.OpenEmpty:
                return openEmptySafeSprite != null ? openEmptySafeSprite : CreateProceduralOpenEmptySafeSprite();
            default:
                return closedSafeSprite != null ? closedSafeSprite : CreateProceduralClosedSafeSprite();
        }
    }

    private void EnsureSafeComponents()
    {
        safeSpriteRenderer = GetComponent<SpriteRenderer>();
        if (safeSpriteRenderer == null) safeSpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        safeSpriteRenderer.sprite = GetSpriteForState(currentState);
        safeSpriteRenderer.sortingOrder = 50;

        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2.5f, 2.5f);
            box.isTrigger = true;
            triggerCollider = box;
        }

        Transform lblTrans = transform.Find("SafeStatusLabel");
        if (lblTrans == null)
        {
            GameObject txtGO = new GameObject("SafeStatusLabel");
            txtGO.transform.SetParent(transform, false);
            txtGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            statusLabel = txtGO.AddComponent<TextMeshPro>();
            statusLabel.fontSize = 2.2f;
            statusLabel.fontStyle = FontStyles.Bold;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.sortingOrder = 200;
        }
        else
        {
            statusLabel = lblTrans.GetComponent<TextMeshPro>();
        }

        SetSafeState(currentState);
    }

    // ── Procedural Safe Sprites Generators ────────────────────────────────────

    private Sprite CreateProceduralClosedSafeSprite()
    {
        Texture2D tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        Color steel = new Color(0.25f, 0.28f, 0.32f, 1f);
        Color darkSteel = new Color(0.15f, 0.18f, 0.22f, 1f);
        Color goldDial = new Color(1f, 0.82f, 0.1f, 1f);

        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                if (x < 3 || x >= 45 || y < 3 || y >= 45)
                    tex.SetPixel(x, y, darkSteel);
                else
                    tex.SetPixel(x, y, steel);
            }
        }

        Vector2 dialCenter = new Vector2(24f, 24f);
        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), dialCenter);
                if (dist <= 8f)
                {
                    tex.SetPixel(x, y, (dist > 6.5f) ? darkSteel : goldDial);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 16f);
    }

    private Sprite CreateProceduralOpenFilledSafeSprite()
    {
        Texture2D tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        Color darkSteel = new Color(0.15f, 0.18f, 0.22f, 1f);
        Color cavity = new Color(0.08f, 0.09f, 0.11f, 1f);
        Color gold = new Color(1f, 0.84f, 0.0f, 1f);
        Color diamondCyan = new Color(0.3f, 0.9f, 1f, 1f);
        Color doorSteel = new Color(0.35f, 0.38f, 0.42f, 1f);

        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                if (x < 4 || x >= 36 || y < 4 || y >= 44)
                    tex.SetPixel(x, y, darkSteel);
                else
                    tex.SetPixel(x, y, cavity);
            }
        }

        for (int y = 2; y < 46; y++)
        {
            for (int x = 36; x < 46; x++)
            {
                tex.SetPixel(x, y, doorSteel);
            }
        }

        for (int y = 6; y <= 22; y++)
        {
            for (int x = 10; x <= 30; x++)
            {
                tex.SetPixel(x, y, gold);
            }
        }
        for (int y = 20; y <= 32; y++)
        {
            for (int x = 16; x <= 24; x++)
            {
                tex.SetPixel(x, y, diamondCyan);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 16f);
    }

    private Sprite CreateProceduralOpenEmptySafeSprite()
    {
        Texture2D tex = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        Color darkSteel = new Color(0.15f, 0.18f, 0.22f, 1f);
        Color cavity = new Color(0.05f, 0.06f, 0.07f, 1f);
        Color doorSteel = new Color(0.35f, 0.38f, 0.42f, 1f);

        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                if (x < 4 || x >= 36 || y < 4 || y >= 44)
                    tex.SetPixel(x, y, darkSteel);
                else
                    tex.SetPixel(x, y, cavity);
            }
        }

        for (int y = 2; y < 46; y++)
        {
            for (int x = 36; x < 46; x++)
            {
                tex.SetPixel(x, y, doorSteel);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 16f);
    }

    // ── Runtime UI Button ─────────────────────────────────────────────────────

    private void BuildButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        buttonGO = new GameObject($"SafeActionBtn_{gameObject.name}", typeof(RectTransform));
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
        button.onClick.AddListener(OnSafeButtonPressed);

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
        buttonLabel.text      = "OPEN SAFE";
        buttonLabel.fontSize  = 20f;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color     = Color.white;

        Outline o        = buttonGO.AddComponent<Outline>();
        o.effectColor    = new Color(1f, 0.8f, 0.1f, 0.9f);
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
                    buttonLabel.text = (currentState == SafeState.Closed) ? "OPEN SAFE" : "STEAL TREASURE";
                }
            }
            buttonGO.SetActive(show);
        }
    }

    private void OnSafeButtonPressed()
    {
        if (localPlayer == null) return;
        HandleThiefSafeInteraction(localPlayer);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) player = other.GetComponent<PlayerController>();

        if (player != null && (player.IsOwner || player.IsLocal))
        {
            localPlayer = player;
            if (currentState != SafeState.OpenEmpty)
            {
                SetButtonVisible(true);
            }
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

    private void HandleThiefSafeInteraction(PlayerController thiefPlayer)
    {
        if (thiefPlayer == null) return;

        if (currentState == SafeState.Closed)
        {
            if (MatchRoleManager.Instance == null || !MatchRoleManager.Instance.SafeKeyCollectedByThief.Value)
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("<color=red>🔒 SAFE IS LOCKED! NEED SAFE KEY TO OPEN!</color>");
                }
                return;
            }

            // Step 1: Open the Safe (Closed -> OpenFilled)
            if (MatchRoleManager.Instance != null)
            {
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
                    MatchRoleManager.Instance.IsSafeOpened.Value = true;
                else
                    MatchRoleManager.Instance.OpenSafeServerRpc();
            }

            SetSafeState(SafeState.OpenFilled);

            if (HUDManager.Instance != null)
                HUDManager.Instance.ShowNotification("<color=yellow>🔓 SAFE UNLOCKED & OPENED! Press button again to Steal Treasure!</color>");

            SetButtonVisible(true);
        }
        else if (currentState == SafeState.OpenFilled)
        {
            // Step 2: Steal the Treasure (OpenFilled -> OpenEmpty)
            StealTreasure(thiefPlayer);
        }
    }

    private void StealTreasure(PlayerController thiefPlayer)
    {
        if (currentState == SafeState.OpenEmpty) return;

        SetSafeState(SafeState.OpenEmpty);

        Debug.Log($"[SafeController] Safe unlocked and Treasure stolen by Thief '{thiefPlayer.playerName.Value}'!");

        if (MatchRoleManager.Instance != null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
            {
                MatchRoleManager.Instance.TreasureStolen.Value = true;
            }
            else
            {
                MatchRoleManager.Instance.StealTreasureServerRpc();
            }
        }

        // Trigger golden blast FX at Safe position
        ProceduralEffectsGenerator.CreateStunBlast(transform.position, 4f);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("<color=gold>💎 TREASURE STOLEN! Collect exit keys to unlock Main Gate & Escape!</color>");
        }

        SetButtonVisible(false);
    }
}
