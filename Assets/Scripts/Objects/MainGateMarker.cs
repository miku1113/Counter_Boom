using UnityEngine;

/// <summary>
/// Attach this component to your Main Gate object in your scene!
/// It automatically identifies the Main Gate position for spawning, unlocking, and victory escape.
/// </summary>
public class MainGateMarker : MonoBehaviour
{
    private void Awake()
    {
        RegisterGateLocation();
    }

    private void Start()
    {
        RegisterGateLocation();
    }

    public void RegisterGateLocation()
    {
        if (MainGateController.Instance != null)
        {
            MainGateController.Instance.transform.position = transform.position;
        }

        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.mainGateTransform = transform;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.mainGateTransform = transform;
        }

        Debug.Log($"[MainGateMarker] Registered Main Gate location: '{gameObject.name}' at position {transform.position}");
    }
}
