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
    private Camera cam;                     // Cached — no GetComponent per frame

    private float defaultOrthoSize;
    private float targetOrthoSize;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            defaultOrthoSize = cam.orthographicSize;
            targetOrthoSize  = defaultOrthoSize;
        }
    }

    /// <summary>
    /// Sets the target for the camera to follow (usually the local Player).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        // Instant snap so there is no lerp from 0,0,0 on first frame
        if (target != null)
        {
            transform.position = target.position + offset;
            Debug.Log($"[CameraController] Now following: {target.name}");
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

        Vector3 desiredPosition  = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        if (useBoundaries)
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

    private void FindLocalPlayerTarget()
    {
        if (IsSpectating) return;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null)
            {
                bool isLocalPlayer = p.IsLocal;
                
                var netObj = p.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsLocalPlayer) isLocalPlayer = true;
                
                if (isLocalPlayer)
                {
                    SetTarget(p.transform);
                    break;
                }
            }
        }
    }
}

