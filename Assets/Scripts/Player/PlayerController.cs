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

    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    public NetworkVariable<int> lobbySlotIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> skinIndex = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    public NetworkVariable<bool> isInGame = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    public NetworkVariable<bool> isGhostNet = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    private TextMeshPro nameTagTMP;

    [Header("Name Tag Settings")]
    [SerializeField] private float nameTagFontSize = 1.4f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    private float nextSnapshotSaveTime = 0f;
    
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

    [Header("Audio Setup")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioListener audioListener;

    [Header("Player Sound Effects")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private AudioClip grenadeThrowClip;
    [SerializeField] private AudioClip defaultPickupClip;
    [SerializeField] private AudioClip defaultDropClip;

    [Header("Movement Dust Effects")]
    [SerializeField] private PlayerMovementDust movementDust;

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
    private float nextFootstepTime;

    [Header("Local Player Identification")]
    [Tooltip("If enabled, this player object is treated as the local player and positioned in the center")]
    public bool isMyPlayerToggle = false;

    public bool IsMyPlayer
    {
        get
        {
            if (isMyPlayerToggle) return true;
            return IsOwner || IsLocalPlayer;
        }
    }

    public static PlayerController LocalPlayer { get; private set; }

    public static Vector3 GetLobbySlotPosition(int slot)
    {
        switch (slot)
        {
            case 0: return new Vector3(0f, 0.58f, 0f);      // Point 0 (My Slot - Center)
            case 1: return new Vector3(-4.2f, 0.58f, 0f);   // Point 1 (Friend 1 - Left)
            case 2: return new Vector3(4.2f, 0.58f, 0f);    // Point 2 (Friend 2 - Right)
            case 3: return new Vector3(-8.4f, 0.58f, 0f);   // Point 3 (Friend 3 - Far Left)
            default: return new Vector3((slot % 2 == 1 ? -1 : 1) * (4.2f + (slot / 2) * 4.2f), 0.58f, 0f);
        }
    }

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
        DontDestroyOnLoad(gameObject);
        RestoreGameplayComponents();

        isLocalCached = false;
        EvaluateIsLocal();

        if (IsOwner || IsLocalPlayer)
        {
            isLocalCached = true;
            playerName.Value = GetOrGeneratePlayerName();
            int mySkin = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            SetSkin(mySkin);
            RegisterCameraIfLocal();
            Debug.Log($"[PlayerController] OnNetworkSpawn: Registered local player '{gameObject.name}' (OwnerClientId: {OwnerClientId}, skinIndex: {skinIndex.Value})");
        }

        EnsureNameTag();
        playerName.OnValueChanged += (oldVal, newVal) => UpdateLobbyNameTag();
        isReady.OnValueChanged += (oldVal, newVal) => RefreshLobbyPositionAndState();
        lobbySlotIndex.OnValueChanged += (oldVal, newVal) =>
        {
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc != null)
                {
                    pc.RefreshLobbyPositionAndState();
                    pc.UpdateLobbyNameTag();
                }
            }
            if (MainMenuController.Instance != null)
            {
                MainMenuController.Instance.UpdateLobbyButtonsState();
            }
        };
        skinIndex.OnValueChanged += (oldVal, newVal) =>
        {
            var ca = GetComponentInChildren<CharacterAssembler>();
            if (ca != null)
            {
                ca.ApplySkinByIndex(newVal);
            }
        };
        isInGame.OnValueChanged += (oldVal, newVal) => UpdateLobbyNameTag();
        isGhostNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal && !IsGhost)
            {
                EnableGhostMode();
            }
            else if (!newVal && IsGhost)
            {
                DisableGhostMode();
            }
            UpdateGhostVisibility();
        };

        // Immediately apply current skinIndex.Value on network spawn for local and remote players!
        var initCA = GetComponentInChildren<CharacterAssembler>();
        if (initCA != null)
        {
            initCA.ApplySkinByIndex(skinIndex.Value);
        }

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

        if (IsOwner && RelayNetworkManager.IsMigrating && RelayNetworkManager.HasSnapshot && RelayNetworkManager.LastPlayerSnapshot.HasValue)
        {
            var snap = RelayNetworkManager.LastPlayerSnapshot.Value;
            if (!IsServer)
            {
                RequestRestoreRoleServerRpc(snap.role);
            }
        }

        RefreshLobbyPositionAndState();
    }

    [ServerRpc]
    public void RequestRestoreRoleServerRpc(PlayerRole role, ServerRpcParams rpcParams = default)
    {
        playerRole.Value = role;
        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.SetRoleForClient(OwnerClientId, role);
        }
        Debug.Log($"[PlayerController] Client {OwnerClientId} authoritatively restored role to {role} via ServerRpc");
    }

    public int GetLocalDisplaySlot()
    {
        // 1. If this player is My Player (local player or has isMyPlayerToggle enabled), it ALWAYS gets Display Spot 0 (Center!)
        if (IsMyPlayer)
        {
            return 0;
        }

        // 2. Find local player instance on this machine
        PlayerController localPC = PlayerController.LocalPlayer;
        if (localPC == null || !localPC.IsMyPlayer)
        {
            foreach (var p in FindObjectsOfType<PlayerController>())
            {
                if (p != null && p.IsMyPlayer) { localPC = p; break; }
            }
        }

        if (localPC == null)
        {
            return lobbySlotIndex.Value;
        }

        // 3. Gather all remote players (players where IsMyPlayer is false)
        var allPCs = FindObjectsOfType<PlayerController>();
        var remotePlayers = new System.Collections.Generic.List<PlayerController>();
        foreach (var p in allPCs)
        {
            if (p != null && !p.IsMyPlayer)
            {
                remotePlayers.Add(p);
            }
        }
        remotePlayers.Sort((a, b) => a.lobbySlotIndex.Value.CompareTo(b.lobbySlotIndex.Value));

        int remoteIndex = remotePlayers.IndexOf(this);
        if (remoteIndex < 0) remoteIndex = 0;

        return remoteIndex + 1;
    }

    public void RefreshLobbyPositionAndState()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "MainMenuScene")
        {
            RestoreGameplayComponents();
            UpdateLobbyNameTag();
            return;
        }

        int displaySlot = GetLocalDisplaySlot();
        Vector3 targetPos;
        if (MainMenuController.Instance != null)
        {
            targetPos = MainMenuController.Instance.GetLobbySpawnPosition(displaySlot);
        }
        else
        {
            targetPos = GetLobbySlotPosition(displaySlot);
        }

        // Disable NetworkTransform / ClientNetworkTransform in Main Menu so network interpolation ticks do not pull transform back to (0,0)
        var cnt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (cnt != null) cnt.enabled = false;
        var cnt2 = GetComponent<ClientNetworkTransform>();
        if (cnt2 != null) cnt2.enabled = false;

        transform.position = targetPos;
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(3.081f, 3.081f, 3.081f);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.position = targetPos;
            rb.simulated = false;
        }

        // Disable Controls, Aiming Dots, and Rotation in Main Menu
        var aiming = GetComponent<PlayerAiming>();
        if (aiming != null) aiming.enabled = false;

        var weaponCtrl = GetComponent<WeaponController>();
        if (weaponCtrl != null) weaponCtrl.enabled = false;

        var dots = GetComponentInChildren<AimingDots>(true);
        if (dots != null) dots.gameObject.SetActive(false);

        // Ensure all visual renderers and child containers are active in Main Menu lobby
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        foreach (Transform child in transform)
        {
            if (child != null)
            {
                if (child.name.Contains("OverheadLeave") || child.name.Contains("OverheadMakeHost"))
                {
                    if (currentScene != "MainMenuScene")
                    {
                        child.gameObject.SetActive(false);
                        Destroy(child.gameObject);
                        continue;
                    }
                }
                child.gameObject.SetActive(true);
            }
        }

        var anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = true;

        var assembler = GetComponentInChildren<CharacterAssembler>();
        if (assembler != null)
        {
            assembler.enabled = true;
            assembler.ApplySkinByIndex(skinIndex.Value);
        }

        UpdateLobbyNameTag();
    }

    private int GetRemotePlayerIndex()
    {
        var allPlayers = FindObjectsOfType<PlayerController>();
        var remotePlayers = new System.Collections.Generic.List<PlayerController>();

        foreach (var p in allPlayers)
        {
            if (p == null) continue;
            var netObj = p.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned) continue; // Skip static or unspawned scene objects

            if (!p.IsOwner && !p.IsLocalPlayer)
            {
                remotePlayers.Add(p);
            }
        }

        remotePlayers.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        int index = remotePlayers.IndexOf(this);
        return index >= 0 ? index : 0;
    }

    public void UpdateLobbyNameTag()
    {
        EnsureNameTag();
        if (nameTagTMP == null) return;

        string displayName = playerName.Value.ToString();
        if (string.IsNullOrEmpty(displayName) || displayName.Contains("(Clone)"))
        {
            string clean = gameObject.name.Replace("(Clone)", "").Trim();
            displayName = !string.IsNullOrEmpty(clean) && clean != "Player" ? clean : "Player";
        }

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (activeScene == "MainMenuScene")
        {
            int clientCount = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

            if (clientCount <= 1)
            {
                // Solo mode: Hide overhead name tag and buttons completely
                nameTagTMP.gameObject.SetActive(false);
                if (leaveRoomButtonCanvasGO != null) leaveRoomButtonCanvasGO.SetActive(false);
                if (makeHostButtonCanvasGO != null) makeHostButtonCanvasGO.SetActive(false);
            }
            else
            {
                // Team mode: Show name tag with role badges
                nameTagTMP.gameObject.SetActive(true);
                if (lobbySlotIndex.Value == 0)
                {
                    nameTagTMP.text = $"{displayName}\n<color=#FFD700>[HOST]</color>";
                }
                else if (isReady.Value)
                {
                    nameTagTMP.text = $"{displayName}\n<color=#00FF00>[READY]</color>";
                }
                else
                {
                    nameTagTMP.text = $"{displayName}\n<color=#AAAAAA>[NOT READY]</color>";
                }

                // Show LEAVE ROOM button only below the local player in Main Menu room
                if (IsOwner || IsLocalPlayer)
                {
                    EnsureOverheadLeaveButton();
                    if (leaveRoomButtonCanvasGO != null) leaveRoomButtonCanvasGO.SetActive(true);
                    if (makeHostButtonCanvasGO != null) makeHostButtonCanvasGO.SetActive(false);
                }
                else
                {
                    if (leaveRoomButtonCanvasGO != null) leaveRoomButtonCanvasGO.SetActive(false);

                    // Show MAKE HOST button below friend players ONLY when the local player is Host (slot 0)
                    bool localIsHost = false;
                    PlayerController localPC = PlayerController.LocalPlayer;
                    if (localPC == null)
                    {
                        foreach (var p in FindObjectsOfType<PlayerController>())
                        {
                            if (p != null && (p.IsOwner || p.IsLocal)) { localPC = p; break; }
                        }
                    }

                    if (localPC != null && localPC.lobbySlotIndex.Value == 0)
                    {
                        localIsHost = true;
                    }

                    if (localIsHost)
                    {
                        EnsureOverheadMakeHostButton();
                        if (makeHostButtonCanvasGO != null) makeHostButtonCanvasGO.SetActive(true);
                    }
                    else
                    {
                        if (makeHostButtonCanvasGO != null) makeHostButtonCanvasGO.SetActive(false);
                    }
                }
            }
        }
        else
        {
            // Non-MainMenu scene (CustomLobby or GameScene): Destroy overhead menu buttons completely
            if (leaveRoomButtonCanvasGO != null)
            {
                Destroy(leaveRoomButtonCanvasGO);
                leaveRoomButtonCanvasGO = null;
            }
            if (makeHostButtonCanvasGO != null)
            {
                Destroy(makeHostButtonCanvasGO);
                makeHostButtonCanvasGO = null;
            }

            Transform oldLeave = transform.Find("OverheadLeaveRoomCanvas");
            if (oldLeave != null) Destroy(oldLeave.gameObject);

            Transform oldHost = transform.Find("OverheadMakeHostCanvas");
            if (oldHost != null) Destroy(oldHost.gameObject);

            // In gameplay / CustomLobby: Do NOT show our own name on top of us! Show clean names for other players only without [HOST] or [READY] tags.
            bool isThisLocal = IsLocal || IsOwner || IsLocalPlayer || (LocalPlayer == this);
            if (isThisLocal)
            {
                if (nameTagTMP != null && nameTagTMP.gameObject != null)
                {
                    nameTagTMP.gameObject.SetActive(false);
                }
            }
            else
            {
                if (nameTagTMP != null && nameTagTMP.gameObject != null)
                {
                    nameTagTMP.gameObject.SetActive(true);
                    nameTagTMP.enabled = true;

                    var mr = nameTagTMP.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.sortingLayerName = "player";
                        mr.sortingOrder = 5000;
                        mr.enabled = true;
                    }

                    bool isOpponent = LocalPlayer != null && LocalPlayer.playerRole.Value != PlayerRole.Hostage && LocalPlayer.playerRole.Value != this.playerRole.Value;
                    if (isOpponent)
                    {
                        nameTagTMP.text = $"<color=#FF3333>{displayName}</color>";
                    }
                    else
                    {
                        nameTagTMP.text = $"<color=#00E5FF>{displayName}</color>";
                    }
                }
            }
        }
    }

    private void OnPlayerRoleNetworkChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        Debug.Log($"[PlayerController] playerRole NetworkVariable synced for '{gameObject.name}': {oldRole} -> {newRole}");
        UpdateLobbyNameTag();
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
        transform.localScale = new Vector3(2f, 2f, 2f);

        // Re-enable NetworkTransform for active gameplay
        var cnt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (cnt != null) cnt.enabled = true;
        var cnt2 = GetComponent<ClientNetworkTransform>();
        if (cnt2 != null) cnt2.enabled = true;

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
            ca.ApplySkinByIndex(skinIndex.Value);
        }
        var anim = GetComponent<Animator>(); if (anim != null) anim.enabled = true;
    }

    [Header("Name Tag Settings")]
    [Tooltip("Vertical height offset above player origin for the overhead name tag")]
    public float nameTagHeightOffset = 0.85f;

    private void EnsureNameTag()
    {
        Transform tagTrans = transform.Find("OverheadNameTag");
        if (tagTrans == null)
        {
            GameObject tagGO = new GameObject("OverheadNameTag");
            tagGO.transform.SetParent(transform, false);
            tagTrans = tagGO.transform;
        }

        // Position on top of the player's head/hair (Y = 0.85f, Z = -1.0f)
        float targetY = (nameTagHeightOffset > 0f) ? nameTagHeightOffset : 0.85f;
        tagTrans.localPosition = new Vector3(0f, targetY, -1.0f);
        tagTrans.localRotation = Quaternion.identity;

        nameTagTMP = tagTrans.GetComponent<TextMeshPro>();
        if (nameTagTMP == null)
        {
            nameTagTMP = tagTrans.gameObject.AddComponent<TextMeshPro>();
        }

        if (nameTagTMP.font == null)
        {
            var f = LoadingGameController.GetFontAsset();
            if (f != null) nameTagTMP.font = f;
        }

        float targetFontSize = (nameTagFontSize > 0f) ? nameTagFontSize : 2.8f;
        nameTagTMP.fontSize = targetFontSize;
        nameTagTMP.fontStyle = FontStyles.Bold;
        nameTagTMP.alignment = TextAlignmentOptions.Center;
        nameTagTMP.enableWordWrapping = false;
        nameTagTMP.overflowMode = TextOverflowModes.Overflow;
        nameTagTMP.rectTransform.sizeDelta = new Vector2(10f, 2f);
        // Bright yellow outline-style: visible above all character sprites and backgrounds
        nameTagTMP.color = new Color(1f, 0.95f, 0.3f, 1f);
        nameTagTMP.outlineWidth = 0.25f;
        nameTagTMP.outlineColor = new Color32(0, 0, 0, 255);
        nameTagTMP.sortingLayerID = SortingLayer.NameToID("player");
        nameTagTMP.sortingOrder = 5000; // Well above character sprite renderers (baseOrder 0 to 4)

        var mr = nameTagTMP.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = "player";
            mr.sortingOrder = 5000;
            mr.enabled = true;
        }
    }

    private GameObject leaveRoomButtonCanvasGO;
    private UnityEngine.UI.Button leaveRoomButton;

    private void EnsureOverheadLeaveButton()
    {
        Transform canvasTrans = transform.Find("OverheadLeaveRoomCanvas");
        if (canvasTrans != null)
        {
            leaveRoomButtonCanvasGO = canvasTrans.gameObject;
            canvasTrans.localPosition = new Vector3(0f, -0.60f, -0.5f);
            canvasTrans.localScale = new Vector3(0.0042f, 0.0042f, 1f);
            
            var rt = canvasTrans.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(180f, 44f);

            leaveRoomButton = leaveRoomButtonCanvasGO.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnOverheadLeaveRoomClicked);
            }

            var txtTMP = leaveRoomButtonCanvasGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txtTMP != null) txtTMP.text = "LEAVE ROOM";
            return;
        }

        leaveRoomButtonCanvasGO = new GameObject("OverheadLeaveRoomCanvas");
        leaveRoomButtonCanvasGO.transform.SetParent(transform, false);
        leaveRoomButtonCanvasGO.transform.localPosition = new Vector3(0f, -0.60f, -0.5f);
        leaveRoomButtonCanvasGO.transform.localScale = new Vector3(0.0042f, 0.0042f, 1f);

        Canvas canvas = leaveRoomButtonCanvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = leaveRoomButtonCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main ?? FindObjectOfType<Camera>();
        canvas.sortingLayerName = "player";
        canvas.sortingOrder = 5500;

        if (leaveRoomButtonCanvasGO.GetComponent<UnityEngine.UI.CanvasScaler>() == null)
            leaveRoomButtonCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        if (leaveRoomButtonCanvasGO.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            leaveRoomButtonCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        RectTransform canvasRt = leaveRoomButtonCanvasGO.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(180f, 44f);

        GameObject btnGO = new GameObject("LeaveRoomBtn", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        btnGO.transform.SetParent(leaveRoomButtonCanvasGO.transform, false);

        RectTransform btnRt = btnGO.GetComponent<RectTransform>();
        btnRt.anchorMin = Vector2.zero;
        btnRt.anchorMax = Vector2.one;
        btnRt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image img = btnGO.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        UnityEngine.UI.Outline outline = btnGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(1f, 0.5f, 0.5f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "LEAVE ROOM";
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        leaveRoomButton = btnGO.GetComponent<UnityEngine.UI.Button>();
        leaveRoomButton.onClick.RemoveAllListeners();
        leaveRoomButton.onClick.AddListener(OnOverheadLeaveRoomClicked);
    }

    private async void OnOverheadLeaveRoomClicked()
    {
        UniversalButtonAudio.PlayClickSFX();
        Debug.Log("[PlayerController] Local player clicked overhead LEAVE ROOM button.");

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        if (isHost)
        {
            try
            {
                NotifyRoomDisbandedClientRpc();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlayerController] Could not broadcast NotifyRoomDisbandedClientRpc: {ex.Message}");
            }
            await System.Threading.Tasks.Task.Delay(150);
        }

        if (RelayNetworkManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
            {
                RelayNetworkManager.LastKickedOrLeftRoomCode = RelayNetworkManager.Instance.CurrentJoinCode;
            }
            await RelayNetworkManager.Instance.LeaveMatchGracefully();
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        var allPCs = FindObjectsOfType<PlayerController>();
        foreach (var pc in allPCs)
        {
            if (pc != null && pc.gameObject != null)
            {
                Destroy(pc.gameObject);
            }
        }

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.SetupPreviewPlayer();
            MainMenuController.Instance.ResetPreviewToEquippedSkin();
            MainMenuController.Instance.UpdateLobbyButtonsState();
            MainMenuController.Instance.UpdatePlayStatus("");
        }
    }

    private GameObject makeHostButtonCanvasGO;
    private UnityEngine.UI.Button makeHostButton;
    private UnityEngine.UI.Button kickButton;

    private void EnsureOverheadMakeHostButton()
    {
        Transform canvasTrans = transform.Find("OverheadMakeHostCanvas");
        if (canvasTrans != null)
        {
            makeHostButtonCanvasGO = canvasTrans.gameObject;
            canvasTrans.localPosition = new Vector3(0f, -0.60f, -0.5f);
            canvasTrans.localScale = new Vector3(0.0042f, 0.0042f, 1f);

            var rt = canvasTrans.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(210f, 44f);

            var canvas = makeHostButtonCanvasGO.GetComponent<Canvas>();
            if (canvas != null) canvas.worldCamera = Camera.main ?? FindObjectOfType<Camera>();

            makeHostButton = makeHostButtonCanvasGO.transform.Find("MakeHostBtn")?.GetComponent<UnityEngine.UI.Button>();
            if (makeHostButton != null)
            {
                makeHostButton.onClick.RemoveAllListeners();
                makeHostButton.onClick.AddListener(OnOverheadMakeHostClicked);
            }

            kickButton = makeHostButtonCanvasGO.transform.Find("KickBtn")?.GetComponent<UnityEngine.UI.Button>();
            if (kickButton != null)
            {
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(OnOverheadKickClicked);
            }
            return;
        }

        makeHostButtonCanvasGO = new GameObject("OverheadMakeHostCanvas");
        makeHostButtonCanvasGO.transform.SetParent(transform, false);
        makeHostButtonCanvasGO.transform.localPosition = new Vector3(0f, -0.60f, -0.5f);
        makeHostButtonCanvasGO.transform.localScale = new Vector3(0.0042f, 0.0042f, 1f);

        Canvas canvasComp = makeHostButtonCanvasGO.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.WorldSpace;
        canvasComp.worldCamera = Camera.main ?? FindObjectOfType<Camera>();
        canvasComp.sortingLayerName = "player";
        canvasComp.sortingOrder = 5500;

        makeHostButtonCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        makeHostButtonCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        RectTransform canvasRt = makeHostButtonCanvasGO.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(210f, 44f);

        // 1. MAKE HOST Button (Gold)
        GameObject hostBtnGO = new GameObject("MakeHostBtn", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        hostBtnGO.transform.SetParent(makeHostButtonCanvasGO.transform, false);

        RectTransform hostRt = hostBtnGO.GetComponent<RectTransform>();
        hostRt.anchorMin = new Vector2(0f, 0f);
        hostRt.anchorMax = new Vector2(0.64f, 1f);
        hostRt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image hostImg = hostBtnGO.GetComponent<UnityEngine.UI.Image>();
        hostImg.color = new Color(0.9f, 0.65f, 0.1f, 0.95f);

        UnityEngine.UI.Outline hostOutline = hostBtnGO.AddComponent<UnityEngine.UI.Outline>();
        hostOutline.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        hostOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject hostTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        hostTxtGO.transform.SetParent(hostBtnGO.transform, false);
        RectTransform hostTxtRt = hostTxtGO.GetComponent<RectTransform>();
        hostTxtRt.anchorMin = Vector2.zero;
        hostTxtRt.anchorMax = Vector2.one;
        hostTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI hostTmp = hostTxtGO.GetComponent<TextMeshProUGUI>();
        hostTmp.text = "MAKE HOST";
        hostTmp.fontSize = 16;
        hostTmp.fontStyle = FontStyles.Bold;
        hostTmp.alignment = TextAlignmentOptions.Center;
        hostTmp.color = Color.white;

        makeHostButton = hostBtnGO.GetComponent<UnityEngine.UI.Button>();
        makeHostButton.onClick.RemoveAllListeners();
        makeHostButton.onClick.AddListener(OnOverheadMakeHostClicked);

        // 2. KICK Button (Red)
        GameObject kickBtnGO = new GameObject("KickBtn", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        kickBtnGO.transform.SetParent(makeHostButtonCanvasGO.transform, false);

        RectTransform kickRt = kickBtnGO.GetComponent<RectTransform>();
        kickRt.anchorMin = new Vector2(0.68f, 0f);
        kickRt.anchorMax = new Vector2(1f, 1f);
        kickRt.sizeDelta = Vector2.zero;

        UnityEngine.UI.Image kickImg = kickBtnGO.GetComponent<UnityEngine.UI.Image>();
        kickImg.color = new Color(0.85f, 0.2f, 0.2f, 0.95f);

        UnityEngine.UI.Outline kickOutline = kickBtnGO.AddComponent<UnityEngine.UI.Outline>();
        kickOutline.effectColor = new Color(1f, 0.5f, 0.5f, 0.9f);
        kickOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject kickTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        kickTxtGO.transform.SetParent(kickBtnGO.transform, false);
        RectTransform kickTxtRt = kickTxtGO.GetComponent<RectTransform>();
        kickTxtRt.anchorMin = Vector2.zero;
        kickTxtRt.anchorMax = Vector2.one;
        kickTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI kickTmp = kickTxtGO.GetComponent<TextMeshProUGUI>();
        kickTmp.text = "KICK";
        kickTmp.fontSize = 16;
        kickTmp.fontStyle = FontStyles.Bold;
        kickTmp.alignment = TextAlignmentOptions.Center;
        kickTmp.color = Color.white;

        kickButton = kickBtnGO.GetComponent<UnityEngine.UI.Button>();
        kickButton.onClick.RemoveAllListeners();
        kickButton.onClick.AddListener(OnOverheadKickClicked);
    }

    private void OnOverheadMakeHostClicked()
    {
        UniversalButtonAudio.PlayClickSFX();
        Debug.Log($"[PlayerController] Local host clicked MAKE HOST for player '{gameObject.name}' (OwnerClientId: {OwnerClientId})");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                TransferHostLocal(OwnerClientId);
            }
            else
            {
                RequestMakeHostServerRpc(OwnerClientId);
            }
        }
    }

    private void OnOverheadKickClicked()
    {
        UniversalButtonAudio.PlayClickSFX();
        Debug.Log($"[PlayerController] Local host clicked KICK for player '{gameObject.name}' (OwnerClientId: {OwnerClientId})");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                KickPlayerLocal(OwnerClientId);
            }
            else
            {
                RequestKickPlayerServerRpc(OwnerClientId);
            }
        }
    }

    public void SetSkin(int newSkinIndex)
    {
        if (IsOwner || !IsSpawned)
        {
            skinIndex.Value = newSkinIndex;
        }

        var ca = GetComponentInChildren<CharacterAssembler>();
        if (ca != null)
        {
            ca.ApplySkinByIndex(newSkinIndex);
        }

        if (IsSpawned)
        {
            SetSkinServerRpc(newSkinIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetSkinServerRpc(int newSkinIndex, ServerRpcParams rpcParams = default)
    {
        if (IsOwner)
        {
            skinIndex.Value = newSkinIndex;
        }
        SetSkinClientRpc(newSkinIndex);
    }

    [ClientRpc]
    private void SetSkinClientRpc(int newSkinIndex)
    {
        var ca = GetComponentInChildren<CharacterAssembler>();
        if (ca != null)
        {
            ca.ApplySkinByIndex(newSkinIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestStartMatchServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log("[PlayerController] Server received RPC from Room Host (slot 0) to start match!");
        if (RelayNetworkManager.Instance != null)
        {
            RelayNetworkManager.Instance.ExecuteSceneLoad("GameScene");
        }
    }

    [ClientRpc]
    public void NotifyPartyTargetModeClientRpc(LoadingGameController.MatchMode mode)
    {
        Debug.Log($"[PlayerController] Client received party target mode from host: {mode}");
        LoadingGameController.TargetMode = mode;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPartyStartMatchServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log("[PlayerController] Server received RPC from Room Host (slot 0) to transition party to LoadingGame -> CustomLobby!");
        LoadingGameController.TargetMode = LoadingGameController.MatchMode.PartyToLobby;
        NotifyPartyTargetModeClientRpc(LoadingGameController.MatchMode.PartyToLobby);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("LoadingGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else if (RelayNetworkManager.Instance != null)
        {
            RelayNetworkManager.Instance.ExecuteSceneLoad("LoadingGame");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingGame");
        }
    }

    [ClientRpc]
    public void NotifyRoomDisbandedClientRpc()
    {
        if (IsServer) return; // Server already initiated the disband
        Debug.Log("[PlayerController] Room Host disbanded the room in Main Menu. Leaving room cleanly...");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        var allPCs = FindObjectsOfType<PlayerController>();
        foreach (var pc in allPCs)
        {
            if (pc != null && pc.gameObject != null)
            {
                Destroy(pc.gameObject);
            }
        }

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.SetupPreviewPlayer();
            MainMenuController.Instance.ResetPreviewToEquippedSkin();
            MainMenuController.Instance.UpdateLobbyButtonsState();
            MainMenuController.Instance.UpdatePlayStatus("<color=yellow>Room disbanded by host</color>");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestKickPlayerServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[PlayerController] Server received RPC to KICK ClientId {targetClientId}");
        KickPlayerLocal(targetClientId);
    }

    public static async void KickPlayerLocal(ulong targetClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[PlayerController] Host kicking client {targetClientId}...");

        PlayerController targetPC = null;
        foreach (var p in FindObjectsOfType<PlayerController>())
        {
            if (p != null && (p.OwnerClientId == targetClientId || (p.IsLocal && targetClientId == 0)))
            {
                targetPC = p;
                break;
            }
        }

        if (targetPC != null)
        {
            targetPC.NotifyKickedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            });
            // Allow RPC packet time to be dispatched over Relay to client before disconnecting
            await System.Threading.Tasks.Task.Delay(150);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
        {
            NetworkManager.Singleton.DisconnectClient(targetClientId);
        }

        PlayerController[] remaining = FindObjectsOfType<PlayerController>();
        int slot = 0;
        foreach (var p in remaining)
        {
            if (p != null && p != targetPC && p.OwnerClientId != targetClientId)
            {
                p.lobbySlotIndex.Value = slot++;
                p.RefreshLobbyPositionAndState();
                p.UpdateLobbyNameTag();
            }
        }
    }

    [ClientRpc]
    public void NotifyKickedClientRpc(ClientRpcParams clientRpcParams = default)
    {
        UniversalButtonAudio.PlayClickSFX();
        Debug.Log("[PlayerController] Received KICK notification from host.");

        if (RelayNetworkManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
            {
                RelayNetworkManager.LastKickedOrLeftRoomCode = RelayNetworkManager.Instance.CurrentJoinCode;
            }
            _ = RelayNetworkManager.Instance.LeaveMatchGracefully();
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        var allPCs = FindObjectsOfType<PlayerController>();
        foreach (var pc in allPCs)
        {
            if (pc != null && pc.gameObject != null) Destroy(pc.gameObject);
        }

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.SetupPreviewPlayer();
            MainMenuController.Instance.ResetPreviewToEquippedSkin();
            MainMenuController.Instance.UpdateLobbyButtonsState();
            MainMenuController.Instance.UpdatePlayStatus("<color=#FF4444>You were kicked from the room</color>");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestMakeHostServerRpc(ulong newHostClientId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[PlayerController] Server received request to transfer Host to ClientId {newHostClientId}");
        TransferHostLocal(newHostClientId);
    }

    public static void TransferHostLocal(ulong newHostClientId)
    {
        PlayerController[] pcs = FindObjectsOfType<PlayerController>();
        PlayerController targetNewHost = null;

        foreach (var p in pcs)
        {
            if (p != null && (p.OwnerClientId == newHostClientId || (p.IsLocal && newHostClientId == 0)))
            {
                targetNewHost = p;
                break;
            }
        }

        if (targetNewHost == null && pcs.Length > 0)
        {
            foreach (var p in pcs)
            {
                if (p != null && p.OwnerClientId == newHostClientId)
                {
                    targetNewHost = p;
                    break;
                }
            }
        }

        if (targetNewHost == null) return;

        targetNewHost.lobbySlotIndex.Value = 0;

        int nextSlot = 1;
        foreach (var p in pcs)
        {
            if (p != null && p != targetNewHost)
            {
                p.lobbySlotIndex.Value = nextSlot++;
            }
        }

        foreach (var p in pcs)
        {
            if (p != null)
            {
                p.RefreshLobbyPositionAndState();
            }
        }

        if (RelayNetworkManager.Instance != null)
        {
            string targetPlayerId = targetNewHost.playerName != null ? targetNewHost.playerName.Value.ToString() : "";
            if (!string.IsNullOrEmpty(targetPlayerId))
            {
                _ = RelayNetworkManager.Instance.TransferLobbyHostAsync(targetPlayerId);
            }
        }
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

        // Ensure PlayerMovementDust exists for walking dust particles
        if (movementDust == null)
        {
            movementDust = GetComponent<PlayerMovementDust>();
            if (movementDust == null)
            {
                movementDust = gameObject.AddComponent<PlayerMovementDust>();
            }
        }

        Debug.Log($"[PlayerController] Initialized on {gameObject.name}");
        defaultMoveSpeed = moveSpeed;
        SetupAudio();
    }

    private bool isLocalCached = false;
    private bool cachedIsLocal = false;

    public bool IsLocal
    {
        get
        {
            if (!isLocalCached) EvaluateIsLocal();
            return cachedIsLocal;
        }
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

        if (isMyPlayerToggle)
        {
            cachedIsLocal = true;
            isLocalCached = true;
            LocalPlayer = this;
            return;
        }

        // Bots are NEVER local human players!
        if (CompareTag("Bot") || GetComponent<AiBotController>() != null || gameObject.name.ToLower().Contains("bot"))
        {
            isLocalCached = true;
            cachedIsLocal = false;
            return;
        }

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
                else local = false;
            }
            else
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    local = netObj.IsOwner || netObj.IsLocalPlayer;
                }
                else
                {
                    local = true; // Solo offline test mode
                }
            }
        }
        // 2. Offline / Single Player mode
        else 
        {
            local = true;
        }

        isLocalCached = true;
        cachedIsLocal = local;

        if (local)
        {
            LocalPlayer = this;
            SetupAudio();
            RegisterCameraIfLocal();
        }

        return;
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
        if (scene.name != "MainMenuScene")
        {
            RestoreGameplayComponents();
            if (IsOwner || IsLocalPlayer)
            {
                RegisterCameraIfLocal();
                // Ensure local player's authoritative skin is broadcast to all clients in the match
                int mySkin = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
                SetSkin(mySkin);
            }
        }

        moveInput = Vector2.zero;
        isMoving = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        if (animator != null && animator.enabled)
        {
            animator.SetBool("isWalking", false);
            animator.SetFloat("moveSpeed", 0f);
        }

        if (scene.name == "CustomLobby")
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
            if (IsOwner || IsLocalPlayer)
            {
                isInGame.Value = true;
                transform.position = new Vector3(0f, 0.58f, 0f);
                if (rb != null) rb.position = new Vector2(0f, 0.58f);
            }
        }
        else if (scene.name == "GameScene")
        {
            if (IsOwner || IsLocalPlayer)
            {
                isInGame.Value = true;
            }
        }
        else if (scene.name == "MainMenuScene")
        {
            if (IsOwner || IsLocalPlayer)
            {
                isInGame.Value = false;
                if (isGhostNet != null && isGhostNet.Value) isGhostNet.Value = false;
            }
            DisableGhostMode();
        }

        UpdateLobbyNameTag();

        if (scene.name == "GameScene")
        {
            RepositionForGameScene();
            var ca = GetComponentInChildren<CharacterAssembler>();
            if (ca != null)
            {
                ca.ApplySkinByIndex(skinIndex.Value);
            }
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
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "MainMenuScene")
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    Debug.Log($"[PlayerController] Destroying unspawned static scene Player object '{gameObject.name}' because Netcode is active.");
                    Destroy(gameObject);
                    return;
                }
            }
        }

        // Ensure animator is found if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        EvaluateIsLocal();

        var ca = GetComponentInChildren<CharacterAssembler>();
        if (ca != null)
        {
            ca.ApplySkinByIndex(skinIndex.Value);
        }

        if (sceneName == "GameScene")
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
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == "MainMenuScene")
        {
            int slot = GetLocalDisplaySlot();
            Vector3 targetPos = MainMenuController.Instance != null 
                ? MainMenuController.Instance.GetLobbySpawnPosition(slot)
                : GetLobbySlotPosition(slot);

            transform.position = targetPos;
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = targetPos;
            return;
        }

        if (!isLocalCached)
        {
            EvaluateIsLocal();
        }

        // Spectral Ghost Visibility:
        // Only ghosts can see other ghosts and themselves. Normal living players cannot see ghosts.
        UpdateGhostVisibility();

        if (rb != null && (IsLocal || !isLocalCached || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening))
        {
            transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
        }

        // Continuously persist live snapshot while playing in GameScene so host migration never loses position/data
        if (IsLocal && !RelayNetworkManager.IsMigrating && activeScene == "GameScene")
        {
            if (Time.time >= nextSnapshotSaveTime)
            {
                nextSnapshotSaveTime = Time.time + 0.5f;
                RelayNetworkManager.SaveCurrentPlayerState(this);
            }
        }

        HandleFootstepSounds();

        // Floating air animation when in ghost mode (smooth vertical bobbing, tilt, scale breathing)
        if (IsGhost && ghostVisualContainer != null && ghostVisualContainer.gameObject.activeSelf)
        {
            float time = Time.time;
            // 1. Smooth vertical bobbing in the air
            float yBob = Mathf.Sin(time * floatSpeed) * floatAmplitude;
            // 2. Gentle sway / tilt in the air
            float tilt = Mathf.Sin(time * 2.2f) * 4.0f;
            // When moving, tilt dynamically towards movement direction
            if (moveInput.sqrMagnitude > 0.01f)
            {
                tilt += -moveInput.x * 10f;
            }
            // 3. Subtle ethereal breathing scale
            float scaleY = 0.95f + Mathf.Sin(time * floatSpeed) * 0.04f;
            float scaleX = 0.95f - Mathf.Sin(time * floatSpeed) * 0.025f;

            ghostVisualContainer.localPosition = ghostInitialVisualLocalPos + new Vector3(0f, yBob, 0f);
            ghostVisualContainer.localRotation = Quaternion.Euler(0f, 0f, tilt);
            ghostVisualContainer.localScale = new Vector3(scaleX, scaleY, 1f);

            // Facing flip according to horizontal movement
            SpriteRenderer ghostSR = ghostVisualContainer.GetComponent<SpriteRenderer>();
            if (ghostSR != null && Mathf.Abs(moveInput.x) > 0.05f)
            {
                ghostSR.flipX = moveInput.x < 0;
            }
        }
    }

    private void LateUpdate()
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == "MainMenuScene")
        {
            int slot = GetLocalDisplaySlot();
            Vector3 targetPos = MainMenuController.Instance != null 
                ? MainMenuController.Instance.GetLobbySpawnPosition(slot)
                : GetLobbySlotPosition(slot);

            transform.position = targetPos;
            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = targetPos;
        }
        else if (rb != null && (IsLocal || !isLocalCached || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening))
        {
            // Prevent root-level Animator clips from overriding world position to (0, 0)
            transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
        }

        if (nameTagTMP != null && nameTagTMP.enabled)
        {
            nameTagTMP.transform.rotation = Quaternion.identity;
            nameTagTMP.transform.localPosition = new Vector3(0f, nameTagHeightOffset > 0f ? nameTagHeightOffset : 0.85f, -1.0f);
            float signX = transform.lossyScale.x < 0f ? -1f : 1f;
            nameTagTMP.transform.localScale = new Vector3(signX, 1f, 1f);

            var mr = nameTagTMP.GetComponent<MeshRenderer>();
            if (mr != null && (mr.sortingLayerName != "player" || mr.sortingOrder != 5000))
            {
                mr.sortingLayerName = "player";
                mr.sortingOrder = 5000;
            }
        }
    }

    /// <summary>
    /// Teleports the player to a target position, updating Transform, Rigidbody2D, and Camera.
    /// </summary>
    public void Teleport(Vector3 newPosition)
    {
        Vector3 oldPosition = transform.position;

        // Visual teleport effects: Depart at old position, arrive at new position
        ProceduralEffectsGenerator.CreateTeleportDepartEffect(oldPosition);
        ProceduralEffectsGenerator.CreateTeleportArriveEffect(newPosition);

        if (IsSpawned)
        {
            if (IsServer)
            {
                TeleportFxClientRpc(oldPosition, newPosition);
            }
            else
            {
                TeleportFxServerRpc(oldPosition, newPosition);
            }
        }

        transform.position = new Vector3(newPosition.x, newPosition.y, 0f);
        if (rb != null)
        {
            rb.position = (Vector2)newPosition;
            rb.velocity = Vector2.zero;
        }

        bool isHumanPlayer = IsLocal && !CompareTag("Bot") && GetComponent<AiBotController>() == null && !gameObject.name.ToLower().Contains("bot");
        if (CameraController.Instance != null && isHumanPlayer)
        {
            CameraController.Instance.SetTarget(transform);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeleportFxServerRpc(Vector3 fromPos, Vector3 toPos)
    {
        TeleportFxClientRpc(fromPos, toPos);
    }

    [ClientRpc]
    private void TeleportFxClientRpc(Vector3 fromPos, Vector3 toPos)
    {
        if (IsOwner) return; // Local player already executed FX locally
        ProceduralEffectsGenerator.CreateTeleportDepartEffect(fromPos);
        ProceduralEffectsGenerator.CreateTeleportArriveEffect(toPos);
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
        // Apply movement when Rigidbody2D is present and simulated
        if (rb != null && rb.simulated)
        {
            if (RelayNetworkManager.IsMigrating)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 velocity = moveInput * moveSpeed;
            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.position += velocity * Time.fixedDeltaTime;
                transform.position = new Vector3(rb.position.x, rb.position.y, 0f);
            }
            else
            {
                rb.velocity = velocity;
            }
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

    private static Sprite proceduralGhostSprite;

    public static Sprite GetProceduralGhostSprite()
    {
        if (proceduralGhostSprite != null) return proceduralGhostSprite;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        float cx = size * 0.5f;
        float headY = size * 0.60f;
        float radiusX = size * 0.22f; // ~56 pixels radius for slender cute proportion
        float radiusY = size * 0.25f; // ~64 pixels radius
        float bottomY = size * 0.16f;

        Color ghostColorTop = new Color(0.92f, 0.98f, 1.0f, 0.94f);    // Glowing celestial white
        Color ghostColorBottom = new Color(0.72f, 0.90f, 1.0f, 0.75f); // Translucent spiritual cyan
        Color glowColor = new Color(0.5f, 0.85f, 1.0f, 0.35f);         // Ethereal aura glow
        Color eyeColor = new Color(0.08f, 0.10f, 0.20f, 0.98f);        // Cute dark eyes
        Color eyeHighlight = new Color(1.0f, 1.0f, 1.0f, 0.98f);       // Sparkle catchlights
        Color blushColor = new Color(0.55f, 0.82f, 1.0f, 0.35f);       // Soft celestial blush

        for (int y = 0; y < size; y++)
        {
            float normY = Mathf.Clamp01((y - bottomY) / (size * 0.88f - bottomY));
            Color baseColor = Color.Lerp(ghostColorBottom, ghostColorTop, normY);

            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;

                // 1. Head dome (smooth semi-ellipse)
                if (y >= headY)
                {
                    float dy = y - headY;
                    float ellipseDist = (dx * dx) / (radiusX * radiusX) + (dy * dy) / (radiusY * radiusY);
                    if (ellipseDist <= 1.0f)
                    {
                        float edgeDist = 1.0f - Mathf.Sqrt(ellipseDist);
                        float alpha = Mathf.Clamp01(edgeDist * (radiusX / 2.0f));
                        tex.SetPixel(x, y, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
                    }
                    else if (ellipseDist <= 1.25f)
                    {
                        float auraAlpha = Mathf.Clamp01((1.25f - ellipseDist) * 3f) * 0.22f;
                        tex.SetPixel(x, y, new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * auraAlpha));
                    }
                }
                // 2. Body & tapered undulating skirt
                else if (y >= bottomY)
                {
                    float progressDown = 1f - Mathf.Clamp01((y - bottomY) / (headY - bottomY));
                    // 3 soft ripples at the hem
                    float wave = Mathf.Sin(((x - cx) / (radiusX * 1.15f)) * Mathf.PI * 3f) * (7f * progressDown);
                    float currentWidth = radiusX * (1.0f + progressDown * 0.12f);

                    if (y + wave >= bottomY && Mathf.Abs(dx) <= currentWidth)
                    {
                        float edgeX = currentWidth - Mathf.Abs(dx);
                        float edgeY = (y + wave) - bottomY;
                        float edgeDist = Mathf.Min(edgeX, edgeY);
                        float alpha = Mathf.Clamp01(edgeDist / 2.5f);
                        tex.SetPixel(x, y, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
                    }
                    else if (Mathf.Abs(dx) <= currentWidth + 5f && y + wave >= bottomY - 5f)
                    {
                        tex.SetPixel(x, y, new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * 0.18f));
                    }
                }
            }
        }

        // 3. Cute vertical oval eyes
        int eyeY = Mathf.RoundToInt(headY + 4f);
        int leftEyeX = Mathf.RoundToInt(cx - 15f);
        int rightEyeX = Mathf.RoundToInt(cx + 15f);
        float eyeRadX = 5.5f;
        float eyeRadY = 9f;

        for (int ey = -12; ey <= 12; ey++)
        {
            for (int ex = -8; ex <= 8; ex++)
            {
                float eyeDist = (ex * ex) / (eyeRadX * eyeRadX) + (ey * ey) / (eyeRadY * eyeRadY);
                if (eyeDist <= 1.0f)
                {
                    float aa = Mathf.Clamp01((1f - Mathf.Sqrt(eyeDist)) * 3f);
                    Color curL = tex.GetPixel(leftEyeX + ex, eyeY + ey);
                    Color curR = tex.GetPixel(rightEyeX + ex, eyeY + ey);
                    tex.SetPixel(leftEyeX + ex, eyeY + ey, Color.Lerp(curL, eyeColor, aa));
                    tex.SetPixel(rightEyeX + ex, eyeY + ey, Color.Lerp(curR, eyeColor, aa));
                }
            }
        }

        // 4. White sparkle catchlights in the eyes
        for (int sy = -2; sy <= 2; sy++)
        {
            for (int sx = -2; sx <= 2; sx++)
            {
                if (sx * sx + sy * sy <= 4)
                {
                    tex.SetPixel(leftEyeX + 2 + sx, eyeY + 3 + sy, eyeHighlight);
                    tex.SetPixel(rightEyeX + 2 + sx, eyeY + 3 + sy, eyeHighlight);
                }
            }
        }

        // 5. Soft blush
        int blushY = Mathf.RoundToInt(headY - 8f);
        int blushLX = Mathf.RoundToInt(cx - 24f);
        int blushRX = Mathf.RoundToInt(cx + 24f);
        for (int by = -4; by <= 4; by++)
        {
            for (int bx = -7; bx <= 7; bx++)
            {
                float bDist = (bx * bx) / 49f + (by * by) / 16f;
                if (bDist <= 1.0f)
                {
                    float bAlpha = (1f - bDist) * 0.35f;
                    Color curL = tex.GetPixel(blushLX + bx, blushY + by);
                    Color curR = tex.GetPixel(blushRX + bx, blushY + by);
                    tex.SetPixel(blushLX + bx, blushY + by, Color.Lerp(curL, blushColor, bAlpha));
                    tex.SetPixel(blushRX + bx, blushY + by, Color.Lerp(curR, blushColor, bAlpha));
                }
            }
        }

        tex.Apply();
        proceduralGhostSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.45f), 240f);
        return proceduralGhostSprite;
    }

    public void UpdateGhostVisibility()
    {
        // 1. Find the local viewer
        PlayerController localViewer = LocalPlayer;
        if (localViewer == null)
        {
            foreach (var pc in FindObjectsOfType<PlayerController>())
            {
                if (pc != null && (pc.IsOwner || pc.IsLocal)) { localViewer = pc; break; }
            }
        }

        bool viewerIsGhost = (localViewer != null && localViewer.IsGhost);
        bool thisIsGhost = IsGhost || (isGhostNet != null && isGhostNet.Value);

        if (thisIsGhost)
        {
            // Only ghost viewers can see ghosts! Normal living players CANNOT see ghosts.
            if (ghostVisualContainer != null)
            {
                if (ghostVisualContainer.gameObject.activeSelf != viewerIsGhost)
                {
                    ghostVisualContainer.gameObject.SetActive(viewerIsGhost);
                }
            }

            // Name tag visibility:
            if (nameTagTMP != null && nameTagTMP.gameObject != null)
            {
                if (!viewerIsGhost)
                {
                    // Living player cannot see ghost name tags
                    if (nameTagTMP.gameObject.activeSelf) nameTagTMP.gameObject.SetActive(false);
                }
                else
                {
                    // Ghost viewer sees other ghosts with an ethereal cyan tag (local ghost hides its own tag)
                    bool isThisLocal = IsLocal || IsOwner || IsLocalPlayer || (LocalPlayer == this);
                    if (isThisLocal)
                    {
                        if (nameTagTMP.gameObject.activeSelf) nameTagTMP.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (!nameTagTMP.gameObject.activeSelf) nameTagTMP.gameObject.SetActive(true);
                        string dName = playerName != null && !string.IsNullOrEmpty(playerName.Value.ToString()) ? playerName.Value.ToString() : gameObject.name;
                        nameTagTMP.text = $"{dName}\n<color=#80d4ff>[GHOST]</color>";
                    }
                }
            }
        }
        else
        {
            // Living player: Always visible to everyone (both living players and ghosts can see living players)
            bool isThisLocal = IsLocal || IsOwner || IsLocalPlayer || (LocalPlayer == this);
            if (isThisLocal)
            {
                if (nameTagTMP != null && nameTagTMP.gameObject.activeSelf)
                {
                    nameTagTMP.gameObject.SetActive(false);
                }
            }
            else
            {
                if (nameTagTMP != null)
                {
                    if (!nameTagTMP.gameObject.activeSelf) nameTagTMP.gameObject.SetActive(true);
                    if (!nameTagTMP.enabled) nameTagTMP.enabled = true;
                }
            }
        }
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

        if (IsOwner || IsLocalPlayer)
        {
            if (isGhostNet != null && isGhostNet.Value) isGhostNet.Value = false;
        }

        if (IsLocal)
        {
            if (MobileInputManager.Instance != null) MobileInputManager.Instance.SetGhostUI(false);
            if (HUDManager.Instance != null) HUDManager.Instance.SetGhostUI(false);
        }

        UpdateGhostVisibility();
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

        // 4. Show translucent floating ghost sprite on the GhostVisualContainer
        Sprite s = null;
        if (ghostSprite != null && !ghostSprite.name.ToLower().Contains("skin") && !ghostSprite.texture.name.ToLower().Contains("skin") && (ghostSprite.name.ToLower().Contains("ghost") || ghostSprite.texture.name.ToLower().Contains("ghost")))
        {
            s = ghostSprite;
        }
        else
        {
            s = GetProceduralGhostSprite();
        }

        SpriteRenderer ghostSR = ghostChild.GetComponent<SpriteRenderer>();
        if (ghostSR == null) ghostSR = ghostChild.gameObject.AddComponent<SpriteRenderer>();

        ghostSR.sprite = s;
        ghostSR.color = new Color(0.92f, 0.98f, 1.0f, 0.92f);
        ghostSR.sortingLayerName = "player"; // Render on "player" layer
        ghostSR.sortingOrder = 1000;         // Render in front of ground and objects
        ghostChild.localScale = new Vector3(0.95f, 0.95f, 1f);

        if (IsOwner || IsLocalPlayer)
        {
            if (isGhostNet != null && !isGhostNet.Value) isGhostNet.Value = true;
        }

        UpdateGhostVisibility();

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

    // ─── Audio & Sound System ──────────────────────────────────────────────

    public void SetupAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        audioSource.playOnAwake = false;
        // 3D Spatial Audio: sounds emit directly from this player's world position
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 2.5f;
        audioSource.maxDistance = 16.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0f;

        if (footstepAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            foreach (var s in sources)
            {
                if (s != audioSource)
                {
                    footstepAudioSource = s;
                    break;
                }
            }
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.spatialBlend = 1.0f;
        footstepAudioSource.minDistance = 2.5f;
        footstepAudioSource.maxDistance = 16.0f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.dopplerLevel = 0f;

        if (audioListener == null)
        {
            audioListener = GetComponent<AudioListener>();
            if (audioListener == null)
            {
                audioListener = GetComponentInChildren<AudioListener>();
            }
            if (audioListener == null && (IsLocal || !isLocalCached))
            {
                audioListener = gameObject.AddComponent<AudioListener>();
            }
        }

        // Enable AudioListener ONLY on the local player and disable camera listeners to prevent dual listener warnings
        if (audioListener != null)
        {
            audioListener.enabled = IsLocal;

            if (IsLocal)
            {
                AudioListener[] allListeners = FindObjectsOfType<AudioListener>();
                foreach (var al in allListeners)
                {
                    if (al != audioListener)
                    {
                        al.enabled = false;
                    }
                }
            }
        }
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (audioSource == null) SetupAudio();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    public void PlayGrenadeThrowSound()
    {
        if (grenadeThrowClip != null)
        {
            PlaySound(grenadeThrowClip);
        }
    }

    public void PlayPickupSound(AudioClip customClip = null)
    {
        AudioClip clipToPlay = customClip != null ? customClip : defaultPickupClip;
        if (clipToPlay != null)
        {
            PlaySound(clipToPlay);
        }
    }

    public void PlayDropSound(AudioClip customClip = null)
    {
        AudioClip clipToPlay = customClip != null ? customClip : defaultDropClip;
        if (clipToPlay != null)
        {
            PlaySound(clipToPlay);
        }
    }

    private void HandleFootstepSounds()
    {
        if (IsGhost || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead))
        {
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
            if (movementDust != null)
            {
                movementDust.SetEmitting(false);
            }
            return;
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Check if walking animation parameter or state is active
        bool isWalkingAnim = false;
        if (animator != null && animator.enabled)
        {
            bool animParam = false;
            try
            {
                animParam = animator.GetBool("isWalking");
            }
            catch { }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isWalkState = stateInfo.IsName("walk") || stateInfo.IsName("Walk") || 
                               stateInfo.IsName("walking") || stateInfo.IsName("Walking") || 
                               stateInfo.IsName("Player_Walk") || stateInfo.IsName("run") || 
                               stateInfo.IsName("Run");

            isWalkingAnim = animParam || isWalkState;
        }
        else
        {
            isWalkingAnim = isMoving || (moveInput.sqrMagnitude > 0.01f);
        }

        // Must also have actual physics velocity, input, or movement flag
        bool isPhysicallyMoving = isMoving || (moveInput.sqrMagnitude > 0.01f) || (rb != null && rb.simulated && rb.velocity.magnitude > 0.05f);

        bool isWalkingAnimationPlaying = isWalkingAnim && isPhysicallyMoving;

        if (movementDust != null)
        {
            movementDust.SetEmitting(isWalkingAnimationPlaying);
        }

        if (isWalkingAnimationPlaying && footstepClip != null)
        {
            if (footstepAudioSource == null) SetupAudio();

            if (footstepAudioSource != null)
            {
                if (!footstepAudioSource.isPlaying || footstepAudioSource.clip != footstepClip)
                {
                    footstepAudioSource.clip = footstepClip;
                    footstepAudioSource.loop = true;
                    footstepAudioSource.volume = 0.5f;
                    float interval = footstepInterval > 0f ? footstepInterval : 0.4f;
                    footstepAudioSource.pitch = 0.4f / interval;
                    footstepAudioSource.Play();
                }
            }
        }
        else
        {
            if (footstepAudioSource != null && footstepAudioSource.isPlaying)
            {
                footstepAudioSource.Stop();
            }
        }
    }
}
