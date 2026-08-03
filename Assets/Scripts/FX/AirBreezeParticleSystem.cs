using UnityEngine;

/// <summary>
/// Helper component for configuring atmospheric Air Breeze / Wind Particle Systems.
/// Allows material assignment and left-to-right flow settings directly in the Unity Inspector.
/// </summary>
public class AirBreezeParticleSystem : MonoBehaviour
{
    [Header("Particle System Reference")]
    [SerializeField] private ParticleSystem targetParticleSystem;

    [Header("Material & Visuals")]
    [Tooltip("Assign your custom Air / Breeze Particle Material here in the Inspector.")]
    [SerializeField] private Material particleMaterial;

    [Header("Air Flow & Direction")]
    [Tooltip("Velocity vector for air breeze (e.g. X = 5 for Left to Right flow).")]
    [SerializeField] private Vector3 flowDirection = new Vector3(5.0f, 0f, 0f);

    [SerializeField] private float emissionRate = 30f;
    [SerializeField] private float startSize = 0.2f;

    private void Awake()
    {
        if (targetParticleSystem == null)
        {
            targetParticleSystem = GetComponent<ParticleSystem>();
        }
        ApplyInspectorSettings();
    }

    private void Start()
    {
        if (targetParticleSystem != null && !targetParticleSystem.isPlaying)
        {
            targetParticleSystem.Play();
        }
    }

    [ContextMenu("Apply Inspector Settings")]
    public void ApplyInspectorSettings()
    {
        if (targetParticleSystem == null)
            targetParticleSystem = GetComponent<ParticleSystem>();

        if (targetParticleSystem == null) return;

        // Apply Material if assigned by user in Inspector
        if (particleMaterial != null)
        {
            ParticleSystemRenderer psRenderer = targetParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sharedMaterial = particleMaterial;
            }
        }

        // Apply Velocity Flow Direction if Velocity Over Lifetime is enabled
        var velocity = targetParticleSystem.velocityOverLifetime;
        if (velocity.enabled)
        {
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(flowDirection.x * 0.7f, flowDirection.x * 1.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(flowDirection.y - 0.5f, flowDirection.y + 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(flowDirection.z - 0.1f, flowDirection.z + 0.1f);
        }

        var emission = targetParticleSystem.emission;
        if (emission.enabled)
        {
            emission.rateOverTime = emissionRate;
        }
    }
}
