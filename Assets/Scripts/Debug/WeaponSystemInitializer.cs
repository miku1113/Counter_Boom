using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A helper script to quickly set up the Weapon & Inventory system in your scene.
/// Attach this to an empty GameObject and use the Context Menu "Setup Weapon System".
/// </summary>
public class WeaponSystemInitializer : MonoBehaviour
{
    [ContextMenu("Setup Weapon System")]
    public void Setup()
    {
        // 1. Setup Bag Manager
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            Debug.LogError("No Player found with tag 'Player'. Please tag your player GameObject.");
            return;
        }

        BagManager bag = p.GetComponent<BagManager>();
        if (bag == null) bag = p.AddComponent<BagManager>();

        WeaponController wc = p.GetComponentInChildren<WeaponController>();
        
        // 2. Setup UI
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("HUD Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        HUDManager hud = canvas.GetComponent<HUDManager>();
        if (hud == null) hud = canvas.gameObject.AddComponent<HUDManager>();

        BagUI bagUI = canvas.GetComponentInChildren<BagUI>(true);
        if (bagUI == null)
        {
            GameObject bagObj = new GameObject("Bag Panel");
            bagObj.transform.SetParent(canvas.transform, false);
            bagUI = bagObj.AddComponent<BagUI>();
        }

        Debug.Log("Weapon System Components Added. Please assign button references in the HUDManager/BagUI inspectors.");
    }
}
