using UnityEngine;

/// <summary>
/// Creates an atmospheric Air Breeze & Wind Particle Effect.
/// Generates soft air wisps, wind particles, and ambient dust motes
/// flowing smoothly from LEFT to RIGHT across the screen.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(ParticleSystemRenderer))]
public class FireEmberParticleSystem : MonoBehaviour
{
    [Header("Air Breeze Settings")]
    [Tooltip("Number of air particles spawned per second.")]
    [SerializeField] private float emissionRate = 35f;

    [Tooltip("Horizontal width of spawn box area.")]
    [SerializeField] private float spawnWidth = 20f;

    [Tooltip("Vertical height of spawn box area.")]
    [SerializeField] private float spawnHeight = 9f;

    [Header("Left-to-Right Flow Speed")]
    [Tooltip("Minimum horizontal flow speed (Left to Right).")]
    [SerializeField] private float minFlowSpeedX = 4.0f;

    [Tooltip("Maximum horizontal flow speed (Left to Right).")]
    [SerializeField] private float maxFlowSpeedX = 8.5f;

    [Tooltip("Vertical drift/wave speed.")]
    [SerializeField] private float verticalDriftSpeed = 0.6f;

    [Header("Turbulence & Air Swirl")]
    [SerializeField] private float breezeTurbulence = 0.3f;

    [Header("Particle Lifespan & Size")]
    [SerializeField] private float minLifetime = 3.0f;
    [SerializeField] private float maxLifetime = 6.0f;
    [SerializeField] private float minSize = 0.08f;
    [SerializeField] private float maxSize = 0.35f;

    [Header("Air Color & Opacity")]
    [SerializeField] private Color airStartColor = new Color(0.9f, 0.96f, 1.0f, 0.45f); // Bright subtle sky white
    [SerializeField] private Color airMidColor   = new Color(0.75f, 0.90f, 1.0f, 0.30f); // Soft atmospheric blue-white
    [SerializeField] private Color airEndColor   = new Color(1.0f, 1.0f, 1.0f, 0.0f);   // Fade out translucent

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 50;

    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    private static Material sharedAirMaterial;

    private void Awake()
    {
        ConfigureParticleSystem();
    }

    private void Start()
    {
        ConfigureParticleSystem();
        if (Application.isPlaying && ps != null && !ps.isPlaying)
        {
            ps.Play();
        }
    }

    private void OnValidate()
    {
        ConfigureParticleSystem();
    }

    [ContextMenu("Rebuild Air Breeze Particle System")]
    public void ConfigureParticleSystem()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        if (psRenderer == null) psRenderer = GetComponent<ParticleSystemRenderer>();
        if (ps == null || psRenderer == null) return;

        // Force set clean non-pink Sprites/Default air material
        EnsureAirMaterial();

        // 1. Main Module
        var main = ps.main;
        main.duration = 5.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f); // Zero gravity
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1000;

        // Pre-warm so air particles cover screen immediately when entering scene
        main.prewarm = true;

        // 2. Emission Module
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // Occasional gust burst of air wisps
        ParticleSystem.Burst gustBurst = new ParticleSystem.Burst(0f, 4, 10, 3, 2.5f);
        emission.SetBursts(new ParticleSystem.Burst[] { gustBurst });

        // 3. Shape Module (Wide Box spawn on the left & middle area)
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(spawnWidth, spawnHeight, 1f);

        // 4. Color over Lifetime (Fades in at start, floats, fades out smoothly)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(airStartColor, 0.0f),
                new GradientColorKey(airMidColor, 0.5f),
                new GradientColorKey(airEndColor, 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.40f, 0.15f),
                new GradientAlphaKey(0.30f, 0.70f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        // 5. Size over Lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0.0f, 0.4f);
        sizeCurve.AddKey(0.3f, 1.0f);
        sizeCurve.AddKey(0.8f, 0.8f);
        sizeCurve.AddKey(1.0f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // 6. Velocity over Lifetime (FLOW LEFT TO RIGHT: Positive X Velocity!)
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;

        // Positive X = Left to Right Movement!
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(minFlowSpeedX, maxFlowSpeedX);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-verticalDriftSpeed, verticalDriftSpeed);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

        // 7. Noise Module (Gentle air wave & breeze turbulence)
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(breezeTurbulence);
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.High;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(breezeTurbulence * 0.5f);
        noise.strengthY = new ParticleSystem.MinMaxCurve(breezeTurbulence * 1.5f); // Natural air wave lift
        noise.strengthZ = new ParticleSystem.MinMaxCurve(0f);

        // 8. Rotation over Lifetime
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        // 9. Renderer Settings (Stretched / Billboard Air Wisps)
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingLayerName = sortingLayerName;
        psRenderer.sortingOrder = sortingOrder;
    }

    private void EnsureAirMaterial()
    {
        // Always recreate if material is null or using Unity's default pink material
        if (psRenderer.sharedMaterial == null || 
            psRenderer.sharedMaterial.name.Contains("Default-Material") || 
            psRenderer.sharedMaterial.name.Contains("Default-Particle") ||
            sharedAirMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null) spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (spriteShader == null) spriteShader = Shader.Find("UI/Default");
            if (spriteShader == null) spriteShader = Shader.Find("Particles/Standard Unlit");

            if (spriteShader != null)
            {
                sharedAirMaterial = new Material(spriteShader);
                sharedAirMaterial.name = "AirBreeze_Material";
                sharedAirMaterial.mainTexture = CreateAirWispTexture();
            }
        }

        if (sharedAirMaterial != null)
        {
            psRenderer.sharedMaterial = sharedAirMaterial;
        }
    }

    /// <summary>
    /// Creates a smooth, translucent air wisp / dust mote texture for clean breeze visuals.
    /// </summary>
    private static Texture2D CreateAirWispTexture()
    {
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.name = "AirWisp_Tex";
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float maxRadius = res * 0.5f;

        Color[] pixels = new Color[res * res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                if (dist >= 1.0f)
                {
                    pixels[y * res + x] = Color.clear;
                }
                else
                {
                    // Soft translucent falloff for air wisps
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, dist);
                    pixels[y * res + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Static helper method to spawn an Air Breeze particle effect in any scene.
    /// </summary>
    public static GameObject CreateEmberEffect(Vector3 position, Transform parent = null)
    {
        GameObject go = new GameObject("AirBreezeParticles");
        go.transform.position = position;
        if (parent != null) go.transform.SetParent(parent, true);

        FireEmberParticleSystem sys = go.AddComponent<FireEmberParticleSystem>();
        sys.ConfigureParticleSystem();

        ParticleSystem particleSys = go.GetComponent<ParticleSystem>();
        if (particleSys != null && !particleSys.isPlaying)
        {
            particleSys.Play();
        }

        return go;
    }
}
