using UnityEngine;

/// <summary>
/// Add this to Player to debug why it's being disabled
/// </summary>
public class PlayerDebugHelper : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log($"[PlayerDebug] Player Awake - Active: {gameObject.activeSelf}");
    }
    
    private void Start()
    {
        Debug.Log($"[PlayerDebug] Player Start - Active: {gameObject.activeSelf}");
    }
    
    private void OnEnable()
    {
        Debug.Log($"[PlayerDebug] Player ENABLED at {Time.time}");
        Debug.LogWarning($"[PlayerDebug] Enabled by: {System.Environment.StackTrace}");
    }
    
    private void OnDisable()
    {
        if (!Application.isPlaying) return; // Ignore normal cleanup on Play Mode stop
        Debug.Log($"[PlayerDebug] Player DISABLED at {Time.time}");
    }
}
