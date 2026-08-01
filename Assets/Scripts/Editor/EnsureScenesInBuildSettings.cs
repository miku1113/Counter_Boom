#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public static class EnsureScenesInBuildSettings
{
    static EnsureScenesInBuildSettings()
    {
        EditorApplication.delayCall += RegisterScenes;
    }

    [MenuItem("Tools/Ensure Scenes in Build Settings")]
    public static void RegisterScenes()
    {
        string[] requiredScenes = new string[]
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/LoadingGame.unity",
            "Assets/Scenes/CustomLobby.unity",
            "Assets/Scenes/GameScene.unity"
        };

        List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool modified = false;

        foreach (string scenePath in requiredScenes)
        {
            if (System.IO.File.Exists(scenePath))
            {
                bool exists = false;
                foreach (var s in buildScenes)
                {
                    if (s.path == scenePath)
                    {
                        exists = true;
                        if (!s.enabled)
                        {
                            s.enabled = true;
                            modified = true;
                        }
                        break;
                    }
                }

                if (!exists)
                {
                    buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    modified = true;
                    Debug.Log($"[BuildSettings] Added scene to build settings: {scenePath}");
                }
            }
        }

        if (modified)
        {
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("[BuildSettings] Updated EditorBuildSettings scenes.");
        }
    }
}
#endif
