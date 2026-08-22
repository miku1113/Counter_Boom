using UnityEngine;

/// <summary>
/// Add this to Player to debug why it's being disabled
/// </summary>
public class PlayerDebugHelper : MonoBehaviour
{
    private Vector3 lastPos;

    private void Awake()
    {
        lastPos = transform.position;
        Debug.Log($"[PlayerDebug] Player Awake at {transform.position} - Active: {gameObject.activeSelf}");
    }
    
    private void Start()
    {
        Debug.Log($"[PlayerDebug] Player Start at {transform.position} - Active: {gameObject.activeSelf}");
    }
    
    private void OnEnable()
    {
        Debug.Log($"[PlayerDebug] Player ENABLED at {transform.position} - Time: {Time.time}");
    }
    
    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        Debug.Log($"[PlayerDebug] Player DISABLED at {transform.position} - Time: {Time.time}");
    }

    private void Update()
    {
        if (Vector3.Distance(transform.position, lastPos) > 1.0f)
        {
            Debug.Log($"[PlayerDebug] Player moved to {transform.position}");
            lastPos = transform.position;
        }
    }
}
