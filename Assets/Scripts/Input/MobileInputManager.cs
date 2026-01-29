using UnityEngine;
using UnityEngine.UI;

public class MobileInputManager : MonoBehaviour
{
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
    
    private void Start()
    {
        // Setup button listeners
        if (shootButton != null)
        {
            // Add MobileButtonHandler if it doesn't exist and subscribe to down/up events
            MobileButtonHandler handler = shootButton.GetComponent<MobileButtonHandler>();
            if (handler == null) handler = shootButton.gameObject.AddComponent<MobileButtonHandler>();
            
            handler.OnPointerDownEvent.AddListener(OnShootButtonPressed);
            handler.OnPointerUpEvent.AddListener(OnShootButtonReleased);
        }
        
        if (reloadButton != null)
        {
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
        }
    }
    
    private void Update()
    {
        ProcessInput();
    }
    
    private void ProcessInput()
    {
        Vector2 moveInput = Vector2.zero;
        Vector2 aimInput = Vector2.zero;
        
#if UNITY_EDITOR
        if (useKeyboardInEditor)
        {
            // Keyboard input for testing in editor
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            
            // Use mouse for aiming
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 aimDir = (mousePos - playerAiming.transform.position).normalized;
            aimInput = aimDir;
            
            // Mouse button for shooting
            if (Input.GetMouseButtonDown(0))
            {
                weaponController?.StartFiring();
            }
            if (Input.GetMouseButtonUp(0))
            {
                weaponController?.StopFiring();
            }
            
            // R for reload
            if (Input.GetKeyDown(KeyCode.R))
            {
                weaponController?.StartReload();
            }
        }
        else
#endif
        {
            // Mobile joystick input
            if (moveJoystick != null)
            {
                moveInput = new Vector2(moveJoystick.Horizontal, moveJoystick.Vertical);
            }
            
            if (aimJoystick != null)
            {
                aimInput = new Vector2(aimJoystick.Horizontal, aimJoystick.Vertical);
            }
        }
        
        // Send input to controllers
        if (playerController != null)
        {
            playerController.SetMoveInput(moveInput);
        }
        
        if (playerAiming != null)
        {
            playerAiming.SetAimInput(aimInput);
        }
    }
    
    private void OnShootButtonDown()
    {
        if (weaponController != null)
        {
            weaponController.StartFiring();
        }
    }
    
    private void OnReloadButtonPressed()
    {
        if (weaponController != null)
        {
            weaponController.StartReload();
        }
    }
    
    // Called by UI button events (for continuous fire)
    public void OnShootButtonPressed()
    {
        weaponController?.StartFiring();
    }
    
    public void OnShootButtonReleased()
    {
        weaponController?.StopFiring();
    }
}
