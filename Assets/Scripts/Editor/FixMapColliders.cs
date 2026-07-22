#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class FixMapColliders
{
    [MenuItem("Tools/Fix Map Colliders in Scene")]
    public static void FixAllMapCollidersInScene()
    {
        int converted3DCount = 0;
        int fixedTriggerCount = 0;

        // Scan all 3D BoxColliders in the active scene
        BoxCollider[] colliders3D = Object.FindObjectsOfType<BoxCollider>(true);
        foreach (BoxCollider col3D in colliders3D)
        {
            GameObject go = col3D.gameObject;

            // Skip player objects or UI elements
            if (go.GetComponent<Canvas>() != null || go.GetComponentInParent<Canvas>() != null) continue;

            Vector3 center = col3D.center;
            Vector3 size = col3D.size;
            bool wasTrigger = col3D.isTrigger;

            Undo.DestroyObjectImmediate(col3D);

            BoxCollider2D col2D = go.GetComponent<BoxCollider2D>();
            if (col2D == null)
            {
                col2D = Undo.AddComponent<BoxCollider2D>(go);
            }

            col2D.offset = new Vector2(center.x, center.y);
            col2D.size = new Vector2(size.x, size.y);
            col2D.isTrigger = false; // Force solid collisions for map walls and obstacles

            converted3DCount++;
        }

        // Scan all existing 2D Colliders and ensure IsTrigger is false for map walls
        Collider2D[] colliders2D = Object.FindObjectsOfType<Collider2D>(true);
        foreach (Collider2D col2D in colliders2D)
        {
            GameObject go = col2D.gameObject;
            if (go.CompareTag("Player") || go.GetComponent<ItemPickup>() != null || go.GetComponent<Canvas>() != null || go.GetComponentInParent<Canvas>() != null)
            {
                continue; // Skip pickups and player triggers
            }

            Rigidbody2D mapRb = go.GetComponent<Rigidbody2D>();
            if (mapRb != null && mapRb.bodyType == RigidbodyType2D.Dynamic)
            {
                Undo.RecordObject(mapRb, "Set Static BodyType");
                mapRb.bodyType = RigidbodyType2D.Static;
                Debug.Log($"[FixMapColliders] Set Rigidbody2D BodyType to Static on '{go.name}'.");
            }

            if (col2D.isTrigger)
            {
                Undo.RecordObject(col2D, "Uncheck IsTrigger");
                col2D.isTrigger = false;
                fixedTriggerCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[FixMapColliders] SUCCESS! Converted {converted3DCount} 3D BoxColliders to 2D BoxCollider2Ds, and set {fixedTriggerCount} triggers to solid map walls.");
        EditorUtility.DisplayDialog("Map Colliders Fixed!", 
            $"Successfully converted {converted3DCount} 3D BoxColliders to 2D BoxCollider2Ds and enforced solid wall collisions across the map.\n\nSave your scene now!", "OK");
    }
}
#endif
