using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Properties")]
    public int   damage   = 10;
    public float speed    = 10f;
    public float lifetime = 3f;

    private Rigidbody2D rb;
    private Vector2     direction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Initialises the bullet with direction, speed, and damage.
    /// </summary>
    public void Initialize(Vector2 fireDirection, float bulletSpeed, int bulletDamage)
    {
        direction = fireDirection.normalized;
        speed     = bulletSpeed;
        damage    = bulletDamage;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (rb != null)
            rb.velocity = direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Primary check: does the hit object have a health component?
        // This works regardless of what tags exist in the project.
        PlayerHealth health = collision.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Debug.Log($"[Bullet] Hit '{collision.name}' for {damage} damage.");
            Destroy(gameObject);
            return;
        }

        // Secondary check: destroy on environment colliders
        // Only use CompareTag for tags that are built-in or confirmed to exist.
        // "Wall" and "Obstacle" must be added to Tags & Layers in Project Settings.
        if (collision.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            // Fallback: destroy if we hit anything solid that isn't a trigger
            if (!collision.isTrigger)
                Destroy(gameObject);
        }
    }
}
