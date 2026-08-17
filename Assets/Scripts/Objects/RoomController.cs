using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Place this script INSIDE the room.
///
/// Assign in Inspector:
///   - roomDisplayName : human-readable name shown in HUD (e.g. "Kitchen")
///   - linkedDoor      : drag in the DoorController placed near the entrance door outside
///   - exitBoxCollider : drag your BoxCollider2D here.
///                       Used for BOTH teleporting into the room and displaying the Exit button.
///   - exitButton      : (Optional) drag a UI Canvas Button directly from your Scene hierarchy.
///                       If assigned, this UI button will be managed directly (shown/hidden/clicked).
///                       If left unassigned, a fallback button is generated at runtime.
/// </summary>
public class RoomController : MonoBehaviour
{
    [Header("Room Info")]
    [Tooltip("Display name shown in HUD, e.g. 'Kitchen', 'Rooftop'.")]
    public string roomDisplayName = "Room";

    [Header("Door Link")]
    [Tooltip("Drag in the DoorController placed near the entrance door outside.")]
    public DoorController linkedDoor;

    [Header("Exit Box Collider")]
    [Tooltip("Drag your BoxCollider2D here.\n" +
             "Used for BOTH spawning/teleporting inside the room AND showing the Exit button when standing in it.")]
    public BoxCollider2D exitBoxCollider;

    [Header("Prompt")]
    public string exitPromptText = "Exit";

    // Set by DoorController automatically
    [HideInInspector] public string roomId = "";

    // ─────────────────────────────────────────────────────────────────────────

    private GameObject      buttonGO;
    private Button          button;
    private TextMeshProUGUI buttonLabel;
    private PlayerController localPlayer;
    private bool            isButtonCurrentlyShowing = false;

    private Button GetExitButton() => GameManager.Instance != null ? GameManager.Instance.exitButton : null;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureExitBox();
    }

    private void Start()
    {
        EnsureExitBox();

        Button targetBtn = GetExitButton();
        if (targetBtn != null)
        {
            targetBtn.onClick.RemoveListener(OnExitPressed);
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
        Button targetBtn = GetExitButton();
        if (targetBtn != null)
        {
            targetBtn.onClick.RemoveListener(OnExitPressed);
        }
        if (buttonGO != null) Destroy(buttonGO);
    }

    private void EnsureExitBox()
    {
        if (exitBoxCollider == null)
        {
            exitBoxCollider = GetComponent<BoxCollider2D>();
        }

        if (exitBoxCollider == null)
        {
            exitBoxCollider = GetComponentInChildren<BoxCollider2D>();
        }

        if (exitBoxCollider != null)
        {
            exitBoxCollider.isTrigger = true;
        }
    }

    /// <summary>
    /// Creates a small world-space text label inside the exit door section/trigger
    /// (always visible in Game view so players know which exit door/room it is).
    /// </summary>
    private void BuildWorldLabel()
    {
        string displayName = !string.IsNullOrEmpty(roomDisplayName) ? roomDisplayName : roomId;
        if (string.IsNullOrEmpty(displayName)) return;

        Transform existing = transform.Find("RoomWorldLabel");
        if (existing != null) Destroy(existing.gameObject);

        GameObject labelGO = new GameObject("RoomWorldLabel");
        labelGO.transform.SetParent(transform, false);

        EnsureExitBox();
        if (exitBoxCollider != null)
        {
            labelGO.transform.localPosition = exitBoxCollider.offset;
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

    /// <summary>
    /// Returns the spawn position inside the room (center of exitBoxCollider, or transform position).
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        EnsureExitBox();

        if (exitBoxCollider != null)
        {
            return exitBoxCollider.bounds.center;
        }

        return transform.position;
    }

    /// <summary>
    /// Returns true if the player's 2D position is inside the exitBoxCollider bounds.
    /// </summary>
    public bool IsPlayerInExitBox(Vector3 worldPos)
    {
        EnsureExitBox();

        if (exitBoxCollider == null)
        {
            Vector3 exitPos = linkedDoor != null ? linkedDoor.transform.position : transform.position;
            return Vector3.Distance(worldPos, exitPos) <= 1.8f;
        }

        Vector2 p = worldPos;

        // 1. Check exact 2D Physics OverlapPoint
        if (exitBoxCollider.OverlapPoint(p))
            return true;

        // 2. Check 2D bounding box (ignores Z coordinate completely)
        Bounds b = exitBoxCollider.bounds;
        return (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y);
    }

    private void Update()
    {
        // Locate local player if null
        if (localPlayer == null)
        {
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            foreach (var p in players)
            {
                if (p != null && p.IsLocal)
                {
                    localPlayer = p;
                    break;
                }
            }
        }

        if (localPlayer == null)
        {
            if (isButtonCurrentlyShowing) SetButtonVisible(false);
            return;
        }

        // Only show Exit button if local player is inside the designated exitBoxCollider
        bool isAtExit = IsPlayerInExitBox(localPlayer.transform.position);
        if (isAtExit != isButtonCurrentlyShowing)
        {
            SetButtonVisible(isAtExit);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void OnExitPressed()
    {
        if (localPlayer == null || linkedDoor == null) return;

        // Teleport player back to the DoorController's position (= outside in the world)
        localPlayer.transform.position = linkedDoor.transform.position;

        GameManager.Instance?.SetCurrentRoom(null);

        if (HUDManager.Instance != null)
            HUDManager.Instance.ShowNotification($"⬅ Left {roomDisplayName}");

        Debug.Log($"[RoomController] Local player exited room '{roomId}'.");
        SetButtonVisible(false);

        // Tell the door to clean up its state
        linkedDoor.NotifyPlayerExited();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime UI — fallback if no custom exitButton is assigned

    private void BuildButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        buttonGO = new GameObject($"RoomExitBtn_{gameObject.name}", typeof(RectTransform));
        buttonGO.transform.SetParent(canvas.transform, false);
        buttonGO.layer = LayerMask.NameToLayer("UI");

        RectTransform rt  = buttonGO.GetComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(160f, 60f);
        rt.anchorMin        = new Vector2(0.5f, 0.18f);
        rt.anchorMax        = new Vector2(0.5f, 0.18f);
        rt.anchoredPosition = Vector2.zero;

        Image bg  = buttonGO.AddComponent<Image>();
        bg.color  = new Color(0.05f, 0.05f, 0.05f, 0.88f);

        button = buttonGO.AddComponent<Button>();
        button.onClick.AddListener(OnExitPressed);

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
        buttonLabel.text      = exitPromptText;
        buttonLabel.fontSize  = 22f;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color     = Color.white;

        Outline o        = buttonGO.AddComponent<Outline>();
        o.effectColor    = new Color(1f, 0.35f, 0.1f, 0.9f);   // Orange tint for exit
        o.effectDistance = new Vector2(2f, -2f);

        buttonGO.SetActive(false);
    }

    private void SetButtonVisible(bool show)
    {
        isButtonCurrentlyShowing = show;

        Button targetBtn = GetExitButton();
        if (targetBtn != null)
        {
            if (show)
            {
                targetBtn.onClick.RemoveListener(OnExitPressed);
                targetBtn.onClick.AddListener(OnExitPressed);
                targetBtn.transform.SetAsLastSibling();
            }
            else
            {
                targetBtn.onClick.RemoveListener(OnExitPressed);
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

    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        BoxCollider2D box = exitBoxCollider != null ? exitBoxCollider : GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.3f);
            Gizmos.DrawCube(box.bounds.center, box.bounds.size);
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 1f);
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
        }
        else
        {
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.8f);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }

#if UNITY_EDITOR
        string lbl = $"ROOM [{roomDisplayName}] - EXIT BOX";
        UnityEditor.Handles.color = new Color(1f, 0.25f, 0.05f, 1f);
        Vector3 labelPos = box != null ? box.bounds.center : transform.position;
        UnityEditor.Handles.Label(labelPos, lbl);
#endif
    }
}
