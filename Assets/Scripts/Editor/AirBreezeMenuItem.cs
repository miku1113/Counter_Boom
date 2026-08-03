#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AirBreezeMenuItem
{
    [MenuItem("GameObject/Effects/Air Breeze Particle System", false, 10)]
    [MenuItem("Tools/Counter Boom/Add Air Breeze Particle System", false, 20)]
    public static void AddAirBreezeParticles(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("AirBreezeParticles");
        
        if (menuCommand.context is GameObject parentGO)
        {
            GameObjectUtility.SetParentAndAlign(go, parentGO);
        }
        else if (Selection.activeGameObject != null)
        {
            GameObjectUtility.SetParentAndAlign(go, Selection.activeGameObject);
        }

        go.AddComponent<ParticleSystem>();
        go.AddComponent<AirBreezeParticleSystem>();

        Undo.RegisterCreatedObjectUndo(go, "Create Air Breeze Particles");
        Selection.activeGameObject = go;
        Debug.Log("[AirBreezeParticles] Created Air Breeze Particle System GameObject.");
    }
}
#endif
