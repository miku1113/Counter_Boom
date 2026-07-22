using UnityEngine;

/// <summary>
/// Static utility that runs automatically on scene load to convert any 3D BoxColliders 
/// on map objects into solid 2D BoxCollider2Ds at runtime.
/// </summary>
public static class MapColliderFixer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoFixSceneColliders()
    {
        FixAllMapCollidersInScene();
    }

    public static void FixAllMapCollidersInScene()
    {
        int converted3DCount = 0;
        int fixedTriggerCount = 0;

        // 1. Find all 3D BoxColliders in the scene (excluding UI and player triggers)
        BoxCollider[] colliders3D = Object.FindObjectsOfType<BoxCollider>(true);
        foreach (BoxCollider col3D in colliders3D)
        {
            if (col3D == null) continue;
            GameObject go = col3D.gameObject;

            // Skip Canvas UI elements
            if (go.GetComponent<Canvas>() != null || go.GetComponentInParent<Canvas>() != null) continue;

            Vector3 center = col3D.center;
            Vector3 size = col3D.size;

            Object.Destroy(col3D);

            BoxCollider2D col2D = go.GetComponent<BoxCollider2D>();
            if (col2D == null)
            {
                col2D = go.AddComponent<BoxCollider2D>();
            }

            col2D.offset = new Vector2(center.x, center.y);
            col2D.size = new Vector2(size.x, size.y);
            col2D.isTrigger = false;

            converted3DCount++;
        }

        // 2. Ensure non-pickup/non-player 2D Colliders are solid (isTrigger = false)
        Collider2D[] colliders2D = Object.FindObjectsOfType<Collider2D>(true);
        foreach (Collider2D col2D in colliders2D)
        {
            if (col2D == null) continue;
            GameObject go = col2D.gameObject;

            // Skip player, pickup items, or canvas elements
            if (go.CompareTag("Player") || go.GetComponent<ItemPickup>() != null || go.GetComponent<Canvas>() != null || go.GetComponentInParent<Canvas>() != null)
            {
                continue;
            }

            // Remove or freeze dynamic Rigidbody2D on map obstacles so gravity doesn't pull them out of the world!
            Rigidbody2D mapRb = go.GetComponent<Rigidbody2D>();
            if (mapRb != null)
            {
                if (mapRb.bodyType == RigidbodyType2D.Dynamic)
                {
                    mapRb.bodyType = RigidbodyType2D.Static;
                    Debug.Log($"[MapColliderFixer] Set Rigidbody2D BodyType to Static on map obstacle '{go.name}'.");
                }
            }

            if (col2D.isTrigger)
            {
                col2D.isTrigger = false;
                fixedTriggerCount++;
            }
        }

        if (converted3DCount > 0 || fixedTriggerCount > 0)
        {
            Debug.Log($"[MapColliderFixer] Auto-Fixed Map Physics: Converted {converted3DCount} 3D BoxColliders to 2D BoxCollider2Ds, and set {fixedTriggerCount} triggers to solid map walls.");
        }
    }
}
