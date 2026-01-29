using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Properties")]
    public int damage = 10;
    public float speed = 10f;
    public float lifetime = 3f;
    
    private Rigidbody2D rb;
    private Vector2 direction;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    
    /// <summary>
    /// Initializes the bullet with direction and speed
    /// </summary>
    public void Initialize(Vector2 fireDirection, float bulletSpeed, int bulletDamage)
    {
        direction = fireDirection.normalized;
        speed = bulletSpeed;
        damage = bulletDamage;
        
        // Set rotation to match direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }
    
    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.velocity = direction * speed;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if hit an enemy or obstacle
        if (collision.CompareTag("Enemy"))
        {
            // Deal damage (implement health system later)
            Debug.Log($"Hit enemy for {damage} damage!");
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            // Destroy on hit
            Destroy(gameObject);
        }
    }
}
