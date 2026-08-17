using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles exploding player body parts (gibbing) on death with 2D physics impulse,
/// angular spin, blood blast visual effects, camera shake, and strict map collision containment.
/// </summary>
public class PlayerBodyExploder : MonoBehaviour
{
    private static PhysicsMaterial2D gibPhysicsMaterial;

    private static PhysicsMaterial2D GetGibPhysicsMaterial()
    {
        if (gibPhysicsMaterial == null)
        {
            gibPhysicsMaterial = new PhysicsMaterial2D("GibPhysicsMat")
            {
                bounciness = 0.35f,
                friction = 0.5f
            };
        }
        return gibPhysicsMaterial;
    }

    public static void ExplodePlayer(Transform playerTransform, Transform visualRoot = null)
    {
        if (playerTransform == null) return;
        Vector3 centerPos = playerTransform.position;

        if (visualRoot == null)
        {
            visualRoot = playerTransform.Find("Visuals");
            if (visualRoot == null) visualRoot = playerTransform.Find("Character");
            if (visualRoot == null) visualRoot = playerTransform;
        }

        // 1. Collect all active SpriteRenderers in visualRoot
        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        // Container GameObject for the exploded body parts
        GameObject gibGroup = new GameObject($"{playerTransform.name}_ExplodedParts");
        gibGroup.transform.position = centerPos;

        // Spawn a juicy central blood explosion blast effect
        CreateBloodExplosionFX(centerPos);

        List<GameObject> gibs = new List<GameObject>();
        PhysicsMaterial2D mat = GetGibPhysicsMaterial();

        foreach (var r in renderers)
        {
            if (r == null || !r.enabled || r.sprite == null) continue;

            string rName = r.gameObject.name.ToLower();

            // Skip ghost visuals, drop points, UI, or aim dot helpers
            if (rName.Contains("ghost") || rName.Contains("aim") || rName.Contains("shadow") || rName.Contains("dot") || rName.Contains("canvas"))
                continue;

            // Create individual Gib object for each body part
            GameObject gib = new GameObject($"Gib_{r.gameObject.name}");
            gib.transform.SetParent(gibGroup.transform);
            gib.transform.position = r.transform.position;
            gib.transform.rotation = r.transform.rotation;
            gib.transform.localScale = r.transform.lossyScale;

            SpriteRenderer gibSr = gib.AddComponent<SpriteRenderer>();
            gibSr.sprite = r.sprite;
            gibSr.color = r.color;
            gibSr.sortingLayerName = r.sortingLayerName;
            gibSr.sortingOrder = r.sortingOrder;
            gibSr.flipX = r.flipX;
            gibSr.flipY = r.flipY;

            // Add CircleCollider2D first with PhysicsMaterial2D for bouncing off walls
            CircleCollider2D col = gib.AddComponent<CircleCollider2D>();
            col.sharedMaterial = mat;
            col.isTrigger = false;
            col.radius = Mathf.Max(0.18f, Mathf.Min(gibSr.bounds.extents.x, gibSr.bounds.extents.y) * 0.9f);

            // Add 2D Physics to explode body parts outwards
            Rigidbody2D rb = gib.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1.0f;
            rb.drag = 3.0f; // Rapid deceleration so parts settle inside room
            rb.angularDrag = 1.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Prevents wall tunneling

            // Random outward explosion direction with upward pop
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            if (randomDir.y < 0) randomDir.y = -randomDir.y * 0.5f;
            randomDir += new Vector2(0f, 0.3f);

            float forceMagnitude = Random.Range(3.5f, 6.0f);
            rb.AddForce(randomDir * forceMagnitude, ForceMode2D.Impulse);

            // Add spinning torque
            float torque = Random.Range(-350f, 350f);
            rb.AddTorque(torque, ForceMode2D.Impulse);

            gibs.Add(gib);
        }

        // Attach controller to handle wall containment, fading out, and destruction
        var controller = gibGroup.AddComponent<GibGroupController>();
        controller.Init(gibs);

        // Camera shake trigger
        if (CameraController.Instance != null)
        {
            CameraController.Instance.TriggerShake(0.35f, 0.25f);
        }
    }

    private static void CreateBloodExplosionFX(Vector3 position)
    {
        GameObject fxObj = new GameObject("BloodExplosionFX");
        fxObj.transform.position = position;

        // Core blood burst circle
        SpriteRenderer sr = fxObj.AddComponent<SpriteRenderer>();
        sr.sprite = ProceduralEffectsGenerator.GetSoftCircleSprite();
        sr.color = new Color(0.85f, 0.05f, 0.05f, 0.9f); // Crimson blood red
        sr.sortingLayerName = "explotion";
        sr.sortingOrder = 999;

        var animator = fxObj.AddComponent<BlastEffectAnimator>();
        animator.Animate(2.5f, 0.4f);

        // Secondary blood droplet particles flying out
        int dropletCount = 8;
        for (int i = 0; i < dropletCount; i++)
        {
            GameObject drop = new GameObject($"BloodDrop_{i}");
            drop.transform.position = position;
            drop.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);

            SpriteRenderer dropSr = drop.AddComponent<SpriteRenderer>();
            dropSr.sprite = ProceduralEffectsGenerator.GetSoftCircleSprite();
            dropSr.color = new Color(0.75f, 0.02f, 0.02f, 0.85f);
            dropSr.sortingLayerName = "explotion";
            dropSr.sortingOrder = 998;

            Rigidbody2D rb = drop.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1.5f;
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
            rb.velocity = dir;

            Destroy(drop, Random.Range(1.5f, 2.5f));
        }
    }
}

/// <summary>
/// Enforces map wall containment, fades out body part gibs after a short delay, and cleans them up.
/// </summary>
public class GibGroupController : MonoBehaviour
{
    private List<GameObject> gibs;
    private WalkableFloorZone[] walkableZones;

    public void Init(List<GameObject> gibList)
    {
        gibs = gibList;
        walkableZones = Object.FindObjectsOfType<WalkableFloorZone>();
        StartCoroutine(FadeAndDestroyRoutine());
    }

    private void Update()
    {
        if (gibs == null || walkableZones == null || walkableZones.Length == 0) return;

        // Keep all moving gibs clamped strictly inside walkable room floor zones
        foreach (var gib in gibs)
        {
            if (gib == null) continue;

            Rigidbody2D rb = gib.GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            {
                Vector3 currentPos = gib.transform.position;
                Vector3 clampedPos = ClampToWalkableZones(currentPos);
                if (currentPos != clampedPos)
                {
                    gib.transform.position = clampedPos;
                    rb.velocity = -rb.velocity * 0.4f; // Bounce back inward
                }
            }
        }
    }

    private Vector3 ClampToWalkableZones(Vector3 currentPos)
    {
        if (walkableZones == null || walkableZones.Length == 0) return currentPos;

        Vector2 p = currentPos;
        foreach (var z in walkableZones)
        {
            if (z != null && z.ContainsPoint(p))
                return currentPos; // Safely inside walkable floor zone
        }

        // Outside all zones — clamp to closest point on any zone collider
        Vector3 closest = currentPos;
        float minDistance = float.MaxValue;

        foreach (var z in walkableZones)
        {
            if (z != null && z.zoneCollider != null)
            {
                Vector3 cp = z.zoneCollider.ClosestPoint(currentPos);
                float dist = Vector3.Distance(currentPos, cp);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = cp;
                }
            }
        }

        return closest;
    }

    private System.Collections.IEnumerator FadeAndDestroyRoutine()
    {
        // Stay solid on the ground for 3 seconds
        yield return new WaitForSeconds(3.0f);

        // Fade out over 1.5 seconds
        float duration = 1.5f;
        float elapsed = 0f;

        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        if (gibs != null)
        {
            foreach (var g in gibs)
            {
                if (g != null)
                {
                    var sr = g.GetComponent<SpriteRenderer>();
                    if (sr != null) renderers.Add(sr);
                }
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
