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
    /// Sets the target for the camera to follow (usually the Player).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        // Instant snap to start to avoid fast lerp from 0,0,0
        if (target != null)
        {
            transform.position = target.position + offset;
        }
        
        Debug.Log($"[CameraController] Now following: {newTarget.name}");
    }

    private float defaultOrthoSize;
    private float targetOrthoSize;
    
    private void Start()
    {
        // Cache default size
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            defaultOrthoSize = cam.orthographicSize;
            targetOrthoSize = defaultOrthoSize;
        }
    }

    /// <summary>
    /// Updates the camera zoom multiplier.
    /// zoomMultiplier = 1 means default size
    /// zoomMultiplier = 2 means 2x zoom OUT (sees more area)
    /// </summary>
    public void SetZoom(float zoomMultiplier)
    {
        targetOrthoSize = defaultOrthoSize * zoomMultiplier;
        Debug.Log($"[CameraController] Zoom set to {zoomMultiplier}x (OrthoSize: {targetOrthoSize})");
    }

    private void LateUpdate()
    {
        // Update Zoom
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            if (Mathf.Abs(cam.orthographicSize - targetOrthoSize) > 0.01f)
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, Time.deltaTime * 5f);
            }
        }
        
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        
        if (useBoundaries)
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minPosition.x, maxPosition.x);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minPosition.y, maxPosition.y);
        }

        transform.position = smoothedPosition;
    }
}
