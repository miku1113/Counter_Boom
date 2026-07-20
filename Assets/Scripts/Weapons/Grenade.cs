using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class Grenade : MonoBehaviour
{
    [Header("Settings")]
    public float fuseTime        = 3f;
    public float explosionRadius = 5f;
    public float throwForce      = 10f;

    [Header("Effects")]
    public GameObject explosionEffect;

    [Header("Physics")]
    [SerializeField] protected Rigidbody2D rb;

    protected virtual void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    public void Throw(Vector2 direction, Collider2D throwerCollider = null)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.velocity = direction * throwForce;
            rb.AddTorque(10f, ForceMode2D.Impulse);

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
        // Spawns standard visual effect if assigned
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
