#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class PickupCreator
{
    [MenuItem("GameObject/CounterBoom/Create Pickup", false, 10)]
    public static void CreatePickup()
    {
        // Create new GameObject
        GameObject go = new GameObject("New Item Pickup");
        
        // Add Sprite Renderer (visuals)
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        // Load a default sprite if possible, or just leave empty for user to assign
        
        // Add Box Collider 2D (Trigger)
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1f, 1f);
        
        // Add ItemPickup script
        ItemPickup pickup = go.AddComponent<ItemPickup>();
        
        // Position it near the scene view camera
        if (SceneView.lastActiveSceneView != null)
        {
            go.transform.position = SceneView.lastActiveSceneView.pivot;
            go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, 0f);
        }
        else
        {
            go.transform.position = Vector3.zero;
        }

        // Select it
        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Pickup");
        
        Debug.Log("Created new Item Pickup! Please assign ItemData and Sprite.");
    }
}
#endif
