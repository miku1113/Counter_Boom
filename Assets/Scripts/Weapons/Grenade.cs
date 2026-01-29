using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    [Header("Settings")]
    public float fuseTime = 3f;
    public float explosionRadius = 5f;
    public int damage = 50;
    public GameObject explosionEffect;
    public float throwForce = 10f;

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
            // Use velocity for immediate response (better for top-down throws)
            rb.velocity = direction * throwForce;
            rb.AddTorque(10f, ForceMode2D.Impulse);

            // Ignore collision with Player to prevent instant stop
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Collider2D playerCol = player.GetComponent<Collider2D>();
                Collider2D grenadeCol = GetComponent<Collider2D>();
                if (playerCol != null && grenadeCol != null)
                {
                    Physics2D.IgnoreCollision(playerCol, grenadeCol, true);
                }
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
        // 1. Visual Effect
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }
        else
        {
            // Fallback visualization if no prefab assigned
            Debug.Log("Grenade: No explosion effect assigned! Creating debug sphere.");
            GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.transform.position = transform.position;
            debugSphere.transform.localScale = Vector3.one * explosionRadius * 2;
            Destroy(debugSphere.GetComponent<Collider>()); // Visual only
            Material mat = debugSphere.GetComponent<Renderer>().material;
            mat.color = new Color(1, 0, 0, 0.5f); // Red transparent
            Destroy(debugSphere, 0.5f);
        }

        // 2. Damage Logic
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D obj in hitObjects)
        {
            // Only damage things that have health - placeholder logic
            Debug.Log($"Grenade hit: {obj.name}");
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
