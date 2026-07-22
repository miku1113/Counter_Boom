using UnityEngine;

/// <summary>
/// Runtime component for Map Collider setup. 
/// Calls MapColliderFixer.FixAllMapCollidersInScene() on Awake.
/// </summary>
public class MapColliderHelper : MonoBehaviour
{
    private void Awake()
    {
        MapColliderFixer.FixAllMapCollidersInScene();
    }

    [ContextMenu("Fix Map Colliders Now")]
    public void FixMapColliders()
    {
        MapColliderFixer.FixAllMapCollidersInScene();
    }
}
