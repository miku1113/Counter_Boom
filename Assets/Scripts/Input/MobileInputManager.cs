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
    [Tooltip("Inner joystick threshold to start aiming (0.05 allows instant aiming near center)")]
    [SerializeField] private float aimThreshold = 0.05f;
    [Tooltip("Outer joystick threshold to trigger weapon firing (0.85 gives 80% joystick area for aiming)")]
    [SerializeField] private float fireThreshold = 0.85f;
    private bool isFiringFromAimJoystick = false;

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
        isFiringFromAimJoystick = false;
        Debug.Log("[MobileInputManager] Local player references registered successfully.");
    }


    private void Start()
    {
        AutoFindJoysticks();

        if (shootButton != null)
        {
            // Shoot button is hidden as firing is now driven directly by the aim joystick
            shootButton.gameObject.SetActive(false);
        }

        if (reloadButton != null)
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
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

        // Trigger weapon firing when the aim joystick is pulled into the outer ring (fireThreshold)
        float aimMag = aimInput.magnitude;
        if (aimMag >= fireThreshold)
        {
            if (!isFiringFromAimJoystick)
            {
                isFiringFromAimJoystick = true;
                weaponController?.StartFiring();
            }
        }
        else
        {
            if (isFiringFromAimJoystick)
            {
                isFiringFromAimJoystick = false;
                weaponController?.StopFiring();
            }
        }

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
}
