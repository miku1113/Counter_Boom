using UnityEngine;

public class ExplosiveGrenade : Grenade
{
    [Header("Explosive Stats")]
    public int damage = 50;

    protected override void Explode()
    {
        base.Explode();

        // Area damage — apply to anything with a PlayerHealth component in radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D col in hitColliders)
        {
            PlayerHealth health = col.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"[ExplosiveGrenade] Dealt {damage} damage to {col.name}.");
            }
        }
    }
}
