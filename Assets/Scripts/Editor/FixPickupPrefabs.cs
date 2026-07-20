#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using System.IO;

[InitializeOnLoad]
public static class FixPickupPrefabs
{
    static FixPickupPrefabs()
    {
        EditorApplication.delayCall += FixPrefabsInFolder;
    }

    [MenuItem("Tools/Fix Pickup Prefabs")]
    public static void FixPrefabsInFolder()
    {
        string folderPath = "Assets/Prefab/Weapon/items";
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[FixPickupPrefabs] Directory not found: {folderPath}");
            return;
        }

        string[] files = Directory.GetFiles(folderPath, "*.prefab");
        bool changesMade = false;

        foreach (string file in files)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
            if (prefab == null) continue;

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.Log($"[FixPickupPrefabs] Detected missing NetworkObject on '{prefab.name}'. Adding now...");
                GameObject root = PrefabUtility.LoadPrefabContents(file);
                
                if (root.GetComponent<NetworkObject>() == null)
                {
                    root.AddComponent<NetworkObject>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, file);
                PrefabUtility.UnloadPrefabContents(root);
                changesMade = true;
            }
        }

        if (changesMade)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FixPickupPrefabs] Successfully updated all pickup prefabs with NetworkObject component.");
        }
    }
}
#endif
