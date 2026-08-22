using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);

    [Header("Map Boundaries (Optional)")]
    [SerializeField] private bool useBoundaries = false;
    [SerializeField] private Vector2 minPosition;
    [SerializeField] private Vector2 maxPosition;

    private Transform target;
    private Rigidbody2D targetRb;
    private Camera cam;                     // Cached — no GetComponent per frame

    private float defaultOrthoSize;
    private float targetOrthoSize;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            defaultOrthoSize = cam.orthographicSize;
            targetOrthoSize  = defaultOrthoSize;
        }

        // Auto-find player if not already assigned
        if (target == null)
        {
            FindLocalPlayerTarget();
        }
    }

    /// <summary>
    /// Sets the target for the camera to follow (usually the local Player).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null && !IsSpectating)
        {
            if (newTarget.CompareTag("Bot") || newTarget.GetComponent<AiBotController>() != null || newTarget.name.ToLower().Contains("bot"))
            {
                return;
            }
        }

        target = newTarget;
        targetRb = target != null ? target.GetComponent<Rigidbody2D>() : null;

        if (target != null)
        {
            Vector2 pos = targetRb != null ? targetRb.position : (Vector2)target.position;
            Vector3 snapPos = new Vector3(pos.x + offset.x, pos.y + offset.y, offset.z);
            if (useBoundaries && (minPosition != Vector2.zero || maxPosition != Vector2.zero))
            {
                snapPos.x = Mathf.Clamp(snapPos.x, minPosition.x, maxPosition.x);
                snapPos.y = Mathf.Clamp(snapPos.y, minPosition.y, maxPosition.y);
            }
            transform.position = snapPos;
            Debug.Log($"[CameraController] ✅ Now following: {target.name} at {pos}");
        }
        else
        {
            Debug.Log("[CameraController] Target cleared.");
        }
    }

    /// <summary>
    /// Updates the camera zoom multiplier.
    /// zoomMultiplier = 1 → default size.
    /// zoomMultiplier = 2 → 2× zoom-out (sees more area).
    /// </summary>
    public void SetZoom(float zoomMultiplier)
    {
        targetOrthoSize = defaultOrthoSize * zoomMultiplier;
        Debug.Log($"[CameraController] Zoom set to {zoomMultiplier}x (OrthoSize: {targetOrthoSize})");
    }

    public bool IsSpectating { get; set; } = false;
    private System.Collections.Generic.List<Transform> spectateTargets = new System.Collections.Generic.List<Transform>();
    private int currentSpectateIndex = 0;

    /// <summary>
    /// Starts spectating other alive players in the match.
    /// </summary>
    public void StartSpectating()
    {
        IsSpectating = true;
        RefreshSpectateTargets();
        if (spectateTargets.Count > 0)
        {
            currentSpectateIndex = 0;
            SetTarget(spectateTargets[currentSpectateIndex]);
        }
        else
        {
            Debug.Log("[CameraController] No other alive players to spectate.");
        }
    }

    public void SpectateNextTarget()
    {
        RefreshSpectateTargets();
        if (spectateTargets.Count == 0) return;

        currentSpectateIndex = (currentSpectateIndex + 1) % spectateTargets.Count;
        SetTarget(spectateTargets[currentSpectateIndex]);
    }

    public void SpectatePreviousTarget()
    {
        RefreshSpectateTargets();
        if (spectateTargets.Count == 0) return;

        currentSpectateIndex--;
        if (currentSpectateIndex < 0) currentSpectateIndex = spectateTargets.Count - 1;
        SetTarget(spectateTargets[currentSpectateIndex]);
    }

    public string GetCurrentSpectatedName()
    {
        if (target != null) return target.name;
        return "None";
    }

    private void RefreshSpectateTargets()
    {
        spectateTargets.Clear();
        PlayerHealth[] allHealths = FindObjectsOfType<PlayerHealth>();
        foreach (var h in allHealths)
        {
            if (h != null && !h.IsDead)
            {
                // Only spectate other players (not our own dead player)
                var netObj = h.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsLocalPlayer) continue;

                spectateTargets.Add(h.transform);
            }
        }
    }

    private float shakeTimer = 0f;
    private float shakeMagnitude = 0.2f;

    /// <summary>
    /// Triggers a screen shake effect for dramatic impact (e.g. explosions, player death).
    /// </summary>
    public void TriggerShake(float duration = 0.35f, float magnitude = 0.25f)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        // Auto-find local player target if null (dynamically spawned) and not spectating
        if (target == null && !IsSpectating)
        {
            FindLocalPlayerTarget();
        }

        // Smooth zoom
        if (cam != null && Mathf.Abs(cam.orthographicSize - targetOrthoSize) > 0.01f)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, Time.deltaTime * 5f);
        }

        if (target == null) return;

        Vector2 targetPos = (targetRb != null) ? targetRb.position : (Vector2)target.position;
        Vector3 desiredPosition = new Vector3(targetPos.x + offset.x, targetPos.y + offset.y, offset.z);

        // Robust smooth follow using SmoothDamp (never produces NaN or jitter)
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 0.12f);

        // Only clamp if boundary box is actually defined (not min == max == 0,0)
        if (useBoundaries && (minPosition != Vector2.zero || maxPosition != Vector2.zero))
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minPosition.x, maxPosition.x);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minPosition.y, maxPosition.y);
        }

        // Apply screen shake offset
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            Vector2 randomShake = Random.insideUnitCircle * shakeMagnitude;
            shakeOffset = new Vector3(randomShake.x, randomShake.y, 0f);
        }

        transform.position = smoothedPosition + shakeOffset;
    }

    public void FindLocalPlayerTarget()
    {
        if (IsSpectating) return;

        // 1. Check OfflineManager spawned player
        if (OfflineManager.Instance != null && OfflineManager.Instance.SpawnedPlayer != null)
        {
            SetTarget(OfflineManager.Instance.SpawnedPlayer.transform);
            return;
        }

        // 2. Fast check static LocalPlayer reference
        if (PlayerController.LocalPlayer != null && !PlayerController.LocalPlayer.CompareTag("Bot") && PlayerController.LocalPlayer.GetComponent<AiBotController>() == null)
        {
            SetTarget(PlayerController.LocalPlayer.transform);
            return;
        }

        // 3. Scan scene for local human PlayerController
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && !p.CompareTag("Bot") && p.GetComponent<AiBotController>() == null && !p.name.ToLower().Contains("bot"))
            {
                bool isLocalPlayer = p.IsLocal;
                var netObj = p.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsLocalPlayer) isLocalPlayer = true;
                
                if (isLocalPlayer || players.Length == 1)
                {
                    SetTarget(p.transform);
                    break;
                }
            }
        }
    }
}

