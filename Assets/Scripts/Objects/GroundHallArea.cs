using UnityEngine;

/// <summary>
/// Attach this component to ANY GameObject in your scene to designate it as the Ground Hall Area!
/// It automatically registers its position for Hostage spawning and cutscenes.
/// </summary>
public class GroundHallArea : MonoBehaviour
{
    private void Awake()
    {
        RegisterHallLocation();
    }

    private void Start()
    {
        RegisterHallLocation();
    }

    public void RegisterHallLocation()
    {
        if (MatchRoleManager.Instance != null)
        {
            MatchRoleManager.Instance.groundHallTransform = transform;
            MatchRoleManager.Instance.groundFloorCenter = transform.position;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.groundHallTransform = transform;
        }

        Debug.Log($"[GroundHallArea] Registered Ground Hall location: '{gameObject.name}' at position {transform.position}");
    }
}
