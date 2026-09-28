using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns atmospheric, soft floating dust motes illuminated inside 2D spotlights and ambient light shafts.
/// Gives volumetric depth and realistic indoor atmosphere to lighting cones across the scene.
/// </summary>
[ExecuteAlways]
public class LightShaftDustMotes : MonoBehaviour
{
    [Header("Mote Settings")]
    [Tooltip("Radius of the illuminated area where motes drift.")]
    [SerializeField] private float lightRadius = 3.5f;

    [Tooltip("Illuminated dust tint color (auto-matched to Light2D if present).")]
    [SerializeField] private Color moteColor = new Color(1f, 0.95f, 0.8f, 0.22f);

    [SerializeField] private float moteMinSize = 0.04f;
    [SerializeField] private float moteMaxSize = 0.08f;
    [SerializeField] private int maxMotes = 30;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "player";
    [SerializeField] private int sortingOrder = 15;

    [Header("Internal")]
    [SerializeField] private ParticleSystem dustSystem;

    private static Material sharedMoteMaterial;

    private void Awake()
    {
        TryAutoDetectLightSettings();
        EnsureParticleSystem();
    }

    private void Start()
    {
        TryAutoDetectLightSettings();
        EnsureParticleSystem();
    }

    public void TryAutoDetectLightSettings()
    {
#if UNITY_EDITOR
        var light2D = GetComponent("Light2D") as Component;
        if (light2D != null)
        {
            var serializedLight = new UnityEditor.SerializedObject(light2D);
            var colorProp = serializedLight.FindProperty("m_Color");
            var radiusProp = serializedLight.FindProperty("m_PointLightOuterRadius");

            if (colorProp != null)
            {
                Color lColor = colorProp.colorValue;
                moteColor = new Color(lColor.r, lColor.g, lColor.b, 0.22f);
            }

            if (radiusProp != null && radiusProp.floatValue > 0.5f)
            {
                lightRadius = radiusProp.floatValue * 0.85f;
            }
        }
#endif
    }

    public void EnsureParticleSystem()
    {
        if (dustSystem == null)
        {
            Transform existing = transform.Find("LightDustMotes");
            if (existing != null) dustSystem = existing.GetComponent<ParticleSystem>();
        }

        if (dustSystem == null)
        {
            GameObject go = new GameObject("LightDustMotes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            dustSystem = go.AddComponent<ParticleSystem>();
        }

        ConfigureParticleSystem(dustSystem);
    }

    private void ConfigureParticleSystem(ParticleSystem ps)
    {
        if (ps == null) return;

        // 1. Main Module
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(moteMinSize, moteMaxSize);
        main.startColor = moteColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = maxMotes;

        // 2. Emission Module
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = maxMotes / 4.0f;

        // 3. Shape Module: Circle matching light radius
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = lightRadius;
        shape.arc = 360f;

        // 4. Color Over Lifetime: Smooth breathing fade-in and fade-out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(Color.white, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.85f, 0.30f),
                new GradientAlphaKey(0.85f, 0.70f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        col.color = grad;

        // 5. Velocity Over Lifetime: Gentle air swirl
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.07f);

        // 6. Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            Material mat = GetOrCreateMoteMaterial();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        if (!ps.isPlaying)
        {
            ps.Play();
        }
    }

    private static Material GetOrCreateMoteMaterial()
    {
        if (sharedMoteMaterial != null) return sharedMoteMaterial;

        // Sprites/Default provides true unlit alpha blending with no black quads in URP 2D
        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null) return null;

        sharedMoteMaterial = new Material(shader);
        sharedMoteMaterial.name = "LightDustMotesMaterial";
        sharedMoteMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Sprite circle = ProceduralEffectsGenerator.GetSoftCircleSprite();
        if (circle != null && circle.texture != null)
        {
            sharedMoteMaterial.mainTexture = circle.texture;
            if (sharedMoteMaterial.HasProperty("_MainTex"))
                sharedMoteMaterial.SetTexture("_MainTex", circle.texture);
            if (sharedMoteMaterial.HasProperty("_BaseMap"))
                sharedMoteMaterial.SetTexture("_BaseMap", circle.texture);
        }

        return sharedMoteMaterial;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Counter Boom/Setup All Scene Light Dust Motes", false, 30)]
    public static void SetupAllSceneLights()
    {
        var allObjects = GameObject.FindObjectsOfType<GameObject>();
        int count = 0;

        foreach (var go in allObjects)
        {
            var lightComponent = go.GetComponent("Light2D");
            if (lightComponent != null)
            {
                var motes = go.GetComponent<LightShaftDustMotes>();
                if (motes == null) motes = go.AddComponent<LightShaftDustMotes>();
                motes.TryAutoDetectLightSettings();
                motes.EnsureParticleSystem();
                EditorUtility.SetDirty(go);
                count++;
            }
        }

        Debug.Log($"[LightShaftDustMotes] Successfully added atmospheric dust motes to {count} 2D lights in active scene!");
    }
#endif
}
