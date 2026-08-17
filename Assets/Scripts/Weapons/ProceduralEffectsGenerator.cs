using UnityEngine;

public static class ProceduralEffectsGenerator
{
    private static Sprite softCircleSprite;

    public static Sprite GetSoftCircleSprite()
    {
        if (softCircleSprite != null) return softCircleSprite;

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ratio = distance / radius;
                if (ratio < 1f)
                {
                    // Soft radial falloff
                    float alpha = Mathf.SmoothStep(1f, 0f, ratio);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        softCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return softCircleSprite;
    }

    private static Sprite realisticFireSprite;

    public static Sprite GetRealisticFireSprite()
    {
        if (realisticFireSprite != null) return realisticFireSprite;

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxRadius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);
                
                // Add soft organic noise displacement to the radius for billowy flame edges
                float noise = Mathf.Sin(angle * 5f) * 0.08f + Mathf.Cos(angle * 7f) * 0.06f;
                float ratio = (dist / maxRadius) + noise;

                if (ratio < 1f)
                {
                    float alpha = Mathf.Pow(1f - Mathf.Clamp01(ratio), 1.4f);
                    
                    // Bake natural thermal fire color gradient from center outward:
                    // Center (White-Yellow) -> Mid (Fiery Orange) -> Edge (Crimson Red & Dark Charcoal)
                    Color colorGradient;
                    if (ratio < 0.25f)
                    {
                        colorGradient = Color.Lerp(new Color(1f, 1f, 0.9f, alpha), new Color(1f, 0.85f, 0.2f, alpha), ratio / 0.25f);
                    }
                    else if (ratio < 0.60f)
                    {
                        colorGradient = Color.Lerp(new Color(1f, 0.85f, 0.2f, alpha), new Color(1f, 0.4f, 0.02f, alpha), (ratio - 0.25f) / 0.35f);
                    }
                    else if (ratio < 0.85f)
                    {
                        colorGradient = Color.Lerp(new Color(1f, 0.4f, 0.02f, alpha), new Color(0.85f, 0.08f, 0.04f, alpha), (ratio - 0.60f) / 0.25f);
                    }
                    else
                    {
                        colorGradient = Color.Lerp(new Color(0.85f, 0.08f, 0.04f, alpha), new Color(0.25f, 0.04f, 0.04f, alpha * 0.7f), (ratio - 0.85f) / 0.15f);
                    }

                    pixels[y * size + x] = colorGradient;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        realisticFireSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return realisticFireSprite;
    }

    public static void CreateRedExplosionBlast(Vector3 position, float radius)
    {
        // Enforce a balanced blast radius scale (reduced by 30%)
        float blastRadius = Mathf.Max(radius, 3.2f);

        GameObject blastParent = new GameObject("RedExplosionBlastFX");
        blastParent.transform.position = position;

        // 1. Central White-Hot Incandescent Core Flash
        GameObject coreObj = new GameObject("RedCoreFlash");
        coreObj.transform.SetParent(blastParent.transform, false);
        SpriteRenderer coreSr = coreObj.AddComponent<SpriteRenderer>();
        coreSr.sprite = GetRealisticFireSprite();
        coreSr.color = new Color(1f, 0.96f, 0.75f, 1f); // White-yellow thermal core
        coreSr.sortingLayerName = "explotion";
        coreSr.sortingOrder = 1001;
        var coreAnim = coreObj.AddComponent<BlastEffectAnimator>();
        coreAnim.Animate(blastRadius * 1.5f, 0.18f);

        // 2. Realistic Volumetric Concentric Thermal Fireball Gradient Cluster
        int puffCount = 11;
        for (int i = 0; i < puffCount; i++)
        {
            GameObject puff = new GameObject($"RedFirePuff_{i}");
            puff.transform.SetParent(blastParent.transform, false);

            Vector2 offset = Random.insideUnitCircle * (blastRadius * 0.35f);
            puff.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
            sr.sprite = GetRealisticFireSprite();

            // Concentric Thermal Fire Gradient Layers:
            Color puffColor;
            if (i < 3)
            {
                // Inner Core: White-Hot Golden Yellow
                puffColor = new Color(1f, Random.Range(0.85f, 0.95f), 0.45f, 0.98f);
            }
            else if (i < 6)
            {
                // Mid Flame: Fiery Intense Orange
                puffColor = new Color(1f, Random.Range(0.45f, 0.65f), 0.05f, 0.95f);
            }
            else if (i < 9)
            {
                // Outer Flame: Crimson Ruby Red
                puffColor = new Color(0.92f, Random.Range(0.08f, 0.20f), 0.05f, 0.92f);
            }
            else
            {
                // Edge Smoke: Dark Charcoal Red
                puffColor = new Color(0.35f, 0.08f, 0.08f, 0.85f);
            }

            sr.color = puffColor;
            sr.sortingLayerName = "explotion";
            sr.sortingOrder = 999 - i;

            float puffScale = Random.Range(blastRadius * 1.5f, blastRadius * 2.3f);
            var puffAnim = puff.AddComponent<FirePuffAnimator>();
            puffAnim.Animate(puffScale, Random.Range(0.45f, 0.70f), sr.color);
        }

        // 3. Neon Red Shockwave Ring (reduced by 30%)
        GameObject shockObj = new GameObject("RedShockwaveRing");
        shockObj.transform.SetParent(blastParent.transform, false);
        SpriteRenderer shockSr = shockObj.AddComponent<SpriteRenderer>();
        shockSr.sprite = GetSoftCircleSprite();
        shockSr.color = new Color(1f, 0.1f, 0.25f, 0.85f); // Bright ruby red shockwave
        shockSr.sortingLayerName = "explotion";
        shockSr.sortingOrder = 998;
        var shockAnim = shockObj.AddComponent<BlastEffectAnimator>();
        shockAnim.Animate(blastRadius * 2.9f, 0.5f);

        // 4. Ground Scorch Burn Mark
        GameObject scorchObj = new GameObject("GroundScorchMark");
        scorchObj.transform.position = position;
        SpriteRenderer scorchSr = scorchObj.AddComponent<SpriteRenderer>();
        scorchSr.sprite = GetSoftCircleSprite();
        scorchSr.color = new Color(0.2f, 0.03f, 0.03f, 0.8f); // Dark red-charcoal burn mark
        scorchSr.sortingLayerName = "Default";
        scorchSr.sortingOrder = 5;
        scorchObj.transform.localScale = Vector3.one * (blastRadius * 1.4f);
        Object.Destroy(scorchObj, 5f);

        // 5. 16 Flying Fiery Red Spark & Shrapnel Debris Particles
        int sparkCount = 16;
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = new GameObject($"RedSparkDebris_{i}");
            spark.transform.position = position;
            spark.transform.localScale = Vector3.one * Random.Range(0.18f, 0.38f);

            SpriteRenderer sparkSr = spark.AddComponent<SpriteRenderer>();
            sparkSr.sprite = GetSoftCircleSprite();
            sparkSr.color = Random.value > 0.3f ? new Color(1f, 0.05f, 0.1f, 1f) : new Color(1f, 0.3f, 0.02f, 1f);
            sparkSr.sortingLayerName = "explotion";
            sparkSr.sortingOrder = 997;

            Rigidbody2D rb = spark.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0.8f;
            rb.drag = 1.8f;
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(5f, 10f);
            rb.velocity = dir;

            Object.Destroy(spark, Random.Range(0.5f, 1.1f));
        }

        // 6. Black & Red Smoke Cloud Puffs
        CreateSmokeCloud(position, blastRadius * 1.0f, 2.5f);

        Object.Destroy(blastParent, 2.8f);
    }

    public static void CreateExplosionBlast(Vector3 position, float radius)
    {
        CreateRedExplosionBlast(position, radius);
    }

    public static void CreateStunBlast(Vector3 position, float radius)
    {
        GameObject blastObj = new GameObject("ProceduralStunBlast");
        blastObj.transform.position = position;

        SpriteRenderer sr = blastObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftCircleSprite();
        sr.color = new Color(0.9f, 0.95f, 1f, 0.8f); // Bright blue-white flash
        sr.sortingLayerName = "explotion"; // Topmost sorting layer — renders above all sprites
        sr.sortingOrder = 999;

        var animator = blastObj.AddComponent<BlastEffectAnimator>();
        animator.Animate(radius, 0.35f);
    }

    public static GameObject CreateSmokeCloud(Vector3 position, float radius, float lifetime, GameObject customPuffPrefab = null)
    {
        GameObject smokeParent = new GameObject("ProceduralSmokeCloud");
        smokeParent.transform.position = position;

        int puffCount = 8;
        for (int i = 0; i < puffCount; i++)
        {
            GameObject puff;
            if (customPuffPrefab != null)
            {
                puff = Object.Instantiate(customPuffPrefab, smokeParent.transform);
                puff.name = $"SmokePuff_{i}";
            }
            else
            {
                puff = new GameObject($"SmokePuff_{i}");
                puff.transform.SetParent(smokeParent.transform);

                SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
                sr.sprite = GetSoftCircleSprite();

                // Slate grey color variations
                float col = Random.Range(0.45f, 0.6f);
                sr.color = new Color(col, col, col, 0f); // Starts transparent, fades in
            }

            // Random offset within the core smoke area
            Vector2 randomOffset = Random.insideUnitCircle * (radius * 0.35f);
            puff.transform.localPosition = new Vector3(randomOffset.x, randomOffset.y, 0f);

            float scale = Random.Range(radius * 0.22f, radius * 0.38f);
            puff.transform.localScale = Vector3.one * scale;

            var animator = puff.GetComponent<SmokePuffAnimator>();
            if (animator == null)
            {
                animator = puff.AddComponent<SmokePuffAnimator>();
            }
            animator.Animate(radius, lifetime);
        }

        return smokeParent;
    }
}


public class BlastEffectAnimator : MonoBehaviour
{
    public void Animate(float targetRadius, float duration)
    {
        StartCoroutine(AnimateRoutine(targetRadius, duration));
    }

    private System.Collections.IEnumerator AnimateRoutine(float targetRadius, float duration)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * targetRadius * 2f; // matching diameter

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f); // Fast expand, slow down

            transform.localScale = Vector3.Lerp(startScale, endScale, easeT);

            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.8f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

public class FirePuffAnimator : MonoBehaviour
{
    public void Animate(float maxScale, float duration, Color color)
    {
        StartCoroutine(AnimateRoutine(maxScale, duration, color));
    }

    private System.Collections.IEnumerator AnimateRoutine(float maxScale, float duration, Color targetColor)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "explotion";
            sr.sortingOrder = 999;
        }

        // Random organic rotation and slight non-uniform stretch
        float aspectX = Random.Range(0.85f, 1.25f);
        float aspectY = Random.Range(0.85f, 1.25f);
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector3 startScale = Vector3.zero;
        Vector3 endScale = new Vector3(maxScale * 2f * aspectX, maxScale * 2f * aspectY, 1f);
        Vector3 driftDir = (Vector3)Random.insideUnitCircle * (maxScale * 0.35f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Hypersonic initial explosion pop curve
            float easeT = 1f - Mathf.Pow(1f - t, 4f);

            transform.localScale = Vector3.Lerp(startScale, endScale, easeT);
            transform.position += driftDir * (Time.deltaTime / duration);

            if (sr != null)
            {
                Color c;
                if (t < 0.12f)
                {
                    // 0% - 12%: Blinding White-Hot Incandescent Detonation Core
                    c = Color.Lerp(new Color(1f, 0.95f, 0.7f, 1f), targetColor, t / 0.12f);
                }
                else if (t < 0.50f)
                {
                    // 12% - 50%: Intense Fiery Scarlet Flame
                    float subT = (t - 0.12f) / 0.38f;
                    c = Color.Lerp(targetColor, new Color(0.85f, 0.08f, 0.02f, 0.9f), subT);
                }
                else if (t < 0.80f)
                {
                    // 50% - 80%: Cooling Dark Crimson Embers
                    float subT = (t - 0.50f) / 0.30f;
                    c = Color.Lerp(new Color(0.85f, 0.08f, 0.02f, 0.9f), new Color(0.25f, 0.04f, 0.04f, 0.6f), subT);
                }
                else
                {
                    // 80% - 100%: Dissipating Charcoal Smoke
                    float subT = (t - 0.80f) / 0.20f;
                    c = Color.Lerp(new Color(0.25f, 0.04f, 0.04f, 0.6f), new Color(0.1f, 0.1f, 0.1f, 0f), subT);
                }
                sr.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

public class SmokePuffAnimator : MonoBehaviour
{
    public void Animate(float blastRadius, float lifetime)
    {
        StartCoroutine(AnimateRoutine(blastRadius, lifetime));
    }

    private System.Collections.IEnumerator AnimateRoutine(float blastRadius, float lifetime)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        
        // Force the smoke puffs to render on top of ALL sprites
        if (renderers != null)
        {
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    sr.sortingLayerName = "explotion"; // Topmost sorting layer in this project
                    sr.sortingOrder = 999;             // Above every other sprite (max used is 200)
                }
            }
        }

        Vector3 startScale = transform.localScale;
        Vector3 targetScale = startScale * Random.Range(1.12f, 1.22f); // puff swells up slightly less

        // Slow drift offset direction
        Vector3 driftDir = (Vector3)Random.insideUnitCircle * (blastRadius * 0.2f);


        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // Drift translation
            transform.position += driftDir * (Time.deltaTime / lifetime);

            // Scale expansion
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // Fade in at the start, fade out at the end
            if (renderers != null && renderers.Length > 0)
            {
                foreach (var sr in renderers)
                {
                    if (sr == null) continue;
                    Color c = sr.color;
                    if (t < 0.15f)
                    {
                        c.a = Mathf.Lerp(0f, 0.75f, t / 0.15f); // Fast fade in
                    }
                    else if (t > 0.6f)
                    {
                        c.a = Mathf.Lerp(0.75f, 0f, (t - 0.6f) / 0.4f); // Fade out
                    }
                    else
                    {
                        c.a = 0.75f;
                    }
                    sr.color = c;
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
