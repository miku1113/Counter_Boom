using UnityEngine;

public class ExplosiveGrenade : Grenade
{
    protected override void Awake()
    {
        base.Awake();
        damage = 50; // Explosive grenade deals 50 lethal area damage
    }

    protected override void Explode()
    {
        damage = 50; // Ensure damage is set

        // 1. Create the awesome red explosion blast effect
        ProceduralEffectsGenerator.CreateRedExplosionBlast(transform.position, explosionRadius);

        // 2. Clear Inspector prefab reference so base.Explode() does NOT spawn the old fallback prefab
        explosionEffect = null;

        // 3. Trigger screen shake
        if (CameraController.Instance != null)
        {
            CameraController.Instance.TriggerShake(0.55f, 0.45f);
        }

        base.Explode();
    }
}
