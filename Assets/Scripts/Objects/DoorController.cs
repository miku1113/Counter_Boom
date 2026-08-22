using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Place this (+ a BoxCollider2D trigger) near the DOOR in the world.
///
/// Assign:
///   - roomId          : unique string ID for this room (e.g. "kitchen")
///   - linkedRoom      : drag in the RoomController that lives inside the room
///
/// How it works:
///   Player walks near the door  → "Enter" button appears
///   Player taps Enter           → teleported to linkedRoom's trigger position
///                                  GameManager.CurrentRoom updated
///   (Exit is handled by RoomController inside the room)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    [Header("Room Info")]
    [Tooltip("Unique ID for this room, e.g. 'kitchen', 'rooftop'. Used by GameManager to track current room.")]
    public string roomId = "room";

    [Header("Room Link")]
    [Tooltip("Drag in the RoomController that is placed inside the room this door leads to.")]
    public RoomController linkedRoom;

    [Header("Prompt")]
    public string promptText = "Enter";

    [Header("Key Requirement")]
    [Tooltip("If false (default) — door is freely openable, no key needed.\n" +
             "If true — player must carry the required key(s).")]
    public bool requiresKey = false;

    [Tooltip("Number of keys required to open this door (e.g. 1 or 2).")]
    public int requiredKeyCount = 1;

    [Tooltip("The item name or comma-separated key indices required (e.g. '1', '2', or '1,2').")]
    public string keyItemName = "1";

    // ─────────────────────────────────────────────────────────────────────────

    private GameObject      buttonGO;
    private Button          button;
    private TextMeshProUGUI buttonLabel;
    private PlayerController localPlayer;

    private Button GetEnterButton() => GameManager.Instance != null ? GameManager.Instance.enterButton : (OfflineManager.Instance != null ? OfflineManager.Instance.enterButton : null);

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Ensure trigger stays on — MapColliderFixer might run before this but Start re-enforces it
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // A Rigidbody2D (Kinematic) is required for OnTriggerEnter2D to fire reliably
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType  = RigidbodyType2D.Kinematic;
        rb.simulated = true;
    }

    private void Start()
    {
        // Re-enforce trigger AFTER MapColliderFixer has run (it runs at AfterSceneLoad)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // Pass our room ID to the RoomController
        if (linkedRoom != null)
        {
            linkedRoom.roomId      = roomId;
            linkedRoom.linkedDoor  = this;
        }

        Button targetBtn = GetEnterButton();
        if (targetBtn != null)
        {
            targetBtn.onClick.RemoveListener(OnEnterPressed);
            targetBtn.gameObject.SetActive(false);
        }
        else
        {
            BuildButton();
        }

        BuildWorldLabel();
    }

    private void OnDestroy()
    {
        Button targetBtn = GetEnterButton();
        if (targetBtn != null)
        {
            targetBtn.onClick.RemoveListener(OnEnterPressed);
        }
        if (buttonGO != null) Destroy(buttonGO);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>()
                           ?? other.GetComponentInParent<PlayerController>();
        if (pc == null || !pc.IsLocal) return;
        localPlayer = pc;
        SetButtonVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>()
                           ?? other.GetComponentInParent<PlayerController>();
        if (pc == null || !pc.IsLocal) return;
        SetButtonVisible(false);
        localPlayer = null;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnterPressed()
    {
        if (localPlayer == null && PlayerController.LocalPlayer != null)
        {
            localPlayer = PlayerController.LocalPlayer;
        }
        if (localPlayer == null && OfflineManager.Instance != null && OfflineManager.Instance.SpawnedPlayer != null)
        {
            localPlayer = OfflineManager.Instance.SpawnedPlayer.GetComponent<PlayerController>();
        }

        if (localPlayer == null || linkedRoom == null) return;

        // Key check — only if this door requires a key
        if (requiresKey && !PlayerHasKey())
        {
            int collected = GetCollectedKeysCount();
            if (HUDManager.Instance != null)
                HUDManager.Instance.ShowNotification($"🔒 Need all required keys ({collected}/{requiredKeyCount}) to open!");
            Debug.Log($"[DoorController] Player tried to enter '{roomId}' without all required keys ({collected}/{requiredKeyCount}).");
            return;
        }

        // Teleport player to a random empty point inside the room's collider bounds
        Vector3 targetPos = GetRandomPointInRoom(linkedRoom);
        localPlayer.Teleport(targetPos);

        GameManager.Instance?.SetCurrentRoom(linkedRoom);
        OfflineManager.Instance?.SetCurrentRoom(linkedRoom);

        if (HUDManager.Instance != null)
            HUDManager.Instance.ShowNotification($"➡ Entered {linkedRoom.roomDisplayName}");

        Debug.Log($"[DoorController] Local player entered room '{roomId}' at {targetPos}.");
        SetButtonVisible(false);
    }

    private Vector3 GetRandomPointInRoom(RoomController room)
    {
        if (room == null) return Vector3.zero;
        return room.GetSpawnPosition();
    }

    /// <summary>
    /// Returns the number of collected keys matching requirements.
    /// </summary>
    public int GetCollectedKeysCount()
    {
        if (MatchRoleManager.Instance != null && MatchRoleManager.Instance.KeysCollected.Value > 0)
        {
            return MatchRoleManager.Instance.KeysCollected.Value;
        }

        int count = 0;
        KeyItemPickup[] remaining = FindObjectsOfType<KeyItemPickup>();
        string[] targets = keyItemName.Split(',');

        foreach (string t in targets)
        {
            if (int.TryParse(t.Trim(), out int targetIndex))
            {
                bool isKeyInWorld = false;
                foreach (var k in remaining)
                {
                    if (k != null && k.keyIndex == targetIndex && k.gameObject.activeSelf)
                    {
                        isKeyInWorld = true;
                        break;
                    }
                }
                if (!isKeyInWorld) count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Returns true if the player's key condition is met.
    /// Always returns true when requiresKey = false (default).
    /// </summary>
    private bool PlayerHasKey()
    {
        if (!requiresKey) return true;

        if (MatchRoleManager.Instance != null && MatchRoleManager.Instance.KeysCollected.Value >= requiredKeyCount)
        {
            return true;
        }

        return GetCollectedKeysCount() >= requiredKeyCount;
    }

    // Callable by RoomController so the button re-shows if player exits and comes back
    public void NotifyPlayerExited()
    {
        localPlayer = null;
        SetButtonVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime UI — no prefab needed

    private void BuildButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        buttonGO = new GameObject($"DoorEnterBtn_{gameObject.name}", typeof(RectTransform));
        buttonGO.transform.SetParent(canvas.transform, false);
        buttonGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rt  = buttonGO.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(160f, 60f);
        rt.anchorMin        = new Vector2(0.5f, 0.18f);
        rt.anchorMax        = new Vector2(0.5f, 0.18f);
        rt.anchoredPosition = Vector2.zero;

        Image bg   = buttonGO.AddComponent<Image>();
        bg.color   = new Color(0.05f, 0.05f, 0.05f, 0.88f);

        button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(OnEnterPressed);

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
        buttonLabel.text      = promptText;
        buttonLabel.fontSize  = 22f;
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
        Button targetBtn = GetEnterButton();
        if (targetBtn != null)
        {
            if (show)
            {
                targetBtn.onClick.RemoveListener(OnEnterPressed);
                targetBtn.onClick.AddListener(OnEnterPressed);
                targetBtn.transform.SetAsLastSibling();
            }
            else
            {
                targetBtn.onClick.RemoveListener(OnEnterPressed);
            }
            targetBtn.gameObject.SetActive(show);
            return;
        }

        if (buttonGO != null)
        {
            if (show) buttonGO.transform.SetAsLastSibling(); // Always on top of every UI element
            buttonGO.SetActive(show);
        }
    }

    /// <summary>
    /// Creates a small world-space text label inside the door section/trigger
    /// (always visible in Game view so players know which room it leads to).
    /// </summary>
    private void BuildWorldLabel()
    {
        string displayName = linkedRoom != null ? linkedRoom.roomDisplayName : roomId;
        if (string.IsNullOrEmpty(displayName)) return;

        Transform existing = transform.Find("DoorWorldLabel");
        if (existing != null) Destroy(existing.gameObject);

        GameObject labelGO = new GameObject("DoorWorldLabel");
        labelGO.transform.SetParent(transform, false);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            labelGO.transform.localPosition = col.offset;
        }
        else
        {
            labelGO.transform.localPosition = Vector3.zero;
        }

        TextMeshPro tmp     = labelGO.AddComponent<TextMeshPro>();
        tmp.text      = displayName;
        tmp.fontSize  = 1.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(1f, 0.9f, 0.3f, 1f); // Golden yellow

        // Outline for readability against any background
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 1f);

        // World-space TMP uses a MeshRenderer — set sorting there
        MeshRenderer mr = labelGO.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "explotion"; // On top of all sprites
            mr.sortingOrder     = 999;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 0.2f, 0.25f);
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(0.1f, 0.8f, 0.2f, 1f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
            Gizmos.DrawSphere(transform.position, 0.5f);

#if UNITY_EDITOR
        string lbl = linkedRoom != null ? $"DOOR → {linkedRoom.roomDisplayName}" : $"DOOR [{roomId}]";
        UnityEditor.Handles.color = new Color(0.1f, 0.8f, 0.2f, 1f);
        Vector3 labelPos = col != null ? col.bounds.center : transform.position;
        UnityEditor.Handles.Label(labelPos, lbl);
#endif
    }
}
