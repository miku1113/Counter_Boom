#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Netcode;

[InitializeOnLoad]
public static class FixPlayerPrefab
{
    static FixPlayerPrefab()
    {
        // Runs automatically when the project compiles or loads
        EditorApplication.delayCall += FixPrefab;
    }

    [MenuItem("Tools/Fix Player Prefab")]
    public static void FixPrefab()
    {
        string path = "Assets/Prefab/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[FixPlayerPrefab] Could not find Player prefab at {path}");
            return;
        }

        // Find and sort all CharacterSkinData in the project
        string[] guids = AssetDatabase.FindAssets("t:CharacterSkinData");
        System.Collections.Generic.List<CharacterSkinData> skinList = new System.Collections.Generic.List<CharacterSkinData>();
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<CharacterSkinData>(p);
            if (asset != null) skinList.Add(asset);
        }
        skinList.Sort((a, b) => {
            bool aDefault = a.skinName != null && a.skinName.ToLower().Contains("default");
            bool bDefault = b.skinName != null && b.skinName.ToLower().Contains("default");
            if (aDefault && !bDefault) return -1;
            if (!aDefault && bDefault) return 1;
            return string.Compare(a.skinName ?? "", b.skinName ?? "", System.StringComparison.Ordinal);
        });

        CharacterAssembler prefabAssembler = prefab.GetComponentInChildren<CharacterAssembler>();
        bool skinsOutdated = false;
        if (prefabAssembler != null)
        {
            if (prefabAssembler.availableSkins == null || prefabAssembler.availableSkins.Length != skinList.Count)
            {
                skinsOutdated = true;
            }
            else
            {
                for (int i = 0; i < skinList.Count; i++)
                {
                    if (prefabAssembler.availableSkins[i] != skinList[i])
                    {
                        skinsOutdated = true;
                        break;
                    }
                }
            }
        }

        bool needsModify = false;
        if (prefab.GetComponent<NetworkObject>() == null || 
            prefab.GetComponent<ClientNetworkTransform>() == null ||
            prefab.GetComponent<OwnerNetworkAnimator>() == null ||
            skinsOutdated)
        {
            needsModify = true;
        }

        if (needsModify)
        {
            Debug.Log("[FixPlayerPrefab] Detected missing network components or outdated skins on Player prefab. Updating prefab now...");
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

            if (prefabRoot.GetComponent<NetworkObject>() == null)
            {
                prefabRoot.AddComponent<NetworkObject>();
                Debug.Log("[FixPlayerPrefab] Added NetworkObject component.");
            }

            if (prefabRoot.GetComponent<ClientNetworkTransform>() == null)
            {
                prefabRoot.AddComponent<ClientNetworkTransform>();
                Debug.Log("[FixPlayerPrefab] Added ClientNetworkTransform component.");
            }

            if (prefabRoot.GetComponent<OwnerNetworkAnimator>() == null)
            {
                var ona = prefabRoot.AddComponent<OwnerNetworkAnimator>();
                var anim = prefabRoot.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    var so = new SerializedObject(ona);
                    so.FindProperty("m_Animator").objectReferenceValue = anim;
                    so.ApplyModifiedProperties();
                    Debug.Log("[FixPlayerPrefab] Added OwnerNetworkAnimator component and assigned Animator reference.");
                }
                else
                {
                    Debug.LogWarning("[FixPlayerPrefab] Added OwnerNetworkAnimator but could not find Animator on prefab or children.");
                }
            }

            var assembler = prefabRoot.GetComponentInChildren<CharacterAssembler>();
            if (assembler != null)
            {
                assembler.availableSkins = skinList.ToArray();
                Debug.Log($"[FixPlayerPrefab] Updated availableSkins on Player prefab with {skinList.Count} sorted skins.");
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log("[FixPlayerPrefab] Saved Player prefab changes.");
        }
    }
}
#endif
