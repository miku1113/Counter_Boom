using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    [Header("Settings")]
    public float fuseTime        = 3f;
    public float explosionRadius = 5f;
    public int   damage          = 50;
    public float throwForce      = 10f;

    [Header("Effects")]
    public GameObject explosionEffect;

    [Header("Physics")]
    [SerializeField] private Rigidbody2D rb;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    public void Throw(Vector2 direction)
    {
        if (rb != null)
        {
            rb.velocity = direction * throwForce;
            rb.AddTorque(10f, ForceMode2D.Impulse);

            // Ignore collision with the throwing player to prevent instant stop
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Collider2D playerCol  = player.GetComponent<Collider2D>();
                Collider2D grenadeCol = GetComponent<Collider2D>();
                if (playerCol != null && grenadeCol != null)
                    Physics2D.IgnoreCollision(playerCol, grenadeCol, true);
            }
        }

        StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    private void Explode()
    {
        // Visual effect
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        else
            Debug.LogWarning("[Grenade] No explosion effect prefab assigned.");

        // Area damage — apply to anything with a PlayerHealth component in radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D col in hitColliders)
        {
            PlayerHealth health = col.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Debug.Log($"[Grenade] Dealt {damage} damage to {col.name}.");
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
