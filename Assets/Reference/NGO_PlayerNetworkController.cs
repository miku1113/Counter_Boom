using UnityEngine;
using Unity.Netcode;

public class NGO_PlayerNetworkController : NetworkBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 3f;
    public KeyCode runKey = KeyCode.LeftShift;

    [Header("References")]
    public Transform head;
    public Transform eyes;
    public Animator animator;
    public SpriteRenderer bodySprite;
    public Transform visuals;

    [Header("Eye Settings")]
    [Range(0.03f, 0.5f)] public float eyeMoveRadius = 0.05f;
    public float eyeLerpSpeed = 12f;

    [Header("Facing")]
    public bool defaultFacesLeft = true;

    [Header("Dust (Landing effect)")]
    public ParticleSystem moveDust;

    private Rigidbody2D rb;
    private Camera cam;
    private Vector2 moveInput;

    private NetworkVariable<bool> networkFacingRight = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> networkIsJumping = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private bool facingRight;
    private bool isJumping;
    private float jumpTimer;
    private bool hasLanded;
    private Vector3 eyesDefaultLocalPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        if (eyes == null && head != null)
            eyes = head.Find("Eyes");

        if (eyes != null)
            eyesDefaultLocalPos = eyes.localPosition;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            facingRight = !defaultFacesLeft;
            UpdateScale();
        }
        else
        {
            networkFacingRight.OnValueChanged += (oldVal, newVal) => {
                facingRight = newVal;
                UpdateScale();
            };
        }
    }

    private void Update()
    {
        if (IsOwner)
        {
            HandleLocalInput();
            UpdateAnimator();
            UpdateEyes();
            HandleMouseFacing();
        }
        else
        {
            if (networkIsJumping.Value)
                UpdateJumpVisual();
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            HandleLocalMovement();
        }
    }

    private void HandleLocalInput()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        if (Input.GetKeyDown(KeyCode.Space) && !isJumping)
            StartJump();

        if (isJumping)
            UpdateJumpVisual();
    }

    private void HandleLocalMovement()
    {
        bool isRunning = Input.GetKey(runKey);
        float speed = isRunning ? runSpeed : walkSpeed;

        Vector2 move = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
        rb.MovePosition(rb.position + move * speed * Time.fixedDeltaTime);
    }

    private void UpdateAnimator()
    {
        if (!animator) return;
        bool isRunning = Input.GetKey(runKey);
        animator.SetFloat("Speed", moveInput.magnitude);
        animator.SetBool("IsRunning", isRunning);
    }

    private void UpdateEyes()
    {
        if (eyes == null || cam == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Vector3 origin = (head != null) ? head.position : transform.position;
        Vector3 dirWorld = (mouseWorld - origin);

        if (dirWorld.sqrMagnitude < 0.0001f)
        {
            eyes.localPosition = Vector3.Lerp(eyes.localPosition, eyesDefaultLocalPos, Time.deltaTime * eyeLerpSpeed);
            return;
        }

        Vector3 dirWorldNorm = dirWorld.normalized;
        Transform eyesParent = eyes.parent ? eyes.parent : transform;
        Vector3 dirLocal = eyesParent.InverseTransformDirection(dirWorldNorm);

        Vector3 targetLocal = eyesDefaultLocalPos + new Vector3(dirLocal.x, dirLocal.y, 0f).normalized * eyeMoveRadius;
        eyes.localPosition = Vector3.Lerp(eyes.localPosition, targetLocal, Time.deltaTime * eyeLerpSpeed);
    }

    private void HandleMouseFacing()
    {
        if (cam == null || visuals == null) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(cam.transform.position.z);
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mousePos);
        mouseWorld.z = 0f;

        bool shouldFaceRight = mouseWorld.x > transform.position.x;
        if (shouldFaceRight != facingRight)
        {
            facingRight = shouldFaceRight;
            networkFacingRight.Value = facingRight;
            UpdateScale();
        }
    }

    private void UpdateScale()
    {
        Vector3 s = transform.localScale;
        float baseScale = Mathf.Abs(s.x);
        if (baseScale < 0.001f) baseScale = 1f;

        float targetScaleX = facingRight ? (defaultFacesLeft ? -baseScale : baseScale) 
                                       : (defaultFacesLeft ? baseScale : -baseScale);
        s.x = targetScaleX;
        transform.localScale = s;
    }

    private void StartJump()
    {
        isJumping = true;
        networkIsJumping.Value = true;
        hasLanded = false;
        jumpTimer = 0f;
    }

    private void UpdateJumpVisual()
    {
        jumpTimer += Time.deltaTime;
        float duration = 0.5f;
        float height = 0.3f;

        float normalizedTime = jumpTimer / duration;
        float yOffset = Mathf.Sin(normalizedTime * Mathf.PI) * height;

        if (visuals != null)
        {
            Vector3 pos = visuals.localPosition;
            pos.y = yOffset;
            visuals.localPosition = pos;
        }

        if (!hasLanded && jumpTimer >= duration)
        {
            hasLanded = true;
            if (moveDust != null) moveDust.Play();
        }

        if (jumpTimer >= duration)
        {
            isJumping = false;
            if (IsOwner) networkIsJumping.Value = false;
            if (visuals != null)
            {
                Vector3 pos = visuals.localPosition;
                pos.y = 0f;
                visuals.localPosition = pos;
            }
        }
    }
}
