using UnityEngine;

/// <summary>
/// Attach this to any GameObject in the scene and resize/shape its
/// PolygonCollider2D (or BoxCollider2D) to exactly cover the walkable
/// floor area.
///
/// isGroundFloor = true  → Hostages spawn here
/// isGroundFloor = false → Thieves spawn here
///
/// isRoom = true + roomId → Mark this zone as a Room. Keys spawn in 2 distinct rooms only.
///
/// Also used by FloorItemSpawner as a whitelist zone for item drops.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WalkableFloorZone : MonoBehaviour
{
    [HideInInspector] public Collider2D zoneCollider;

    [Header("Floor Settings")]
    [Tooltip("Check this if this zone is on the GROUND FLOOR.\n" +
             "Hostages will spawn ONLY in ground floor zones.\n" +
             "Thieves will spawn in NON-ground floor zones.")]
    public bool isGroundFloor = false;

    [Header("Room Settings")]
    [Tooltip("Check this if this zone represents a ROOM (not a corridor or open hall).\n" +
             "Keys will ONLY spawn in zones where isRoom = true.")]
    public bool isRoom = false;

    [Tooltip("Unique ID or name for this room (e.g. 'room_1', 'room_2', 'kitchen').\n" +
             "Keys spawn in 2 DIFFERENT room IDs so both keys are never in the same room.")]
    public string roomId = "";

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();

        // Must be a trigger — items and players should not be blocked by it
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        // Keep trigger flag enforced in editor too
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Returns true if the 2D world point lies inside this zone.</summary>
    public bool ContainsPoint(Vector2 point)
    {
        if (zoneCollider == null) zoneCollider = GetComponent<Collider2D>();
        return zoneCollider != null && zoneCollider.OverlapPoint(point);
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Color fill, wire;
        if (isRoom)
        {
            // Room zones get Purple / Amber highlight
            fill = new Color(0.8f, 0.4f, 1f, 0.2f);
            wire = new Color(0.8f, 0.4f, 1f, 0.9f);
        }
        else
        {
            // Ground floor = BLUE gizmo, Upper floor = GREEN gizmo
            fill = isGroundFloor ? new Color(0.2f, 0.5f, 1f, 0.15f) : new Color(0f, 1f, 0.3f, 0.15f);
            wire = isGroundFloor ? new Color(0.2f, 0.5f, 1f, 0.9f) : new Color(0f, 1f, 0.3f, 0.9f);
        }

        Gizmos.color = fill;
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = wire;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.color = wire;
        string labelText;
        if (isRoom)
        {
            string idStr = string.IsNullOrEmpty(roomId) ? gameObject.name : roomId;
            labelText = $"[ROOM: {idStr}]";
        }
        else
        {
            labelText = isGroundFloor ? "[Ground Floor - Hostage]" : "[Upper Floor - Thief]";
        }

        // Display the label UNDER the box as requested
        UnityEditor.Handles.Label(
            col.bounds.center - Vector3.up * (col.bounds.extents.y + 0.3f),
            labelText);
#endif
    }
}
