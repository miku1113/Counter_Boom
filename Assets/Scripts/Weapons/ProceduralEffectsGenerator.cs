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

    // ─── Gun Muzzle Flash and Smoke ──────────────────────────────────────────

    public static void CreateMuzzleFlashAndSmoke(Vector3 firePos, Vector2 direction, Transform parent = null)
    {
        GameObject root = new GameObject("ProceduralMuzzleFlash");
        root.transform.position = firePos;
        if (parent != null)
        {
            root.transform.SetParent(parent, true);
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 1. Core Flash Spark
        GameObject flashCore = new GameObject("FlashCore");
        flashCore.transform.SetParent(root.transform, false);
        flashCore.transform.localPosition = Vector3.zero;
        var coreSr = flashCore.AddComponent<SpriteRenderer>();
        coreSr.sprite = GetSoftCircleSprite();
        coreSr.color = new Color(1f, 0.98f, 0.65f, 1f); // Bright yellow-white incandescent flash
        coreSr.sortingLayerName = "explotion";
        coreSr.sortingOrder = 1005;

        // 2. Outer Flash Halo
        GameObject flashHalo = new GameObject("FlashHalo");
        flashHalo.transform.SetParent(root.transform, false);
        flashHalo.transform.localPosition = (Vector3)(direction.normalized * 0.08f);
        var haloSr = flashHalo.AddComponent<SpriteRenderer>();
        haloSr.sprite = GetSoftCircleSprite();
        haloSr.color = new Color(1f, 0.6f, 0.15f, 0.85f); // Fiery orange halo
        haloSr.sortingLayerName = "explotion";
        haloSr.sortingOrder = 1004;

        var flashAnim = root.AddComponent<MuzzleFlashAnimator>();
        flashAnim.Animate(flashCore.transform, flashHalo.transform, 0.07f);

        // 3. Gun Barrel Smoke Puffs (spawned in world space so smoke drifts naturally)
        int smokeCount = Random.Range(2, 4);
        for (int i = 0; i < smokeCount; i++)
        {
            GameObject smoke = new GameObject($"GunSmoke_{i}");
            smoke.transform.position = firePos + (Vector3)(direction.normalized * (0.05f + i * 0.06f));

            var smokeSr = smoke.AddComponent<SpriteRenderer>();
            smokeSr.sprite = GetSoftCircleSprite();
            float grey = Random.Range(0.75f, 0.88f);
            smokeSr.color = new Color(grey, grey, grey, Random.Range(0.45f, 0.65f));
            smokeSr.sortingLayerName = "explotion";
            smokeSr.sortingOrder = 1002 - i;

            Vector2 driftVelocity = direction.normalized * Random.Range(2.0f, 3.8f) + Random.insideUnitCircle * 0.6f;
            float startScale = Random.Range(0.12f, 0.18f);
            float endScale = Random.Range(0.32f, 0.48f);
            float lifetime = Random.Range(0.22f, 0.35f);

            var smokeAnim = smoke.AddComponent<GunSmokeAnimator>();
            smokeAnim.Animate(driftVelocity, startScale, endScale, lifetime);
        }

        Object.Destroy(root, 0.12f);
    }

    // ─── Bullet Body Hit Effect (Blood/Impact for Opponent, Deflection for Teammate) ─

    public static void CreateBulletBodyHitEffect(Vector3 hitPoint, Vector2 hitNormal, bool isTeammate)
    {
        GameObject hitRoot = new GameObject("BulletBodyHitEffect");
        hitRoot.transform.position = hitPoint;

        float normalAngle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;

        if (!isTeammate)
        {
            // === OPPONENT HIT: Crimson blood splash & bright kinetic impact flash ===
            
            // 1. Central Impact Flash
            GameObject flashObj = new GameObject("HitFlash");
            flashObj.transform.SetParent(hitRoot.transform, false);
            var flashSr = flashObj.AddComponent<SpriteRenderer>();
            flashSr.sprite = GetSoftCircleSprite();
            flashSr.color = new Color(1f, 0.2f, 0.15f, 0.95f); // Crimson impact flash
            flashSr.sortingLayerName = "explotion";
            flashSr.sortingOrder = 1006;
            var flashAnim = flashObj.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.42f, 0.09f, true);

            // 2. Expanding Impact Ring
            GameObject ringObj = new GameObject("HitRing");
            ringObj.transform.SetParent(hitRoot.transform, false);
            var ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = GetSoftCircleSprite();
            ringSr.color = new Color(1f, 0.4f, 0.1f, 0.8f);
            ringSr.sortingLayerName = "explotion";
            ringSr.sortingOrder = 1004;
            var ringAnim = ringObj.AddComponent<QuickScaleFadeAnimator>();
            ringAnim.Animate(0.65f, 0.14f, false);

            // 3. 6-8 Blood & Spark Droplets bursting along hitNormal
            int particleCount = Random.Range(6, 9);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject drop = new GameObject($"BloodDroplet_{i}");
                drop.transform.position = hitPoint;
                var dropSr = drop.AddComponent<SpriteRenderer>();
                dropSr.sprite = GetSoftCircleSprite();

                bool isSpark = Random.value > 0.4f;
                dropSr.color = isSpark 
                    ? new Color(1f, Random.Range(0.3f, 0.6f), 0.1f, 1f) 
                    : new Color(Random.Range(0.75f, 0.95f), 0.05f, 0.08f, 0.95f);
                dropSr.sortingLayerName = "explotion";
                dropSr.sortingOrder = 1005;

                float spreadAngle = (normalAngle + Random.Range(-55f, 55f)) * Mathf.Deg2Rad;
                Vector2 burstDir = new Vector2(Mathf.Cos(spreadAngle), Mathf.Sin(spreadAngle)) * Random.Range(3.5f, 8.0f);

                float dropScale = Random.Range(0.08f, 0.16f);
                float dropLifetime = Random.Range(0.18f, 0.32f);

                var particleAnim = drop.AddComponent<ImpactParticleAnimator>();
                particleAnim.Animate(burstDir, dropScale, dropLifetime);
            }
        }
        else
        {
            // === TEAMMATE HIT: Electric Cyan Deflection & Energy Shield Absorption ===
            
            // 1. Deflection Flash
            GameObject shieldFlash = new GameObject("ShieldFlash");
            shieldFlash.transform.SetParent(hitRoot.transform, false);
            var flashSr = shieldFlash.AddComponent<SpriteRenderer>();
            flashSr.sprite = GetSoftCircleSprite();
            flashSr.color = new Color(0.15f, 0.88f, 1f, 0.95f); // Vivid electric cyan
            flashSr.sortingLayerName = "explotion";
            flashSr.sortingOrder = 1006;
            var flashAnim = shieldFlash.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.48f, 0.11f, true);

            // 2. Deflection Wave Ring
            GameObject waveObj = new GameObject("ShieldWave");
            waveObj.transform.SetParent(hitRoot.transform, false);
            var waveSr = waveObj.AddComponent<SpriteRenderer>();
            waveSr.sprite = GetSoftCircleSprite();
            waveSr.color = new Color(0.4f, 0.95f, 1f, 0.75f);
            waveSr.sortingLayerName = "explotion";
            waveSr.sortingOrder = 1004;
            var waveAnim = waveObj.AddComponent<QuickScaleFadeAnimator>();
            waveAnim.Animate(0.75f, 0.16f, false);

            // 3. 4-6 Cyan Deflection Glints radiating outward
            int glintCount = Random.Range(4, 7);
            for (int i = 0; i < glintCount; i++)
            {
                GameObject glint = new GameObject($"DeflectionGlint_{i}");
                glint.transform.position = hitPoint;
                var glintSr = glint.AddComponent<SpriteRenderer>();
                glintSr.sprite = GetSoftCircleSprite();
                glintSr.color = new Color(0.6f, 0.98f, 1f, 0.9f);
                glintSr.sortingLayerName = "explotion";
                glintSr.sortingOrder = 1005;

                float spreadAngle = (normalAngle + Random.Range(-65f, 65f)) * Mathf.Deg2Rad;
                Vector2 burstDir = new Vector2(Mathf.Cos(spreadAngle), Mathf.Sin(spreadAngle)) * Random.Range(2.5f, 5.5f);

                float glintScale = Random.Range(0.07f, 0.13f);
                float glintLifetime = Random.Range(0.14f, 0.24f);

                var particleAnim = glint.AddComponent<ImpactParticleAnimator>();
                particleAnim.Animate(burstDir, glintScale, glintLifetime);
            }
        }

        Object.Destroy(hitRoot, 0.35f);
    }

    // ─── Bullet Environment Surface Hit Effect ──────────────────────────────

    public static void CreateBulletSurfaceHitEffect(Vector3 hitPoint, Vector2 hitNormal)
    {
        GameObject hitRoot = new GameObject("BulletSurfaceHitEffect");
        hitRoot.transform.position = hitPoint;

        float normalAngle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;

        // 1. Surface Impact Flash
        GameObject flash = new GameObject("SurfaceFlash");
        flash.transform.SetParent(hitRoot.transform, false);
        var flashSr = flash.AddComponent<SpriteRenderer>();
        flashSr.sprite = GetSoftCircleSprite();
        flashSr.color = new Color(1f, 0.85f, 0.4f, 0.9f);
        flashSr.sortingLayerName = "explotion";
        flashSr.sortingOrder = 1005;
        var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
        flashAnim.Animate(0.35f, 0.08f, true);

        // 2. 4 Wall Ricochet Sparks & Dust
        for (int i = 0; i < 4; i++)
        {
            GameObject spark = new GameObject($"WallSpark_{i}");
            spark.transform.position = hitPoint;
            var sparkSr = spark.AddComponent<SpriteRenderer>();
            sparkSr.sprite = GetSoftCircleSprite();
            sparkSr.color = new Color(1f, Random.Range(0.7f, 0.95f), 0.2f, 1f);
            sparkSr.sortingLayerName = "explotion";
            sparkSr.sortingOrder = 1004;

            float angle = (normalAngle + Random.Range(-45f, 45f)) * Mathf.Deg2Rad;
            Vector2 burstDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(3.0f, 6.5f);

            var anim = spark.AddComponent<ImpactParticleAnimator>();
            anim.Animate(burstDir, Random.Range(0.06f, 0.12f), Random.Range(0.12f, 0.22f));
        }

        Object.Destroy(hitRoot, 0.25f);
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

// ─── Gun Muzzle Flash & Smoke Animator ───────────────────────────────────────

public class MuzzleFlashAnimator : MonoBehaviour
{
    public void Animate(Transform core, Transform halo, float duration)
    {
        StartCoroutine(Routine(core, halo, duration));
    }

    private System.Collections.IEnumerator Routine(Transform core, Transform halo, float duration)
    {
        float elapsed = 0f;
        Vector3 startCore = Vector3.one * 0.35f;
        Vector3 startHalo = Vector3.one * 0.55f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (core != null) core.localScale = Vector3.Lerp(startCore, Vector3.zero, t);
            if (halo != null) halo.localScale = Vector3.Lerp(startHalo, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}

// ─── Gun Smoke Puff Animator ─────────────────────────────────────────────────

public class GunSmokeAnimator : MonoBehaviour
{
    public void Animate(Vector2 velocity, float startScale, float endScale, float lifetime)
    {
        StartCoroutine(Routine(velocity, startScale, endScale, lifetime));
    }

    private System.Collections.IEnumerator Routine(Vector2 velocity, float startScale, float endScale, float lifetime)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            transform.position += (Vector3)(velocity * (1f - t * 0.5f) * Time.deltaTime);
            float curScale = Mathf.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            transform.localScale = Vector3.one * curScale;

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}

// ─── Quick Scale Fade Animator (Hit Flash & Rings) ───────────────────────────

public class QuickScaleFadeAnimator : MonoBehaviour
{
    public void Animate(float maxScale, float duration, bool shrinkOnExit)
    {
        StartCoroutine(Routine(maxScale, duration, shrinkOnExit));
    }

    private System.Collections.IEnumerator Routine(float maxScale, float duration, bool shrinkOnExit)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float curScale = shrinkOnExit ? Mathf.Lerp(maxScale, 0f, t) : Mathf.Lerp(0.1f, maxScale, t);
            transform.localScale = Vector3.one * curScale;

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}

// ─── Impact Particle Droplet & Spark Animator ────────────────────────────────

public class ImpactParticleAnimator : MonoBehaviour
{
    public void Animate(Vector2 velocity, float startScale, float lifetime)
    {
        StartCoroutine(Routine(velocity, startScale, lifetime));
    }

    private System.Collections.IEnumerator Routine(Vector2 velocity, float startScale, float lifetime)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            transform.position += (Vector3)(velocity * (1f - t * 0.7f) * Time.deltaTime);
            float curScale = Mathf.Lerp(startScale, startScale * 0.2f, t);
            transform.localScale = Vector3.one * curScale;

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}

