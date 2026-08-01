#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FireEmberMenuItem
{
    [MenuItem("GameObject/Effects/Fire Ember Particles (Sparks & Heat Drift)", false, 10)]
    [MenuItem("Tools/Counter Boom/Add Fire Ember Particle Effect", false, 20)]
    public static void AddFireEmberParticles(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("FireEmberParticles");
        
        // Position relative to selection or scene view center
        if (menuCommand.context is GameObject parentGO)
        {
            GameObjectUtility.SetParentAndAlign(go, parentGO);
        }
        else if (Selection.activeGameObject != null)
        {
            GameObjectUtility.SetParentAndAlign(go, Selection.activeGameObject);
        }
        else
        {
            // Position in center of active scene camera view if available
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                Vector3 cameraPos = view.camera.transform.position;
                cameraPos.z = 0f; // 2D level plane
                go.transform.position = cameraPos;
            }
            else
            {
                go.transform.position = new Vector3(0f, -2f, 0f);
            }
        }

        FireEmberParticleSystem sys = go.AddComponent<FireEmberParticleSystem>();
        sys.ConfigureParticleSystem();

        Undo.RegisterCreatedObjectUndo(go, "Create Fire Ember Particles");
        Selection.activeGameObject = go;
        Debug.Log("[FireEmberParticles] Created glowing Fire Ember Particle Effect in scene!");
    }
}
#endif
