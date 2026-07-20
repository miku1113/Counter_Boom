using UnityEngine;

public class SmokeGrenade : Grenade
{
    [Header("Smoke Settings")]
    public GameObject smokeCloudPrefab;
    public float      smokeLifetime = 6f; // Lingers for 6 seconds

    protected override void Explode()
    {
        base.Explode();

        GameObject smoke = null;

        if (smokeCloudPrefab != null)
        {
            // Spawn volumetric smoke screen using multiple instances of the assigned custom prefab
            smoke = ProceduralEffectsGenerator.CreateSmokeCloud(transform.position, explosionRadius, smokeLifetime, smokeCloudPrefab);
            Debug.Log($"[SmokeGrenade] Spawned volumetric prefab smoke screen (multiple puffs) at {transform.position}.");
        }
        else
        {
            // If prefab is null, generate the volumetric smoke cloud programmatically from code
            smoke = ProceduralEffectsGenerator.CreateSmokeCloud(transform.position, explosionRadius, smokeLifetime);
            Debug.Log($"[SmokeGrenade] Spawned procedural volumetric smoke screen at {transform.position}.");
        }



        if (smoke != null)
        {
            // Ensure the smoke has a 2D trigger collider to register player entries
            CircleCollider2D col = smoke.GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = smoke.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = explosionRadius * 0.8f; // Smoke area is slightly smaller than full blast radius
            }
            else
            {
                col.isTrigger = true;
            }

            // Dynamically attach the SmokeEffectZone component
            if (smoke.GetComponent<SmokeEffectZone>() == null)
            {
                smoke.AddComponent<SmokeEffectZone>();
            }

            // Procedural smoke parent manages its own lifetime
            Destroy(smoke, smokeLifetime);
        }
    }


}
