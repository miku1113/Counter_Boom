using UnityEngine;

/// <summary>
/// Controls high-fidelity cartoon footstep cloud puffs trailing near the player's legs.
/// Emits overlapping cartoon spiral cloud puffs (MoveDustCloud) using a single uniform color
/// and an Unlit 2D shader so particles render crisp and clear under all light conditions.
/// </summary>
public class PlayerMovementDust : MonoBehaviour
{
    [Header("Leg References")]
    [Tooltip("Left leg transform used to anchor left footstep puffs.")]
    [SerializeField] private Transform leftLegTransform;

    [Tooltip("Right leg transform used to anchor right footstep puffs.")]
    [SerializeField] private Transform rightLegTransform;

    [Tooltip("General legs root transform fallback.")]
    [SerializeField] private Transform legsTransform;

    [Header("Particle System")]
    [SerializeField] private ParticleSystem dustParticles;

    [Header("Footstep Cadence & Dynamics")]
    [Tooltip("Tight distance step threshold between consecutive puffs to ensure overlapping continuous cloud trail. Recommended: 0.08 - 0.11.")]
    [SerializeField] private float stepDistance = 0.09f;

    [Tooltip("Force pushing dust puffs backward opposite to movement direction. Recommended: 0.05 - 0.15.")]
    [SerializeField] private float kickbackForce = 0.10f;

    [Tooltip("Slight upward float velocity as dust dissipates. Recommended: 0.01 - 0.03.")]
    [SerializeField] private float upwardDrift = 0.02f;

    [Tooltip("Random lateral spread jitter for natural organic dispersion.")]
    [SerializeField] private float spreadJitter = 0.03f;

    [Tooltip("Vertical offset from leg pivot down to ground surface under foot sole.")]
    [SerializeField] private float footVerticalOffset = -0.42f;

    [Header("Smoke / Dust Sprite Settings")]
    [Tooltip("Drag & drop your custom smoke/cloud sprite here to directly set it from the Unity Inspector.")]
    [SerializeField] private Sprite customSmokeSprite;

    [Tooltip("Drag & drop a custom texture directly here (optional alternative to Sprite).")]
    [SerializeField] private Texture2D customSmokeTexture;

    [Header("Visual Appearance")]
    [Tooltip("Single uniform dust cloud color applied across all floors.")]
    [SerializeField] private Color dustColor = new Color(0.92f, 0.94f, 0.96f, 0.50f);

    [SerializeField] private float minPuffSize = 0.26f;
    [SerializeField] private float maxPuffSize = 0.36f;
    [SerializeField] private float puffLifetime = 0.40f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "player";
    [SerializeField] private int sortingOrder = -2;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private PlayerController playerController;
    private Rigidbody2D rb;
    private Animator animator;
    private PlayerHealth playerHealth;

    private Vector3 lastWorldPos;
    private Vector3 lastStepPos;
    private Vector3 moveDirection = Vector3.zero;
    private int stepCount = 0;
    private bool isWalking = false;

    private static Texture2D loadedCloudTexture;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<PlayerHealth>();
        animator = GetComponentInChildren<Animator>();

        FindLegReferences();
        EnsureParticleSystem();

        lastWorldPos = transform.position;
        lastStepPos = transform.position;
    }

    private void Start()
    {
        FindLegReferences();
        EnsureParticleSystem();

        lastWorldPos = transform.position;
        lastStepPos = transform.position;
    }

    public void FindLegReferences()
    {
        if (legsTransform == null)
        {
            legsTransform = transform.Find("Body/Legs") ?? transform.Find("Legs");
        }

        if (leftLegTransform == null)
        {
            leftLegTransform = transform.Find("Body/Legs/LeftLeg") ?? transform.Find("Legs/LeftLeg");
        }

        if (rightLegTransform == null)
        {
            rightLegTransform = transform.Find("Body/Legs/RightLeg") ?? transform.Find("Legs/RightLeg");
        }

        if (leftLegTransform == null || rightLegTransform == null)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (leftLegTransform == null && t.name == "LeftLeg") leftLegTransform = t;
                if (rightLegTransform == null && t.name == "RightLeg") rightLegTransform = t;
                if (legsTransform == null && t.name == "Legs") legsTransform = t;
            }
        }
    }

    /// <summary>
    /// Configures the ParticleSystem with world space simulation, overlapping expansion curves,
    /// Unlit 2D shader, and custom cartoon cloud sprite texture.
    /// </summary>
    public void EnsureParticleSystem()
    {
        if (dustParticles == null)
        {
            Transform dustT = transform.Find("Body/Legs/MoveDust") ?? transform.Find("Legs/MoveDust") ?? transform.Find("MoveDust");
            if (dustT != null) dustParticles = dustT.GetComponent<ParticleSystem>();
        }

        if (dustParticles == null)
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                if (ps.name.ToLower().Contains("dust"))
                {
                    dustParticles = ps;
                    break;
                }
            }
        }

        if (dustParticles == null)
        {
            Transform parent = legsTransform != null ? legsTransform : transform;
            GameObject go = new GameObject("MoveDust");
            go.transform.SetParent(parent);
            go.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            dustParticles = go.AddComponent<ParticleSystem>();
        }

        if (dustParticles != null)
        {
            ApplyParticleSettings();
        }
    }

    public void ApplyParticleSettings()
    {
        if (dustParticles == null) return;

        // 1. Main Module: World simulation space, short lifetime, local scaling
        var main = dustParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;
        main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(puffLifetime * 0.85f, puffLifetime * 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startSize = new ParticleSystem.MinMaxCurve(minPuffSize, maxPuffSize);
        main.startColor = dustColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        main.maxParticles = 100;

        // 2. Emission: Controlled directly per step via Emit()
        var emission = dustParticles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

        // 3. Shape: Disabled (positions supplied per foot)
        var shape = dustParticles.shape;
        shape.enabled = false;

        // 4. Color Over Lifetime: Smooth soft translucent dissolve
        var col = dustParticles.colorOverLifetime;
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

        // 5. Size Over Lifetime: Expands quickly to overlap adjacent puffs then dissolves smoothly
        var sol = dustParticles.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.65f);     // starts compact at foot strike
        sizeCurve.AddKey(0.25f, 1.0f);   // expands quickly to bridge gaps between steps!
        sizeCurve.AddKey(0.65f, 0.85f);  // stays fluffy
        sizeCurve.AddKey(1.0f, 0.20f);   // dissolves down
        sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // 6. Rotation Over Lifetime: Gentle swirl
        var rol = dustParticles.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        // 7. Renderer: Unlit Shader & Cartoon Cloud Sprite
        var renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            Material mat = GetOrCreateUnlitCloudMaterial();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        if (!dustParticles.isPlaying)
        {
            dustParticles.Play();
        }
    }

    public Texture2D GetCloudPuffTexture()
    {
        if (customSmokeSprite != null && customSmokeSprite.texture != null)
        {
            return customSmokeSprite.texture;
        }

        if (customSmokeTexture != null)
        {
            return customSmokeTexture;
        }

        if (loadedCloudTexture != null) return loadedCloudTexture;

        loadedCloudTexture = Resources.Load<Texture2D>("MoveDustCloud");

#if UNITY_EDITOR
        if (loadedCloudTexture == null)
        {
            loadedCloudTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/MoveDustCloud.png");
        }
        if (loadedCloudTexture == null)
        {
            loadedCloudTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefab/Effects/MoveDustCloud.png");
        }
#endif
        if (loadedCloudTexture == null)
        {
            var circle = ProceduralEffectsGenerator.GetSoftCircleSprite();
            if (circle != null) loadedCloudTexture = circle.texture;
        }

        return loadedCloudTexture;
    }

    /// <summary>
    /// Uses Sprites/Default unlit shader so dust particles blend seamlessly without any black box or quad artifacts.
    /// Explicitly sets clamp wrap mode and pure white base color to eliminate black border artifacts under 2D lights.
    /// </summary>
    private Material GetOrCreateUnlitCloudMaterial()
    {
        // Sprites/Default guarantees SrcAlpha OneMinusSrcAlpha blending and ZWrite Off in URP 2D
        Shader shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Mobile/Particles/Alpha Blended")
                     ?? Shader.Find("UI/Default");

        if (shader == null) return null;

        Material runtimeMat = new Material(shader);
        runtimeMat.name = "RuntimeUnlitCloudMat";
        runtimeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Texture2D cloudTex = GetCloudPuffTexture();
        if (cloudTex != null)
        {
            cloudTex.wrapMode = TextureWrapMode.Clamp;
            runtimeMat.mainTexture = cloudTex;
            if (runtimeMat.HasProperty("_MainTex")) runtimeMat.SetTexture("_MainTex", cloudTex);
            if (runtimeMat.HasProperty("_BaseMap")) runtimeMat.SetTexture("_BaseMap", cloudTex);
        }

        if (runtimeMat.HasProperty("_Color")) runtimeMat.SetColor("_Color", Color.white);
        if (runtimeMat.HasProperty("_BaseColor")) runtimeMat.SetColor("_BaseColor", Color.white);

        return runtimeMat;
    }

    private void Update()
    {
        CheckMovementAndTriggerSteps();
        lastWorldPos = transform.position;
    }

    private void CheckMovementAndTriggerSteps()
    {
        if (playerController != null && playerController.IsGhost)
        {
            isWalking = false;
            return;
        }
        if (playerHealth != null && playerHealth.IsDead)
        {
            isWalking = false;
            return;
        }

        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - lastWorldPos;
        float frameDist = delta.magnitude;

        if (frameDist > 0.001f && frameDist < 3.0f)
        {
            moveDirection = delta / frameDist;
        }

        bool hasMoveInput = playerController != null && playerController.IsMoving();
        bool hasVelocity = rb != null && rb.simulated && rb.velocity.sqrMagnitude > 0.004f;
        bool hasDisplacement = frameDist > 0.002f && frameDist < 3.0f;

        bool isAnimWalking = false;
        if (animator != null && animator.enabled)
        {
            try { isAnimWalking = animator.GetBool("isWalking"); } catch { }
            if (!isAnimWalking)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                isAnimWalking = stateInfo.IsName("walk") || stateInfo.IsName("Walk") ||
                                stateInfo.IsName("walking") || stateInfo.IsName("Walking") ||
                                stateInfo.IsName("run") || stateInfo.IsName("Run") ||
                                stateInfo.IsName("Player_Walk");
            }
        }

        isWalking = hasMoveInput || hasVelocity || (hasDisplacement && (isAnimWalking || frameDist > 0.012f));

        if (isWalking)
        {
            float distFromLastStep = Vector3.Distance(currentPos, lastStepPos);
            if (distFromLastStep >= stepDistance)
            {
                lastStepPos = currentPos;
                EmitAlternatingFootstepPuff();
            }
        }
        else
        {
            if (Vector3.Distance(currentPos, lastStepPos) > stepDistance * 0.8f)
            {
                lastStepPos = currentPos - moveDirection * (stepDistance * 0.5f);
            }
        }
    }

    /// <summary>
    /// Emits overlapping cartoon cloud puffs directly at foot level on the ground using a single uniform color.
    /// </summary>
    public void EmitAlternatingFootstepPuff()
    {
        if (dustParticles == null) return;

        bool isLeft = (stepCount++ % 2 == 0);
        Transform steppingFoot = isLeft ? leftLegTransform : rightLegTransform;

        Vector3 spawnPos;
        if (steppingFoot != null)
        {
            spawnPos = steppingFoot.position + new Vector3(0f, footVerticalOffset, 0f);
        }
        else
        {
            float lateralSign = isLeft ? -1f : 1f;
            Vector3 lateralOffset = Vector3.Cross(moveDirection != Vector3.zero ? moveDirection : Vector3.up, Vector3.forward) * (lateralSign * 0.10f);
            spawnPos = transform.position + new Vector3(0f, -0.75f, 0f) + lateralOffset;
        }

        Vector3 kickDir = moveDirection != Vector3.zero ? -moveDirection : Vector3.down;
        Vector3 kickVel = kickDir * kickbackForce 
                        + Vector3.up * upwardDrift 
                        + new Vector3(Random.Range(-spreadJitter, spreadJitter), Random.Range(-spreadJitter * 0.5f, spreadJitter * 0.5f), 0f);

        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = spawnPos;
        emitParams.velocity = kickVel;
        emitParams.startSize = Random.Range(minPuffSize, maxPuffSize);
        emitParams.startColor = dustColor;
        emitParams.startLifetime = Random.Range(puffLifetime * 0.85f, puffLifetime * 1.15f);
        emitParams.rotation = Random.Range(-25f, 25f);

        dustParticles.Emit(emitParams, 1);

        if (debugLogs)
        {
            Debug.Log($"[PlayerMovementDust] {(isLeft ? "LEFT" : "RIGHT")} cloud puff emitted at {spawnPos}");
        }
    }

    public void TriggerFootstepPuff(int count = 1)
    {
        if (playerController != null && playerController.IsGhost) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        EmitAlternatingFootstepPuff();
    }

    public void SetEmitting(bool emit)
    {
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (dustParticles != null)
        {
            ApplyParticleSettings();
        }
    }

    [ContextMenu("Auto Setup Dust Particles")]
    public void EditorSetup()
    {
        FindLegReferences();
        EnsureParticleSystem();
        ApplyParticleSettings();
        UnityEditor.EditorUtility.SetDirty(this);
        if (dustParticles != null) UnityEditor.EditorUtility.SetDirty(dustParticles.gameObject);
        Debug.Log($"[PlayerMovementDust] Setup completed on {gameObject.name}. DustParticles: {dustParticles != null}");
    }
#endif
}
