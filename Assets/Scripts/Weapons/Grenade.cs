using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class Grenade : MonoBehaviour
{
    [Header("Settings")]
    public float fuseTime        = 1.2f;
    public float explosionRadius = 5f;
    public float throwForce      = 12f;
    public int   damage          = 50; // Default 50 damage for normal blast grenade

    [Header("Effects")]
    public GameObject explosionEffect;

    [Header("Physics")]
    [SerializeField] protected Rigidbody2D rb;

    protected virtual void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.drag = 2.5f; // Smooth glide & stop
            rb.angularDrag = 1.0f;
        }
    }

    public void Throw(Vector2 direction, Collider2D throwerCollider = null)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.drag = 2.5f;
            rb.velocity = direction.normalized * throwForce;
            rb.AddTorque(15f, ForceMode2D.Impulse);

            Debug.Log($"[Grenade] Thrown with velocity: {rb.velocity} (Force: {throwForce}, Dir: {direction})");

            // Ignore collision with the actual thrower (including all child colliders) to prevent instant stop
            if (throwerCollider != null)
            {
                Collider2D grenadeCol = GetComponent<Collider2D>();
                if (grenadeCol != null)
                {
                    Collider2D[] throwerColliders = throwerCollider.transform.root.GetComponentsInChildren<Collider2D>();
                    foreach (Collider2D col in throwerColliders)
                    {
                        Physics2D.IgnoreCollision(col, grenadeCol, true);
                    }
                }
            }
            else
            {
                // Fallback Tag Lookup
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Collider2D grenadeCol = GetComponent<Collider2D>();
                    if (grenadeCol != null)
                    {
                        Collider2D[] playerColliders = player.transform.root.GetComponentsInChildren<Collider2D>();
                        foreach (Collider2D col in playerColliders)
                        {
                            Physics2D.IgnoreCollision(col, grenadeCol, true);
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"[Grenade] Rigidbody2D is missing on '{gameObject.name}'!");
        }

        StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    protected virtual void Explode()
    {
        Vector3 blastPos = transform.position;

        // 1. Spawns RED explosion blast effect ONLY for normal blast grenade (damage > 0)
        if (damage > 0)
        {
            ProceduralEffectsGenerator.CreateRedExplosionBlast(blastPos, explosionRadius);

            if (CameraController.Instance != null)
            {
                CameraController.Instance.TriggerShake(0.55f, 0.45f);
            }
        }
        else if (explosionEffect != null)
        {
            Instantiate(explosionEffect, blastPos, Quaternion.identity);
        }

        // 3. Apply area damage (only if grenade deals damage)
        if (damage > 0)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(blastPos, explosionRadius);
            foreach (Collider2D col in hitColliders)
            {
                PlayerHealth health = col.GetComponent<PlayerHealth>() ?? col.GetComponentInParent<PlayerHealth>();
                if (health != null && !health.IsDead)
                {
                    health.TakeDamage(damage);
                    Debug.Log($"[Grenade] Dealt {damage} explosion damage to {col.name}.");
                }
            }
        }

        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
