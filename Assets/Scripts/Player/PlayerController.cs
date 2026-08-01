using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        writePerm: NetworkVariableWritePermission.Owner
    );

    private TextMeshPro nameTagTMP;

    [Header("Name Tag Settings")]
    [SerializeField] private float nameTagFontSize = 1.4f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

    [Header("Ghost Settings")]
    [Tooltip("Custom sprite assigned to player when in Ghost Mode")]
    [SerializeField] private Sprite ghostSprite;
    [SerializeField] private float floatSpeed = 3.0f;
    [SerializeField] private float floatAmplitude = 0.12f;
    
    private Vector2 moveInput;
    private bool isMoving;
    
    private float defaultMoveSpeed;
    private Coroutine speedBoostCoroutine;
    private Transform ghostVisualContainer;
    private Vector3 ghostInitialVisualLocalPos;

    public static string GetOrGeneratePlayerName()
    {
        int nameHasBeenSet = PlayerPrefs.GetInt("PlayerNameHasBeenSet", 0);
        string savedName = PlayerPrefs.GetString("PlayerName", "");

        if (nameHasBeenSet == 1 && !string.IsNullOrEmpty(savedName) && savedName.Trim() != "" && savedName != "Player" && savedName != "You")
        {
            return savedName.Trim();
        }

        // User has not explicitly submitted a custom name: generate a Guest Code!
        int guestCode = Random.Range(1000, 9999);
        string guestName = $"Guest_{guestCode}";
        PlayerPrefs.SetString("PlayerName", guestName);
        PlayerPrefs.SetInt("PlayerNameHasBeenSet", 0);
        PlayerPrefs.Save();
        return guestName;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        RestoreGameplayComponents();

        isLocalCached = false;
        EvaluateIsLocal();

        if (IsOwner || IsLocalPlayer)
        {
            isLocalCached = true;
            playerName.Value = GetOrGeneratePlayerName();
            RegisterCameraIfLocal();
            Debug.Log($"[PlayerController] OnNetworkSpawn: Registered local player '{gameObject.name}' (OwnerClientId: {OwnerClientId})");
        }

        EnsureNameTag();
        playerName.OnValueChanged += (oldVal, newVal) => UpdateNameTag(newVal.ToString());
        UpdateNameTag(playerName.Value.ToString());
    }

    public void RestoreGameplayComponents()
    {
        // 1. Ensure Rigidbody2D is Dynamic and simulated
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // 2. Enable all colliders
        foreach (var col in GetComponentsInChildren<Collider2D>(true))
        {
            col.enabled = true;
        }

        // 3. Enable gameplay components
        var pa = GetComponent<PlayerAiming>(); if (pa != null) pa.enabled = true;
        var wc = GetComponent<WeaponController>(); if (wc != null) wc.enabled = true;
        var bm = GetComponent<BagManager>(); if (bm != null) bm.enabled = true;
        var ph = GetComponent<PlayerHealth>(); if (ph != null) ph.enabled = true;
        var pe = GetComponent<PlayerEnergy>(); if (pe != null) pe.enabled = true;
        var ca = GetComponentInChildren<CharacterAssembler>(); if (ca != null) { ca.enabled = true; ca.LoadEquippedSkin(); }
        var anim = GetComponent<Animator>(); if (anim != null) anim.enabled = true;
    }

    private void EnsureNameTag()
    {
        Transform tagTrans = transform.Find("OverheadNameTag");
        if (tagTrans == null)
        {
            GameObject tagGO = new GameObject("OverheadNameTag");
            tagGO.transform.SetParent(transform, false);
            tagGO.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            nameTagTMP = tagGO.AddComponent<TextMeshPro>();
            nameTagTMP.fontSize = nameTagFontSize;
            nameTagTMP.fontStyle = FontStyles.Bold;
            nameTagTMP.alignment = TextAlignmentOptions.Center;
            // Bright yellow outline-style: visible against any skin or background
            nameTagTMP.color = new Color(1f, 0.95f, 0.3f, 1f);
            nameTagTMP.outlineWidth = 0.15f;
            nameTagTMP.outlineColor = new Color32(0, 0, 0, 200);
            nameTagTMP.sortingOrder = 200; // Well above all character sprite renderers
        }
        else
        {
            nameTagTMP = tagTrans.GetComponent<TextMeshPro>();
        }
    }

    public void UpdateNameTag(string nameText)
    {
        if (nameTagTMP == null) EnsureNameTag();
        // Skip empty names — keep previous text until real name arrives via OnValueChanged
        if (string.IsNullOrEmpty(nameText)) return;
        if (nameTagTMP != null)
        {
            nameTagTMP.text = nameText;
            nameTagTMP.enabled = true;
        }
    }
    
    private void Awake()
    {
        RestoreGameplayComponents();
        
        // Ensure proper setup for top-down 2D game
        if (rb == null)
        {
            Debug.LogError("[PlayerController] Rigidbody2D is missing! Add one to the Player GameObject.");
        }
        
        // Ensure Player has a non-trigger 2D Collider for solid map collision
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = GetComponentInChildren<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.4f;
            circle.isTrigger = false;
            Debug.Log("[PlayerController] Dynamically added CircleCollider2D to player for map collision.");
        }

        // Ensure PlayerHealth and PlayerEnergy exist on Player GameObject
        if (GetComponent<PlayerHealth>() == null)
        {
            gameObject.AddComponent<PlayerHealth>();
        }
        if (GetComponent<PlayerEnergy>() == null)
        {
            gameObject.AddComponent<PlayerEnergy>();
        }

        Debug.Log($"[PlayerController] Initialized on {gameObject.name}");
        defaultMoveSpeed = moveSpeed;
    }
    
    private bool isLocalCached = false;

    public bool IsLocal
    {
        get
        {
            if (isLocalCached) return true;
            EvaluateIsLocal();
            return isLocalCached;
        }
        private set => isLocalCached = value;
    }

    // ─── Local Player Stun/Smoke Events & Triggers ─────────────────────────────
    public static event System.Action<float> OnLocalPlayerStunned;
    public static event System.Action OnLocalPlayerEnterSmoke;
    public static event System.Action OnLocalPlayerExitSmoke;

    public static void TriggerLocalPlayerStun(float duration)
    {
        OnLocalPlayerStunned?.Invoke(duration);
    }

    public static void TriggerEnterSmoke()
    {
        OnLocalPlayerEnterSmoke?.Invoke();
    }

    public static void TriggerExitSmoke()
    {
        OnLocalPlayerExitSmoke?.Invoke();
    }

    private void EvaluateIsLocal()
    {
        if (isLocalCached) return;

        // Prevent unspawned preview objects in MainMenuScene from evaluating as local player
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenuScene")
        {
            var no = GetComponent<Unity.Netcode.NetworkObject>();
            if (no == null || !no.IsSpawned) return;
        }

        bool local = false;

        // 1. Check Unity Netcode (NGO)
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null)
        {
            if (netObj.IsSpawned && (netObj.IsLocalPlayer || netObj.IsOwner)) local = true;
        }
        // 2. Check Photon PUN
        else 
        {
            var photonView = GetComponent<Photon.Pun.PhotonView>();
            if (photonView != null)
            {
                if (photonView.IsMine) local = true;
            }
            // 3. No Networking (Offline / Single Player)
            else
            {
                local = true;
            }
        }

        if (local)
        {
            isLocalCached = true;
            RegisterCameraIfLocal();
        }
    }

    private void RegisterCameraIfLocal()
    {
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetTarget(transform);
        }
        else
        {
            var cam = FindObjectOfType<CameraController>();
            if (cam != null) cam.SetTarget(transform);
        }

        // Register local player on inputs
        if (MobileInputManager.Instance != null)
        {
            MobileInputManager.Instance.SetLocalPlayer(
                this,
                GetComponent<PlayerAiming>(),
                GetComponent<WeaponController>()
            );
        }

        // Register local player on aiming dots
        if (AimingDots.Instance != null)
        {
            AimingDots.Instance.SetLocalPlayer(
                transform,
                GetComponent<PlayerAiming>()
            );
        }

        // Register local player drop point on BagManager
        if (BagManager.Instance != null)
        {
            Transform dp = transform.Find("DropPoint");
            BagManager.Instance.dropPoint = dp != null ? dp : transform;
            Debug.Log("[PlayerController] Linked local player drop point to BagManager.");
        }
    }

    private void Start()
    {
        // Ensure animator is found if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        EvaluateIsLocal();

        if (RelayNetworkManager.HasSnapshot)
        {
            transform.position = RelayNetworkManager.LastPlayerSnapshot.position;
            transform.rotation = RelayNetworkManager.LastPlayerSnapshot.rotation;
            if (RelayNetworkManager.LastPlayerSnapshot.isGhost)
            {
                EnableGhostMode();
            }
            Debug.Log($"[PlayerController] Player '{gameObject.name}' spawned at restored snapshot position: {transform.position}, IsGhost: {IsGhost}");
        }
        else
        {
            // Spawn player at a random non-collider position in the map (not at fixed center)
            Vector3 spawnPos = GetRandomNonColliderSpawnPosition(Vector3.zero, 6.5f);
            transform.position = spawnPos;
            Debug.Log($"[PlayerController] Player '{gameObject.name}' spawned at random non-collider position: {spawnPos}");
        }
    }

    /// <summary>
    /// Calculates a random spawn position within maxRadius that does not overlap solid map colliders.
    /// </summary>
    public static Vector3 GetRandomNonColliderSpawnPosition(Vector3 center, float maxRadius = 6.5f)
    {
        for (int attempts = 0; attempts < 35; attempts++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * maxRadius;
            Vector3 testPos = center + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // Check if testPos overlaps any non-trigger solid map colliders
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(testPos, 0.45f);
            bool isSolidObstacle = false;
            foreach (var col in overlaps)
            {
                if (col != null && !col.isTrigger && !col.CompareTag("Player"))
                {
                    isSolidObstacle = true;
                    break;
                }
            }

            if (!isSolidObstacle)
            {
                return testPos;
            }
        }
        return center;
    }

    private void Update()
    {
        if (!isLocalCached)
        {
            EvaluateIsLocal();
        }

        // Floating air animation when in ghost mode (smooth vertical bobbing)
        if (IsGhost && ghostVisualContainer != null)
        {
            float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            ghostVisualContainer.localPosition = ghostInitialVisualLocalPos + new Vector3(0f, yOffset, 0f);
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
        if (animator != null && animator.enabled)
        {
            animator.SetBool("isWalking", isMoving);
            animator.SetFloat("moveSpeed", input.magnitude);
        }
    }
    
    private void FixedUpdate()
    {
        // Apply movement only when Rigidbody2D is dynamic and simulated
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && rb.simulated)
        {
            if (RelayNetworkManager.Instance != null && RelayNetworkManager.Instance.IsMigrating)
            {
                rb.velocity = Vector2.zero;
                return;
            }

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
    }

    public bool IsGhost { get; private set; } = false;

    public void EnableGhostMode()
    {
        if (IsGhost) return;
        IsGhost = true;

        // Decrease move speed by 20% for ghost mode so living players can escape
        moveSpeed = defaultMoveSpeed * 0.8f;

        // 1. Hide ALL original skin renderers unconditionally
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null) r.enabled = false;
        }

        // 2. Find or create GhostVisualContainer
        Transform ghostChild = transform.Find("GhostVisualContainer");
        if (ghostChild == null)
        {
            GameObject ghostGO = new GameObject("GhostVisualContainer");
            ghostGO.transform.SetParent(transform, false);
            ghostGO.transform.localPosition = Vector3.zero;
            ghostChild = ghostGO.transform;
        }
        ghostVisualContainer = ghostChild;
        ghostInitialVisualLocalPos = Vector3.zero;

        // 3. Show ghost sprite ONLY to the local ghost player (living players see nothing)
        //    The ghost sprite is assigned in the Inspector on the PlayerController prefab.
        if (IsLocal)
        {
            // Get or add a SpriteRenderer on the GhostVisualContainer
            SpriteRenderer ghostSR = ghostChild.GetComponent<SpriteRenderer>();
            if (ghostSR == null) ghostSR = ghostChild.gameObject.AddComponent<SpriteRenderer>();

            if (ghostSprite != null)
            {
                ghostSR.sprite = ghostSprite;
            }
            ghostSR.color = new Color(0.8f, 0.92f, 1.0f, 0.75f); // Translucent blue-white ghost
            ghostSR.sortingOrder = 110;
            ghostChild.gameObject.SetActive(true);
        }
        else
        {
            // Remote ghost: completely invisible to living players
            ghostChild.gameObject.SetActive(false);
        }

        // 4. Hide name tag — ghosts have no overhead name
        if (nameTagTMP != null)
        {
            nameTagTMP.enabled = false;
        }

        // Set colliders to trigger so ghost can pass through obstacles/players
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            if (col != null) col.isTrigger = true;
        }

        // Camera: retarget onto local ghost for spectating
        if (IsLocal)
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(transform);
            }
            // AimingDots hides via Update() IsGhost check
        }

        Debug.Log($"[PlayerController] Ghost mode enabled on '{gameObject.name}' (IsLocal: {IsLocal}). Speed: {moveSpeed}.");
    }

}
