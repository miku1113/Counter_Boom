#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlayerDustSetupUtility
{
    private const string SOFT_SMOKE_MAT_PATH = "Assets/Prefab/Effects/MoveDustSoft.mat";
    private const string MOVE_DUST_PREFAB_PATH = "Assets/Prefab/Effects/MoveDust.prefab";
    private static readonly string[] PLAYER_PREFAB_PATHS = new string[]
    {
        "Assets/Prefab/Player.prefab",
        "Assets/Resources/Player.prefab"
    };

    [MenuItem("Tools/Counter Boom/Setup Player Leg Dust Particles", false, 15)]
    [MenuItem("GameObject/Effects/Setup Player Leg Dust Particles", false, 25)]
    public static void SetupPlayerLegDustParticles()
    {
        Debug.Log("[PlayerDustSetup] Starting configuration of MoveDust and Player prefabs...");

        Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>(SOFT_SMOKE_MAT_PATH);
        if (smokeMat == null)
        {
            smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MoveDustSoft.mat");
        }

        // 1. Configure MoveDust.prefab
        ConfigureMoveDustPrefab(smokeMat);

        // 2. Configure Player prefabs
        foreach (string prefabPath in PLAYER_PREFAB_PATHS)
        {
            ConfigurePlayerPrefab(prefabPath, smokeMat);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerDustSetup] Successfully completed setup for all player prefabs and MoveDust!");
    }

    [InitializeOnLoadMethod]
    private static void AutoRunOnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("PlayerDustSetup_Done", false))
            {
                SessionState.SetBool("PlayerDustSetup_Done", true);
                SetupPlayerLegDustParticles();
            }
        };
    }

    public static void ConfigureMoveDustPrefab(Material smokeMat)
    {
        GameObject moveDustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MOVE_DUST_PREFAB_PATH);
        if (moveDustPrefab == null)
        {
            Debug.LogError($"[PlayerDustSetup] Could not find MoveDust prefab at {MOVE_DUST_PREFAB_PATH}");
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(MOVE_DUST_PREFAB_PATH);
        try
        {
            ParticleSystem ps = instance.GetComponent<ParticleSystem>();
            if (ps == null) ps = instance.AddComponent<ParticleSystem>();

            ConfigureParticleSystemSettings(ps, smokeMat);

            PrefabUtility.SaveAsPrefabAsset(instance, MOVE_DUST_PREFAB_PATH);
            Debug.Log($"[PlayerDustSetup] Updated {MOVE_DUST_PREFAB_PATH} successfully.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    public static void ConfigurePlayerPrefab(string prefabPath, Material smokeMat)
    {
        GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (playerAsset == null)
        {
            Debug.LogWarning($"[PlayerDustSetup] Player prefab not found at {prefabPath}");
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // Find Legs, LeftLeg, RightLeg
            Transform legsTransform = instance.transform.Find("Body/Legs") ?? instance.transform.Find("Legs");
            Transform leftLeg = instance.transform.Find("Body/Legs/LeftLeg") ?? instance.transform.Find("Legs/LeftLeg");
            Transform rightLeg = instance.transform.Find("Body/Legs/RightLeg") ?? instance.transform.Find("Legs/RightLeg");

            if (legsTransform == null || leftLeg == null || rightLeg == null)
            {
                Transform[] all = instance.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (legsTransform == null && t.name == "Legs") legsTransform = t;
                    if (leftLeg == null && t.name == "LeftLeg") leftLeg = t;
                    if (rightLeg == null && t.name == "RightLeg") rightLeg = t;
                }
            }

            Transform dustParent = legsTransform != null ? legsTransform : instance.transform;
            Vector3 dustLocalPos = new Vector3(0f, -0.45f, 0f);

            Transform moveDustChild = dustParent.Find("MoveDust");
            if (moveDustChild == null)
            {
                Transform existing = instance.transform.Find("MoveDust");
                if (existing != null)
                {
                    existing.SetParent(dustParent);
                    moveDustChild = existing;
                }
            }

            GameObject dustGO;
            if (moveDustChild == null)
            {
                dustGO = new GameObject("MoveDust");
                dustGO.transform.SetParent(dustParent);
            }
            else
            {
                dustGO = moveDustChild.gameObject;
            }

            dustGO.transform.localPosition = dustLocalPos;
            dustGO.transform.localRotation = Quaternion.identity;
            dustGO.transform.localScale = Vector3.one;

            ParticleSystem ps = dustGO.GetComponent<ParticleSystem>();
            if (ps == null) ps = dustGO.AddComponent<ParticleSystem>();

            ConfigureParticleSystemSettings(ps, smokeMat);

            // Setup PlayerMovementDust on root
            PlayerMovementDust movementDust = instance.GetComponent<PlayerMovementDust>();
            if (movementDust == null) movementDust = instance.AddComponent<PlayerMovementDust>();

            SerializedObject serializedDust = new SerializedObject(movementDust);
            serializedDust.Update();
            serializedDust.FindProperty("dustParticles").objectReferenceValue = ps;
            serializedDust.FindProperty("legsTransform").objectReferenceValue = legsTransform;
            serializedDust.FindProperty("leftLegTransform").objectReferenceValue = leftLeg;
            serializedDust.FindProperty("rightLegTransform").objectReferenceValue = rightLeg;
            serializedDust.FindProperty("stepDistance").floatValue = 0.09f;
            serializedDust.FindProperty("kickbackForce").floatValue = 0.10f;
            serializedDust.FindProperty("upwardDrift").floatValue = 0.02f;
            serializedDust.FindProperty("footVerticalOffset").floatValue = -0.42f;
            serializedDust.FindProperty("minPuffSize").floatValue = 0.26f;
            serializedDust.FindProperty("maxPuffSize").floatValue = 0.36f;
            serializedDust.FindProperty("puffLifetime").floatValue = 0.40f;
            serializedDust.FindProperty("dustColor").colorValue = new Color(0.92f, 0.94f, 0.96f, 0.50f);
            serializedDust.FindProperty("sortingLayerName").stringValue = "player";
            serializedDust.FindProperty("sortingOrder").intValue = -2;
            serializedDust.ApplyModifiedProperties();

            // Setup PlayerController reference
            PlayerController pc = instance.GetComponent<PlayerController>();
            if (pc != null)
            {
                SerializedObject serializedPC = new SerializedObject(pc);
                serializedPC.Update();
                var moveDustProp = serializedPC.FindProperty("movementDust");
                if (moveDustProp != null)
                {
                    moveDustProp.objectReferenceValue = movementDust;
                }
                serializedPC.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log($"[PlayerDustSetup] Updated player prefab at {prefabPath} successfully.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    public static void ConfigureParticleSystemSettings(ParticleSystem ps, Material smokeMat)
    {
        // 1. Main Module
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpeed = 1.0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.46f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.36f);
        main.startColor = new Color(0.92f, 0.94f, 0.96f, 0.50f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 100;

        // 2. Emission: Controlled directly per step
        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        // 3. Shape: Disabled (positions given per foot)
        var shape = ps.shape;
        shape.enabled = false;

        // 4. Color Over Lifetime Module: Soft translucent dissolve
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(0.95f, 0.95f, 0.95f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.55f, 0.0f),
                new GradientAlphaKey(0.45f, 0.45f),
                new GradientAlphaKey(0.20f, 0.75f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;

        // 5. Size Over Lifetime Module: Overlapping cloud expansion
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.65f);
        sizeCurve.AddKey(0.25f, 1.0f);
        sizeCurve.AddKey(0.65f, 0.85f);
        sizeCurve.AddKey(1.0f, 0.20f);
        sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // 6. Rotation Over Lifetime Module
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        // 7. Renderer: Sorting & Material
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "player";
            renderer.sortingOrder = -2;
            if (smokeMat != null)
            {
                renderer.sharedMaterial = smokeMat;
            }
        }
    }
}
#endif
