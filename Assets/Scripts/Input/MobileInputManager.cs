using UnityEngine;
using UnityEngine.UI;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance { get; private set; }

    [Header("Joysticks")]
    [SerializeField] private Joystick moveJoystick;
    [SerializeField] private Joystick aimJoystick;

    [Header("Buttons")]
    [SerializeField] private Button shootButton;
    [SerializeField] private Button reloadButton;

    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerAiming playerAiming;
    [SerializeField] private WeaponController weaponController;

    [Header("Editor Testing")]
    [SerializeField] private bool useKeyboardInEditor = true;

    [Header("Mini Militia Aim & Fire Joystick Setup")]
    [Tooltip("Inner joystick threshold to start aiming")]
    [SerializeField] private float aimThreshold = 0.12f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Programmatically links dynamically spawned local player components.
    /// </summary>
    public void SetLocalPlayer(PlayerController controller, PlayerAiming aiming, WeaponController weapon)
    {
        playerController = controller;
        playerAiming = aiming;
        weaponController = weapon;
        Debug.Log("[MobileInputManager] Local player references registered successfully.");
    }

    /// <summary>
    /// Enables or disables mobile joysticks & buttons during story intro cutscene.
    /// </summary>
    public void SetControlsActive(bool active)
    {
        if (moveJoystick != null) moveJoystick.gameObject.SetActive(active);
        if (aimJoystick != null) aimJoystick.gameObject.SetActive(active);
        if (shootButton != null) shootButton.gameObject.SetActive(active);
        if (reloadButton != null) reloadButton.gameObject.SetActive(active);

        if (playerController != null && !active)
        {
            playerController.SetMoveInput(Vector2.zero);
        }
    }


    private void Start()
    {
        ScreenAndUIScaler.EnforceLandscapeOrientation();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) ScreenAndUIScaler.ConfigureCanvas(canvas);

        AutoFindJoysticks();
        AutoFindOrCreateShootButton();

        if (reloadButton != null)
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
    }

    private void AutoFindOrCreateShootButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (shootButton == null && canvas != null)
        {
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                string bName = b.gameObject.name.ToLower();
                if (bName.Contains("shoot") || bName.Contains("fire") || bName.Contains("attack"))
                {
                    shootButton = b;
                    break;
                }
            }

            if (shootButton == null)
            {
                // Create dedicated Shoot Button dynamically on HUD Canvas
                GameObject btnGO = new GameObject("ShootButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(canvas.transform, false);

                RectTransform rt = btnGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-60f, 170f); // Positioned clearly on the right side above aim joystick
                rt.sizeDelta = new Vector2(90f, 90f);

                Image img = btnGO.GetComponent<Image>();
                img.color = new Color(0.9f, 0.25f, 0.2f, 0.85f); // Red bullet fire button

                GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textGO.transform.SetParent(btnGO.transform, false);
                RectTransform textRt = textGO.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one; textRt.sizeDelta = Vector2.zero;

                TMPro.TextMeshProUGUI tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
                tmp.text = "FIRE";
                tmp.fontSize = 22;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.white;

                shootButton = btnGO.GetComponent<Button>();
            }
        }

        if (shootButton != null)
        {
            shootButton.gameObject.SetActive(true);

            // Add EventTrigger for PointerDown and PointerUp to handle continuous firing when held down!
            var trigger = shootButton.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = shootButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => OnShootButtonPressed());
            trigger.triggers.Add(pointerDown);

            var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) => OnShootButtonReleased());
            trigger.triggers.Add(pointerUp);
        }
    }

    private void AutoFindJoysticks()
    {
        if (moveJoystick == null || aimJoystick == null)
        {
            Joystick[] joysticks = FindObjectsOfType<Joystick>(true);
            foreach (var j in joysticks)
            {
                if (j == null) continue;
                string jName = j.gameObject.name.ToLower();
                if (moveJoystick == null && (jName.Contains("move") || jName.Contains("left")))
                {
                    moveJoystick = j;
                }
                else if (aimJoystick == null && (jName.Contains("aim") || jName.Contains("right")))
                {
                    aimJoystick = j;
                }
            }

            // Fallback by index if naming convention is missing
            if (joysticks.Length >= 1 && moveJoystick == null) moveJoystick = joysticks[0];
            if (joysticks.Length >= 2 && aimJoystick == null) aimJoystick = joysticks[1];
        }
    }

    private void Update()
    {
        // Auto-find local player if reference is lost, null, or not local
        if (playerController == null || !playerController.IsLocal)
        {
            FindLocalPlayer();
        }

        if (moveJoystick == null || aimJoystick == null)
        {
            AutoFindJoysticks();
        }

        ProcessInput();
    }

    private void FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && p.IsLocal)
            {
                SetLocalPlayer(
                    p,
                    p.GetComponent<PlayerAiming>(),
                    p.GetComponent<WeaponController>()
                );
                break;
            }
        }

        // Fallback: Link to singleplayer/local player if IsLocal flag is initializing
        if (playerController == null && players.Length > 0)
        {
            SetLocalPlayer(
                players[0],
                players[0].GetComponent<PlayerAiming>(),
                players[0].GetComponent<WeaponController>()
            );
        }
    }


    private void ProcessInput()
    {
        Vector2 moveInput = Vector2.zero;
        Vector2 aimInput  = Vector2.zero;

        // 1. Read On-Screen Joysticks First
        if (moveJoystick != null)
        {
            Vector2 joyMove = new Vector2(moveJoystick.Horizontal, moveJoystick.Vertical);
            if (joyMove.magnitude > 0.05f)
            {
                moveInput = joyMove;
            }
        }

        if (aimJoystick != null)
        {
            Vector2 joyAim = new Vector2(aimJoystick.Horizontal, aimJoystick.Vertical);
            if (joyAim.magnitude > aimThreshold)
            {
                aimInput = joyAim;
            }
        }

#if UNITY_EDITOR
        // 2. Editor Keyboard & Mouse Fallback if Joysticks are Idle
        if (useKeyboardInEditor)
        {
            if (moveInput == Vector2.zero)
            {
                moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }

            if (aimInput == Vector2.zero && Camera.main != null && playerAiming != null)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2 dir = (Vector2)(mousePos - playerAiming.transform.position);
                if (dir.magnitude > 0.1f)
                {
                    aimInput = dir.normalized;
                }
            }

            if (Input.GetMouseButtonDown(0)) weaponController?.StartFiring();
            if (Input.GetMouseButtonUp(0))   weaponController?.StopFiring();
            if (Input.GetKeyDown(KeyCode.R)) weaponController?.StartReload();
        }
#endif

        playerController?.SetMoveInput(moveInput);
        playerAiming?.SetAimInput(aimInput);
    }

    private void OnReloadButtonPressed()
    {
        weaponController?.StartReload();
    }

    // Called by MobileButtonHandler pointer events (continuous fire support)
    public void OnShootButtonPressed()
    {
        weaponController?.StartFiring();
    }

    public void OnShootButtonReleased()
    {
        weaponController?.StopFiring();
    }

    /// <summary>
    /// Disables Aim joystick and action buttons for ghost spectating while keeping Move joystick active.
    /// </summary>
    public void SetGhostUI(bool isGhost)
    {
        if (moveJoystick != null)
        {
            moveJoystick.gameObject.SetActive(true); // Move joystick remains active for ghost spectating!
        }
        if (aimJoystick != null)
        {
            aimJoystick.gameObject.SetActive(!isGhost); // Disable Aim joystick
        }
        if (shootButton != null) shootButton.gameObject.SetActive(!isGhost);
        if (reloadButton != null) reloadButton.gameObject.SetActive(!isGhost);
    }
}
