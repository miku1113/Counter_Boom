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


    private void Start()
    {
        if (shootButton != null)
        {
            MobileButtonHandler handler = shootButton.GetComponent<MobileButtonHandler>();
            if (handler == null) handler = shootButton.gameObject.AddComponent<MobileButtonHandler>();

            handler.OnPointerDownEvent.AddListener(OnShootButtonPressed);
            handler.OnPointerUpEvent.AddListener(OnShootButtonReleased);
        }

        if (reloadButton != null)
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
    }

    private void Update()
    {
        // Auto-find local player if reference is lost or dynamically spawned late
        if (playerController == null)
        {
            FindLocalPlayer();
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

#if UNITY_EDITOR
        if (useKeyboardInEditor)
        {
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            // Mouse aim
            if (Camera.main != null && playerAiming != null)
            {
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                aimInput = ((Vector2)(mousePos - playerAiming.transform.position)).normalized;
            }

            if (Input.GetMouseButtonDown(0)) weaponController?.StartFiring();
            if (Input.GetMouseButtonUp(0))   weaponController?.StopFiring();
            if (Input.GetKeyDown(KeyCode.R)) weaponController?.StartReload();
        }
        else
#endif
        {
            if (moveJoystick != null)
                moveInput = new Vector2(moveJoystick.Horizontal, moveJoystick.Vertical);

            if (aimJoystick != null)
                aimInput = new Vector2(aimJoystick.Horizontal, aimJoystick.Vertical);
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
