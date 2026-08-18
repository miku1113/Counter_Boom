using UnityEngine;

/// <summary>
/// Helper component attached to safe spawn points in scene for visual editor gizmos and auto-discovery.
/// </summary>
public class SafePointMarker : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 0.8f, 0.1f));
    }
}
