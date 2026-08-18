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

    public NetworkVariable<PlayerRole> playerRole = new NetworkVariable<PlayerRole>(
        PlayerRole.Hostage, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
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

        if (IsServer)
        {
            if (MatchRoleManager.Instance == null && FindObjectOfType<MatchRoleManager>() != null)
            {
                // MatchRoleManager instance found
            }
            if (MatchRoleManager.Instance != null)
            {
                PlayerRole assignedRole = MatchRoleManager.Instance.GetRoleForClient(OwnerClientId);
                playerRole.Value = assignedRole;
                Debug.Log($"[PlayerController] OnNetworkSpawn: Server set playerRole for '{gameObject.name}' (ClientId {OwnerClientId}) -> {assignedRole}");
            }
        }

        playerRole.OnValueChanged += OnPlayerRoleNetworkChanged;
        OnPlayerRoleNetworkChanged(playerRole.Value, playerRole.Value);
    }

    private void OnPlayerRoleNetworkChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        Debug.Log($"[PlayerController] playerRole NetworkVariable synced for '{gameObject.name}': {oldRole} -> {newRole}");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
        {
            RepositionForGameScene();
        }
        if (IsLocal && HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateRoleBadgeDisplay();
        }
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
        var ca = GetComponentInChildren<CharacterAssembler>(); 
        if (ca != null) 
        { 
            ca.enabled = true; 
            if (IsOwner || !IsSpawned)
            {
                ca.LoadEquippedSkin(); 
            }
            else
            {
                ca.ApplySkinByIndex(ca.GetEquippedSkinIndexNetworkValue());
            }
        }
        var anim = GetComponent<Animator>(); if (anim != null) anim.enabled = true;
    }

    private void EnsureNameTag()
    {
        Transform tagTrans = transform.Find("OverheadNameTag");
        if (tagTrans == null)
        {
            GameObject tagGO = new GameObject("OverheadNameTag");
            tagGO.transform.SetParent(transform, false);
            tagTrans = tagGO.transform;
        }

        // Lift position above the player's head and helmet (Y = 1.45f, Z = -0.5f)
        tagTrans.localPosition = new Vector3(0f, 1.45f, -0.5f);
        tagTrans.localRotation = Quaternion.identity;

        nameTagTMP = tagTrans.GetComponent<TextMeshPro>();
        if (nameTagTMP == null)
        {
            nameTagTMP = tagTrans.gameObject.AddComponent<TextMeshPro>();
        }

        float targetFontSize = (nameTagFontSize > 0f) ? nameTagFontSize : 3.5f;
        nameTagTMP.fontSize = targetFontSize;
        nameTagTMP.fontStyle = FontStyles.Bold;
        nameTagTMP.alignment = TextAlignmentOptions.Center;
        // Bright yellow outline-style: visible above all character sprites and backgrounds
        nameTagTMP.color = new Color(1f, 0.95f, 0.3f, 1f);
        nameTagTMP.outlineWidth = 0.2f;
        nameTagTMP.outlineColor = new Color32(0, 0, 0, 255);
        nameTagTMP.sortingOrder = 1000; // Well above all character sprite renderers, weapons, and tilemaps
    }

    public void UpdateNameTag(string nameText)
    {
        EnsureNameTag();
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
            if (netObj.IsSpawned)
            {
                if (netObj.IsLocalPlayer || netObj.IsOwner) local = true;
            }
            else if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                // Netcode not listening / offline test mode
                local = true;
            }
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

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            RepositionForGameScene();
        }
    }

    public void RepositionForGameScene()
    {
        if (RelayNetworkManager.IsMigrating && RelayNetworkManager.HasSnapshot && RelayNetworkManager.LastPlayerSnapshot.HasValue)
        {
            var snap = RelayNetworkManager.LastPlayerSnapshot.Value;
            transform.position = snap.position;
            transform.rotation = snap.rotation;
            if (snap.isGhost)
            {
                EnableGhostMode();
            }
            Debug.Log($"[PlayerController] Repositioned from snapshot position: {transform.position}");
            return;
        }

        // Fresh match spawn: ALWAYS reset ghost state so player starts alive!
        DisableGhostMode();

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.SyncLocationsFromGameManager();
            PlayerRole role;
            if (IsServer)
            {
                role = MatchRoleManager.Instance.GetRoleForClient(OwnerClientId);
                playerRole.Value = role;
            }
            else
            {
                role = playerRole.Value;
            }

            Vector3 spawnPos = MatchRoleManager.Instance.GetSpawnPositionForRole(role);
            transform.position = spawnPos;

            if (rb != null) rb.velocity = Vector2.zero;
            Debug.Log($"[PlayerController] OnSceneLoaded ('GameScene'): Repositioned player '{gameObject.name}' (IsServer: {IsServer}, Role: {role}) to position: {spawnPos}");
        }
        else
        {
            Vector3 spawnPos = GetRandomNonColliderSpawnPosition(Vector3.zero, 6.5f);
            transform.position = spawnPos;
            if (rb != null) rb.velocity = Vector2.zero;
            Debug.Log($"[PlayerController] OnSceneLoaded ('GameScene'): Repositioned player '{gameObject.name}' to position: {spawnPos}");
        }

        if (IsLocal && HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateRoleBadgeDisplay();
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

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
        {
            RepositionForGameScene();
        }
    }

    /// <summary>
    /// Calculates a random spawn position within maxRadius that does not overlap solid map colliders.
    /// </summary>
    public static Vector3 GetRandomNonColliderSpawnPosition(Vector3 center, float maxRadius = 6.5f)
    {
        for (int attempts = 0; attempts < 50; attempts++)
        {
            float radius = (attempts < 30) ? maxRadius : maxRadius * 2.0f;
            Vector2 randomOffset = Random.insideUnitCircle * radius;
            Vector3 testPos = center + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // Check if testPos overlaps any non-trigger solid map colliders
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(testPos, 0.45f);
            bool isSolidObstacle = false;
            foreach (var col in overlaps)
            {
                if (col != null && !col.isTrigger && !col.CompareTag("Player") && col.GetComponent<KeyItemPickup>() == null && col.GetComponent<MainGateController>() == null)
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
        return new Vector3(0f, 0f, 0f);
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
            if (RelayNetworkManager.IsMigrating)
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

    private static Sprite fallbackGhostSprite;

    public static Sprite GetFallbackGhostSprite()
    {
        if (fallbackGhostSprite != null) return fallbackGhostSprite;

        int width = 128;
        int height = 128;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color ghostBody = new Color(0.82f, 0.94f, 1.0f, 0.85f);
        Color eyeColor = new Color(0.12f, 0.18f, 0.3f, 0.95f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, y, transparent);
            }
        }

        float centerX = width * 0.5f;
        float headCenterY = height * 0.62f;
        float headRadius = width * 0.36f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                float dy = y - headCenterY;
                float distToHead = Mathf.Sqrt(dx * dx + dy * dy);

                // Upper rounded head
                if (y >= headCenterY && distToHead <= headRadius)
                {
                    float edgeAlpha = Mathf.Clamp01((headRadius - distToHead) / 2.5f);
                    tex.SetPixel(x, y, new Color(ghostBody.r, ghostBody.g, ghostBody.b, ghostBody.a * edgeAlpha));
                }
                // Floating tail & body
                else if (y < headCenterY && y > height * 0.1f && Mathf.Abs(dx) <= headRadius * (y / headCenterY))
                {
                    float wave = Mathf.Sin((y / (float)height) * Mathf.PI * 4f) * 6f;
                    float distFromEdge = (headRadius * (y / headCenterY)) - Mathf.Abs(dx + wave);
                    if (distFromEdge >= 0f)
                    {
                        float edgeAlpha = Mathf.Clamp01(distFromEdge / 2f);
                        tex.SetPixel(x, y, new Color(ghostBody.r, ghostBody.g, ghostBody.b, ghostBody.a * edgeAlpha));
                    }
                }
            }
        }

        // Cute dark ghost eyes
        int eyeOffsetY = Mathf.RoundToInt(headCenterY + 4f);
        int leftEyeX = Mathf.RoundToInt(centerX - 12f);
        int rightEyeX = Mathf.RoundToInt(centerX + 12f);

        for (int ey = -7; ey <= 7; ey++)
        {
            for (int ex = -5; ex <= 5; ex++)
            {
                if (ex * ex + ey * ey <= 28)
                {
                    tex.SetPixel(leftEyeX + ex, eyeOffsetY + ey, eyeColor);
                    tex.SetPixel(rightEyeX + ex, eyeOffsetY + ey, eyeColor);
                }
            }
        }

        tex.Apply();
        fallbackGhostSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.4f), 100f);
        return fallbackGhostSprite;
    }

    public void DisableGhostMode()
    {
        IsGhost = false;
        moveSpeed = defaultMoveSpeed;

        // Re-enable combat & skin components
        PlayerAiming aiming = GetComponent<PlayerAiming>();
        if (aiming != null) aiming.enabled = true;

        WeaponController wc = GetComponent<WeaponController>();
        if (wc != null) wc.enabled = true;

        CharacterAssembler ca = GetComponentInChildren<CharacterAssembler>();
        if (ca != null)
        {
            ca.enabled = true;
            ca.LoadEquippedSkin();
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            if (anim != null) anim.enabled = true;
        }

        // Re-enable all original character skin & visual child objects
        foreach (Transform child in transform)
        {
            if (child != null && child.name != "GhostVisualContainer")
            {
                child.gameObject.SetActive(true);
            }
        }

        // Hide Ghost visual container
        if (ghostVisualContainer != null)
        {
            ghostVisualContainer.gameObject.SetActive(false);
        }
        else
        {
            Transform ghostChild = transform.Find("GhostVisualContainer");
            if (ghostChild != null) ghostChild.gameObject.SetActive(false);
        }

        // Re-enable all child renderers
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null && r.gameObject.name != "GhostVisualContainer")
            {
                r.enabled = true;
            }
        }

        // Reset solid BoxCollider2D
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = GetComponentInChildren<BoxCollider2D>();
        if (boxCol != null)
        {
            boxCol.enabled = true;
            boxCol.isTrigger = false;
        }

        Rigidbody2D playerRb = GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.simulated = true;
            playerRb.bodyType = RigidbodyType2D.Dynamic;
            playerRb.gravityScale = 0f;
            playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (IsLocal)
        {
            if (MobileInputManager.Instance != null) MobileInputManager.Instance.SetGhostUI(false);
            if (HUDManager.Instance != null) HUDManager.Instance.SetGhostUI(false);
        }

        Debug.Log($"[PlayerController] Ghost mode disabled on '{gameObject.name}'. Player is alive.");
    }

    public void EnableGhostMode()
    {
        if (IsGhost) return;
        IsGhost = true;

        // Decrease move speed by 20% for ghost mode so living players can escape
        moveSpeed = defaultMoveSpeed * 0.8f;

        // 1. Disable scripts/animators that re-enable skin parts every frame
        PlayerAiming aiming = GetComponent<PlayerAiming>();
        if (aiming != null) aiming.enabled = false;

        WeaponController wc = GetComponent<WeaponController>();
        if (wc != null)
        {
            wc.ClearAttachPointChildren();
            wc.enabled = false;
        }

        CharacterAssembler ca = GetComponentInChildren<CharacterAssembler>();
        if (ca != null) ca.enabled = false;

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            if (anim != null) anim.enabled = false;
        }

        // 2. Hide all original skin & weapon child GameObjects unconditionally
        foreach (Transform child in transform)
        {
            if (child != null && child.name != "GhostVisualContainer" && child.name != "Canvas" && child.name != "DropPoint")
            {
                child.gameObject.SetActive(false);
            }
        }

        // Also disable any lingering renderers on top level or remaining children
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null && r.gameObject.name != "GhostVisualContainer")
            {
                r.enabled = false;
            }
        }

        // 3. Find or create GhostVisualContainer
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

        // 4. Show translucent floating ghost sprite ONLY to local ghost player
        if (IsLocal)
        {
            SpriteRenderer ghostSR = ghostChild.GetComponent<SpriteRenderer>();
            if (ghostSR == null) ghostSR = ghostChild.gameObject.AddComponent<SpriteRenderer>();

            Sprite s = (ghostSprite != null) ? ghostSprite : GetFallbackGhostSprite();
            ghostSR.sprite = s;
            ghostSR.color = new Color(0.8f, 0.95f, 1.0f, 0.85f); // Translucent blue-white ghost
            ghostSR.sortingOrder = 110;
            ghostChild.gameObject.SetActive(true);
        }
        else
        {
            // Remote ghost: completely invisible to living players
            ghostChild.gameObject.SetActive(false);
        }

        // 5. Hide name tag — ghosts have no overhead name
        if (nameTagTMP != null)
        {
            nameTagTMP.enabled = false;
        }

        // Ensure ghost player retains solid BoxCollider2D so it collides with walls
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = GetComponentInChildren<BoxCollider2D>();
        if (boxCol != null)
        {
            boxCol.enabled = true;
            boxCol.isTrigger = false;
        }

        Rigidbody2D playerRb = GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.simulated = true;
            playerRb.bodyType = RigidbodyType2D.Dynamic;
            playerRb.gravityScale = 0f;
            playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // Camera: retarget onto local ghost for spectating
        if (IsLocal)
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(transform);
            }
        }

        Debug.Log($"[PlayerController] Ghost mode enabled on '{gameObject.name}' (IsLocal: {IsLocal}). Speed: {moveSpeed}.");
    }

}
