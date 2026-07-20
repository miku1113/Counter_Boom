using UnityEngine;

public class StunGrenade : Grenade
{
    [Header("Stun Settings")]
    public float speedMultiplier = 0.2f; // Slows movement to 20%
    public float stunDuration    = 4f;   // Lasts 4 seconds

    protected override void Explode()
    {
        // If no visual explosionEffect prefab is assigned, create the procedural stun blast
        if (explosionEffect == null)
        {
            ProceduralEffectsGenerator.CreateStunBlast(transform.position, explosionRadius);
        }

        base.Explode();

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D col in hitColliders)
        {
            PlayerController controller = col.GetComponent<PlayerController>();
            if (controller != null)
            {
                // Only stun the local player on their screen (avoid multiple stun calls for other clients)
                if (controller.IsLocal)
                {
                    controller.ApplySpeedBoost(speedMultiplier, stunDuration);
                    PlayerController.TriggerLocalPlayerStun(stunDuration);
                    Debug.Log($"[StunGrenade] Stunned local player '{col.name}' (speed set to {speedMultiplier}x for {stunDuration}s).");
                }
            }
        }
    }


}
