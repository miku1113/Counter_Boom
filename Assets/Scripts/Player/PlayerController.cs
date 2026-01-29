using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    
    private Vector2 moveInput;
    private bool isMoving;
    
    private float defaultMoveSpeed;
    private Coroutine speedBoostCoroutine;
    
    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }
        
        // Ensure proper setup for top-down 2D game
        if (rb != null)
        {
            rb.gravityScale = 0f; // No gravity for top-down
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Don't rotate
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        else
        {
            Debug.LogError("[PlayerController] Rigidbody2D is missing! Add one to the Player GameObject.");
        }
        
        Debug.Log($"[PlayerController] Initialized on {gameObject.name}");
        defaultMoveSpeed = moveSpeed;

        // Camera Registration Logic
        RegisterCameraIfLocal();
    }
    
    private void RegisterCameraIfLocal()
    {
        bool isLocal = false;

        // 1. Check Unity Netcode (NGO)
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null)
        {
            if (netObj.IsLocalPlayer) isLocal = true;
        }
        // 2. Check Photon PUN
        else 
        {
            var photonView = GetComponent<Photon.Pun.PhotonView>();
            if (photonView != null)
            {
                if (photonView.IsMine) isLocal = true;
            }
            // 3. No Networking (Offline / Single Player)
            else
            {
                isLocal = true;
            }
        }

        if (isLocal)
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(transform);
            }
            else
            {
                // Fallback: Try to find it if Instance isn't set yet (e.g. execution order)
                var cam = FindObjectOfType<CameraController>();
                if (cam != null) cam.SetTarget(transform);
            }
        }
    }
    
    private void Start()
    {
        // Ensure animator is found if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }
    
    /// <summary>
    /// Called by input system to set movement direction
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        moveInput = input;
        isMoving = input.magnitude > 0.1f;
        
        // Update animator
        if (animator != null)
        {
            animator.SetBool("isWalking", isMoving);
            animator.SetFloat("moveSpeed", input.magnitude);
        }
    }
    
    private void FixedUpdate()
    {
        // Apply movement
        if (rb != null)
        {
            Vector2 velocity = moveInput * moveSpeed;
            rb.velocity = velocity;
        }
    }
    
    public bool IsMoving() => isMoving;

    public Vector2 GetMoveDirection() => moveInput.normalized;

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        if (speedBoostCoroutine != null) StopCoroutine(speedBoostCoroutine);
        speedBoostCoroutine = StartCoroutine(SpeedBoostRoutine(multiplier, duration));
    }

    private System.Collections.IEnumerator SpeedBoostRoutine(float multiplier, float duration)
    {
        moveSpeed = defaultMoveSpeed * multiplier;
        Debug.Log($"[PlayerController] Speed boosted to {moveSpeed} for {duration}s");
        yield return new WaitForSeconds(duration);
        moveSpeed = defaultMoveSpeed;
        speedBoostCoroutine = null;
        Debug.Log($"[PlayerController] Speed reset to {moveSpeed}");
    }
}
