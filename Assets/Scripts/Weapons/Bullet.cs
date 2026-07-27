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
    private GameObject  shooter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Initialises the bullet with direction, speed, damage, and shooter reference.
    /// </summary>
    public void Initialize(Vector2 fireDirection, float bulletSpeed, int bulletDamage, GameObject shooterObject = null)
    {
        direction = fireDirection.normalized;
        speed     = bulletSpeed;
        damage    = bulletDamage;
        shooter   = shooterObject;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // Ignore collisions with shooter colliders
        if (shooter != null)
        {
            Collider2D bulletCol = GetComponent<Collider2D>();
            if (bulletCol != null)
            {
                Collider2D[] shooterCols = shooter.transform.root.GetComponentsInChildren<Collider2D>();
                foreach (Collider2D col in shooterCols)
                {
                    if (col != null) Physics2D.IgnoreCollision(col, bulletCol, true);
                }
            }
        }

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (rb != null)
            rb.velocity = direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ProcessHit(collision);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ProcessHit(collision.collider);
    }

    private void ProcessHit(Collider2D collision)
    {
        if (collision == null) return;

        // Ignore hitting the shooter
        if (shooter != null && (collision.gameObject == shooter || collision.transform.root == shooter.transform.root))
        {
            return;
        }

        // Primary check: does the hit object have a health component?
        PlayerHealth health = collision.GetComponent<PlayerHealth>();
        if (health == null) health = collision.GetComponentInParent<PlayerHealth>();

        if (health != null)
        {
            // Ignore hitting shooter's own health component
            if (shooter != null && health.gameObject == shooter.transform.root.gameObject)
            {
                return;
            }

            // Apply damage to enemy player
            health.TakeDamage(damage);
            Debug.Log($"[Bullet] Hit '{collision.name}' for {damage} damage.");

            Destroy(gameObject);
            return;
        }

        // Secondary check: destroy on environment colliders
        if (collision.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            if (!collision.isTrigger)
                Destroy(gameObject);
        }
    }
}
