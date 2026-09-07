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
        Vector2 hitPoint = collision != null ? collision.ClosestPoint(transform.position) : (Vector2)transform.position;
        ProcessHit(collision, hitPoint);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        ProcessHit(collision.collider, hitPoint);
    }

    private void ProcessHit(Collider2D collision, Vector2 hitPoint)
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

            // Friendly fire check: Teammates take NO damage!
            bool isTeammate = AreTeammates(shooter, health.gameObject);

            Vector2 hitNormal = ((Vector2)transform.position - hitPoint).normalized;
            if (hitNormal.sqrMagnitude < 0.001f) hitNormal = -direction;

            // Spawn visual hit effect directly on the body where bullet hit
            ProceduralEffectsGenerator.CreateBulletBodyHitEffect(hitPoint, hitNormal, isTeammate);

            if (isTeammate)
            {
                Debug.Log($"[Bullet] Hit teammate '{collision.name}'. Shield effect played without damage.");
            }
            else
            {
                // Only damage opponents! Only the shooter's client triggers networked damage to prevent duplicate hits
                bool isShooterOwner = shooter == null || 
                                      !shooter.TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj) || 
                                      !netObj.IsSpawned || 
                                      netObj.IsOwner;

                if (isShooterOwner)
                {
                    health.TakeDamage(damage);
                    Debug.Log($"[Bullet] Hit opponent '{collision.name}' for {damage} damage.");
                }
            }

            Destroy(gameObject);
            return;
        }

        // Secondary check: destroy on environment colliders
        int layer = collision.gameObject.layer;
        if (layer == LayerMask.NameToLayer("Default") || layer == LayerMask.NameToLayer("Obstacle") || layer == LayerMask.NameToLayer("Wall"))
        {
            if (!collision.isTrigger)
            {
                Vector2 hitNormal = ((Vector2)transform.position - hitPoint).normalized;
                if (hitNormal.sqrMagnitude < 0.001f) hitNormal = -direction;
                ProceduralEffectsGenerator.CreateBulletSurfaceHitEffect(hitPoint, hitNormal);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Checks whether two GameObjects belong to the same team (Thief vs Hostage, Bot vs Bot, etc.)
    /// </summary>
    public static bool AreTeammates(GameObject objA, GameObject objB)
    {
        if (objA == null || objB == null) return false;
        if (objA == objB) return true;
        if (objA.transform.root == objB.transform.root) return true;

        bool aIsBot = objA.CompareTag("Bot") || objA.GetComponentInParent<AiBotController>() != null || objA.name.ToLower().Contains("bot");
        bool bIsBot = objB.CompareTag("Bot") || objB.GetComponentInParent<AiBotController>() != null || objB.name.ToLower().Contains("bot");

        if (aIsBot && bIsBot) return true; // Bots are allies with each other
        if (aIsBot != bIsBot) return false; // Bot vs Human are always opponents

        PlayerController pcA = objA.GetComponent<PlayerController>() ?? objA.GetComponentInParent<PlayerController>();
        PlayerController pcB = objB.GetComponent<PlayerController>() ?? objB.GetComponentInParent<PlayerController>();

        if (pcA != null && pcB != null)
        {
            // If both players have the same role (both Thieves or both Hostages), they are teammates!
            return pcA.playerRole.Value == pcB.playerRole.Value;
        }

        return false;
    }
}
