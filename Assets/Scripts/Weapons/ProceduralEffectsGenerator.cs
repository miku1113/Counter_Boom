using UnityEngine;

public static class ProceduralEffectsGenerator
{
    private static Sprite softCircleSprite;

    public static Sprite GetSoftCircleSprite()
    {
        if (softCircleSprite != null) return softCircleSprite;

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        Color transparentWhite = new Color(1f, 1f, 1f, 0f);

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
                    pixels[y * size + x] = transparentWhite;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        softCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return softCircleSprite;
    }

    private static Material sharedUnlitMaterial;

    public static Material GetUnlitMaterial()
    {
        if (sharedUnlitMaterial == null)
        {
            // Sprites/Default is the standard unlit shader with SrcAlpha OneMinusSrcAlpha, Lighting Off, ZWrite Off.
            // Eliminates any black opaque quad/box background in URP 2D.
            Shader shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");

            if (shader != null)
            {
                sharedUnlitMaterial = new Material(shader);
                sharedUnlitMaterial.name = "FX_Unlit_Sprite_Material";
                sharedUnlitMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
        return sharedUnlitMaterial;
    }

    public static SpriteRenderer CreateFxSprite(GameObject go, Sprite sprite, Color color, int sortingOrder = 60)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerName = "player"; // Below wall layer (wall is index 3, player is index 2)
        sr.sortingOrder = sortingOrder;
        Material mat = GetUnlitMaterial();
        if (mat != null) sr.sharedMaterial = mat;
        return sr;
    }

    private static Sprite starSparkleSprite;

    public static Sprite GetStarSparkleSprite()
    {
        if (starSparkleSprite != null) return starSparkleSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center.x) / (size / 2f);
                float dy = Mathf.Abs(y - center.y) / (size / 2f);
                float d = Mathf.Pow(dx, 0.5f) + Mathf.Pow(dy, 0.5f);
                if (d < 1f)
                {
                    float a = Mathf.Pow(1f - d, 1.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                else
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        starSparkleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return starSparkleSprite;
    }

    private static Sprite shellCasingSprite;

    public static Sprite GetShellCasingSprite()
    {
        if (shellCasingSprite != null) return shellCasingSprite;

        int w = 8, h = 4;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        Color brassRim = new Color(0.95f, 0.82f, 0.32f, 1f);
        Color brassDark = new Color(0.55f, 0.38f, 0.08f, 1f);
        Color brassBase = new Color(0.85f, 0.68f, 0.22f, 1f);
        Color brassHighlight = new Color(1f, 0.94f, 0.55f, 1f);
        Color interiorDark = new Color(0.30f, 0.18f, 0.05f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (x == 0)
                {
                    // Primer / Rim
                    tex.SetPixel(x, y, (y == 0 || y == h - 1) ? brassDark : brassRim);
                }
                else if (x == 1)
                {
                    // Extractor groove
                    tex.SetPixel(x, y, brassDark);
                }
                else if (x == w - 1)
                {
                    // Open hollow neck
                    tex.SetPixel(x, y, interiorDark);
                }
                else
                {
                    // Sleek polished brass cylinder with specular highlight
                    if (y == 2)
                        tex.SetPixel(x, y, brassHighlight);
                    else if (y == 0)
                        tex.SetPixel(x, y, brassDark);
                    else
                        tex.SetPixel(x, y, brassBase);
                }
            }
        }

        tex.Apply();
        shellCasingSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return shellCasingSprite;
    }

    private static Sprite realisticFireSprite;

    public static Sprite GetRealisticFireSprite()
    {
        if (realisticFireSprite != null) return realisticFireSprite;

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
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
                    pixels[y * size + x] = new Color(0.25f, 0.04f, 0.04f, 0f);
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
        CreateFxSprite(coreObj, GetRealisticFireSprite(), new Color(1f, 0.96f, 0.75f, 1f), 95);
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

            CreateFxSprite(puff, GetRealisticFireSprite(), puffColor, 90 - i);

            float puffScale = Random.Range(blastRadius * 1.5f, blastRadius * 2.3f);
            var puffAnim = puff.AddComponent<FirePuffAnimator>();
            puffAnim.Animate(puffScale, Random.Range(0.45f, 0.70f), puffColor);
        }

        // 3. Neon Red Shockwave Ring (reduced by 30%)
        GameObject shockObj = new GameObject("RedShockwaveRing");
        shockObj.transform.SetParent(blastParent.transform, false);
        CreateFxSprite(shockObj, GetSoftCircleSprite(), new Color(1f, 0.1f, 0.25f, 0.85f), 88);
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

            Color sCol = Random.value > 0.3f ? new Color(1f, 0.05f, 0.1f, 1f) : new Color(1f, 0.3f, 0.02f, 1f);
            CreateFxSprite(spark, GetSoftCircleSprite(), sCol, 92);

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

        CreateFxSprite(blastObj, GetSoftCircleSprite(), new Color(0.9f, 0.95f, 1f, 0.85f), 92);

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
        CreateFxSprite(flashCore, GetSoftCircleSprite(), new Color(1f, 0.98f, 0.65f, 1f), 65);

        // 2. Outer Flash Halo
        GameObject flashHalo = new GameObject("FlashHalo");
        flashHalo.transform.SetParent(root.transform, false);
        flashHalo.transform.localPosition = (Vector3)(direction.normalized * 0.08f);
        CreateFxSprite(flashHalo, GetSoftCircleSprite(), new Color(1f, 0.6f, 0.15f, 0.85f), 64);

        var flashAnim = root.AddComponent<MuzzleFlashAnimator>();
        flashAnim.Animate(flashCore.transform, flashHalo.transform, 0.07f);

        // 3. Gun Barrel Smoke Puffs (spawned in world space so smoke drifts naturally)
        int smokeCount = Random.Range(2, 4);
        for (int i = 0; i < smokeCount; i++)
        {
            GameObject smoke = new GameObject($"GunSmoke_{i}");
            smoke.transform.position = firePos + (Vector3)(direction.normalized * (0.05f + i * 0.06f));

            float grey = Random.Range(0.75f, 0.88f);
            CreateFxSprite(smoke, GetSoftCircleSprite(), new Color(grey, grey, grey, Random.Range(0.45f, 0.65f)), 60 - i);

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
            CreateFxSprite(flashObj, GetSoftCircleSprite(), new Color(1f, 0.2f, 0.15f, 0.95f), 75);
            var flashAnim = flashObj.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.42f, 0.09f, true);

            // 2. Expanding Impact Ring
            GameObject ringObj = new GameObject("HitRing");
            ringObj.transform.SetParent(hitRoot.transform, false);
            CreateFxSprite(ringObj, GetSoftCircleSprite(), new Color(1f, 0.4f, 0.1f, 0.8f), 74);
            var ringAnim = ringObj.AddComponent<QuickScaleFadeAnimator>();
            ringAnim.Animate(0.65f, 0.14f, false);

            // 3. 6-8 Blood & Spark Droplets bursting along hitNormal
            int particleCount = Random.Range(6, 9);
            for (int i = 0; i < particleCount; i++)
            {
                GameObject drop = new GameObject($"BloodDroplet_{i}");
                drop.transform.position = hitPoint;

                bool isSpark = Random.value > 0.4f;
                Color dropColor = isSpark 
                    ? new Color(1f, Random.Range(0.3f, 0.6f), 0.1f, 1f) 
                    : new Color(Random.Range(0.75f, 0.95f), 0.05f, 0.08f, 0.95f);
                CreateFxSprite(drop, GetSoftCircleSprite(), dropColor, 76);

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
            CreateFxSprite(shieldFlash, GetSoftCircleSprite(), new Color(0.15f, 0.88f, 1f, 0.95f), 75);
            var flashAnim = shieldFlash.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.48f, 0.11f, true);

            // 2. Deflection Wave Ring
            GameObject waveObj = new GameObject("ShieldWave");
            waveObj.transform.SetParent(hitRoot.transform, false);
            CreateFxSprite(waveObj, GetSoftCircleSprite(), new Color(0.4f, 0.95f, 1f, 0.75f), 74);
            var waveAnim = waveObj.AddComponent<QuickScaleFadeAnimator>();
            waveAnim.Animate(0.75f, 0.16f, false);

            // 3. 4-6 Cyan Deflection Glints radiating outward
            int glintCount = Random.Range(4, 7);
            for (int i = 0; i < glintCount; i++)
            {
                GameObject glint = new GameObject($"DeflectionGlint_{i}");
                glint.transform.position = hitPoint;
                CreateFxSprite(glint, GetSoftCircleSprite(), new Color(0.6f, 0.98f, 1f, 0.9f), 76);

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

    // ─── Bullet Environment Surface Hit Effect (Sparks, Concrete Dust & Chips) ──

    public static void CreateBulletSurfaceHitEffect(Vector3 hitPoint, Vector2 hitNormal, string surfaceTag = "")
    {
        GameObject hitRoot = new GameObject("BulletSurfaceHitEffect");
        hitRoot.transform.position = hitPoint;

        float normalAngle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;

        // 1. Surface Impact Flash
        GameObject flash = new GameObject("SurfaceFlash");
        flash.transform.SetParent(hitRoot.transform, false);
        CreateFxSprite(flash, GetSoftCircleSprite(), new Color(1f, 0.95f, 0.6f, 0.95f), 65);
        var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
        flashAnim.Animate(0.42f, 0.08f, true);

        // 2. Concrete / Wall Pulverized Dust Puffs (2-3 soft clouds drifting away from wall)
        int dustCount = Random.Range(2, 4);
        for (int i = 0; i < dustCount; i++)
        {
            GameObject dust = new GameObject($"WallDust_{i}");
            dust.transform.position = hitPoint + (Vector3)(Random.insideUnitCircle * 0.06f);
            float grey = Random.Range(0.70f, 0.85f);
            CreateFxSprite(dust, GetSoftCircleSprite(), new Color(grey, grey * 0.95f, grey * 0.90f, Random.Range(0.40f, 0.60f)), 62);

            float dustAngle = (normalAngle + Random.Range(-40f, 40f)) * Mathf.Deg2Rad;
            Vector2 dustVel = new Vector2(Mathf.Cos(dustAngle), Mathf.Sin(dustAngle)) * Random.Range(1.2f, 2.8f);

            var smokeAnim = dust.AddComponent<GunSmokeAnimator>();
            smokeAnim.Animate(dustVel, Random.Range(0.12f, 0.18f), Random.Range(0.32f, 0.48f), Random.Range(0.25f, 0.40f));
        }

        // 3. 6-8 Glowing Ricochet Sparks (using diamond/star sprite)
        int sparkCount = Random.Range(6, 9);
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = new GameObject($"WallSpark_{i}");
            spark.transform.position = hitPoint;
            Color sparkColor = Random.value > 0.35f 
                ? new Color(1f, Random.Range(0.85f, 1f), 0.35f, 1f) 
                : new Color(1f, 0.55f, 0.1f, 1f);
            CreateFxSprite(spark, GetStarSparkleSprite(), sparkColor, 66);

            float angle = (normalAngle + Random.Range(-55f, 55f)) * Mathf.Deg2Rad;
            Vector2 burstDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(4.0f, 8.5f);

            var anim = spark.AddComponent<ImpactParticleAnimator>();
            anim.Animate(burstDir, Random.Range(0.10f, 0.18f), Random.Range(0.14f, 0.26f));
        }

        // 4. Wall Impact Chip Mark (settles on wall surface)
        GameObject chip = new GameObject("WallChipMark");
        chip.transform.position = hitPoint;
        CreateFxSprite(chip, GetSoftCircleSprite(), new Color(0.18f, 0.18f, 0.20f, 0.75f), 20);
        chip.transform.localScale = Vector3.one * Random.Range(0.08f, 0.14f);
        Object.Destroy(chip, 4.0f);

        Object.Destroy(hitRoot, 0.30f);
    }

    // ─── Brass Shell Casing Ejection ─────────────────────────────────────────

    public static void CreateShellCasing(Vector3 ejectPos, Vector2 fireDir)
    {
        GameObject casing = new GameObject("ShellCasing");
        casing.transform.position = ejectPos;
        casing.transform.localScale = Vector3.one * 0.7f;

        CreateFxSprite(casing, GetShellCasingSprite(), Color.white, 25);

        // Eject perpendicular to firing direction with slight backward drift
        Vector2 rightDir = new Vector2(fireDir.y, -fireDir.x).normalized;
        Vector2 ejectVel = (rightDir * Random.Range(1.8f, 3.2f)) 
                         - (fireDir.normalized * Random.Range(0.2f, 0.6f)) 
                         + (Random.insideUnitCircle * 0.3f);
        float spinSpeed = Random.Range(540f, 1080f) * (Random.value > 0.5f ? 1f : -1f);

        var anim = casing.AddComponent<ShellCasingAnimator>();
        anim.Animate(ejectVel, spinSpeed, 2.5f);
    }

    // ─── Melee Hit Impact Effect (Vibrant RED on Enemy, Dust on Wall) ─────────

    public static void CreateMeleeHitEffect(Vector3 hitPoint, Vector2 punchDir, bool isEnemy)
    {
        GameObject root = new GameObject("MeleeHitImpactEffect");
        root.transform.position = hitPoint;

        float angle = Mathf.Atan2(punchDir.y, punchDir.x) * Mathf.Rad2Deg;

        if (isEnemy)
        {
            // === ENEMY HIT: VIBRANT RED IMPACT ===
            // 1. Central Red Impact Flash
            GameObject flash = new GameObject("PunchRedFlash");
            flash.transform.SetParent(root.transform, false);
            CreateFxSprite(flash, GetSoftCircleSprite(), new Color(1f, 0.12f, 0.12f, 1f), 75);
            var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.58f, 0.11f, true);

            // 2. Red Kinetic Shockwave Ring
            GameObject ring = new GameObject("PunchRedRing");
            ring.transform.SetParent(root.transform, false);
            CreateFxSprite(ring, GetSoftCircleSprite(), new Color(1f, 0.35f, 0.1f, 0.85f), 74);
            var ringAnim = ring.AddComponent<QuickScaleFadeAnimator>();
            ringAnim.Animate(0.85f, 0.16f, false);

            // 3. 6-8 Fiery Red & Amber Sparks bursting out in punch direction
            int sparkCount = Random.Range(6, 9);
            for (int i = 0; i < sparkCount; i++)
            {
                GameObject spark = new GameObject($"PunchRedSpark_{i}");
                spark.transform.position = hitPoint;
                Color sparkColor = Random.value > 0.4f ? new Color(1f, 0.1f, 0.15f, 1f) : new Color(1f, 0.6f, 0.1f, 1f);
                CreateFxSprite(spark, GetStarSparkleSprite(), sparkColor, 76);

                float spreadAngle = (angle + Random.Range(-50f, 50f)) * Mathf.Deg2Rad;
                Vector2 burstDir = new Vector2(Mathf.Cos(spreadAngle), Mathf.Sin(spreadAngle)) * Random.Range(3.5f, 7.5f);

                var anim = spark.AddComponent<ImpactParticleAnimator>();
                anim.Animate(burstDir, Random.Range(0.12f, 0.20f), Random.Range(0.18f, 0.30f));
            }
        }
        else
        {
            // === WALL / OBSTACLE PUNCH: Concrete dust & sparks ===
            GameObject flash = new GameObject("PunchWallFlash");
            flash.transform.SetParent(root.transform, false);
            CreateFxSprite(flash, GetSoftCircleSprite(), new Color(1f, 0.95f, 0.7f, 0.85f), 75);
            var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
            flashAnim.Animate(0.40f, 0.09f, true);

            int dustCount = 2;
            for (int i = 0; i < dustCount; i++)
            {
                GameObject dust = new GameObject($"PunchWallDust_{i}");
                dust.transform.position = hitPoint + (Vector3)(Random.insideUnitCircle * 0.05f);
                CreateFxSprite(dust, GetSoftCircleSprite(), new Color(0.75f, 0.75f, 0.75f, 0.5f), 72);
                float dustAngle = (angle + 180f + Random.Range(-35f, 35f)) * Mathf.Deg2Rad;
                Vector2 dustVel = new Vector2(Mathf.Cos(dustAngle), Mathf.Sin(dustAngle)) * Random.Range(1.2f, 2.5f);
                var smokeAnim = dust.AddComponent<GunSmokeAnimator>();
                smokeAnim.Animate(dustVel, 0.12f, 0.32f, 0.28f);
            }

            for (int i = 0; i < 4; i++)
            {
                GameObject spark = new GameObject($"PunchWallSpark_{i}");
                spark.transform.position = hitPoint;
                CreateFxSprite(spark, GetStarSparkleSprite(), new Color(1f, 0.85f, 0.3f, 1f), 76);
                float spAngle = (angle + 180f + Random.Range(-45f, 45f)) * Mathf.Deg2Rad;
                Vector2 spDir = new Vector2(Mathf.Cos(spAngle), Mathf.Sin(spAngle)) * Random.Range(2.5f, 5.0f);
                var anim = spark.AddComponent<ImpactParticleAnimator>();
                anim.Animate(spDir, 0.09f, 0.20f);
            }
        }

        Object.Destroy(root, 0.35f);
    }

    public static void CreateMeleePunchEffect(Vector3 punchPos, Vector2 punchDir)
    {
        CreateMeleeHitEffect(punchPos, punchDir, isEnemy: true);
    }

    // ─── Pickup Sparkle Burst (Keys, Medkits, Weapons) ──────────────────────

    public static void CreatePickupSparkleBurst(Vector3 pos, Color sparkleColor)
    {
        GameObject burstRoot = new GameObject("PickupSparkleBurst");
        burstRoot.transform.position = pos;

        // 1. Radiant Glow Ring
        GameObject ring = new GameObject("PickupRing");
        ring.transform.SetParent(burstRoot.transform, false);
        CreateFxSprite(ring, GetSoftCircleSprite(), new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0.85f), 70);
        var ringAnim = ring.AddComponent<QuickScaleFadeAnimator>();
        ringAnim.Animate(0.95f, 0.22f, false);

        // 2. 8 Twinkling Star Sparkles flying outward in 360 degree circle
        int count = 8;
        for (int i = 0; i < count; i++)
        {
            GameObject star = new GameObject($"PickupStar_{i}");
            star.transform.position = pos;
            Color starCol = Color.Lerp(sparkleColor, Color.white, Random.Range(0.2f, 0.7f));
            CreateFxSprite(star, GetStarSparkleSprite(), starCol, 72);

            float rad = (i * (360f / count) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(2.0f, 4.5f);

            var anim = star.AddComponent<ImpactParticleAnimator>();
            anim.Animate(dir, Random.Range(0.14f, 0.22f), Random.Range(0.25f, 0.45f));
        }

        Object.Destroy(burstRoot, 0.50f);
    }

    // ─── Safe Treasure Unlock Burst (Gold Celebration FX) ───────────────────

    public static void CreateSafeUnlockBurst(Vector3 pos)
    {
        GameObject safeRoot = new GameObject("SafeUnlockBurst");
        safeRoot.transform.position = pos;

        // 1. Central Golden Flash
        GameObject flash = new GameObject("SafeFlash");
        flash.transform.SetParent(safeRoot.transform, false);
        CreateFxSprite(flash, GetSoftCircleSprite(), new Color(1f, 0.92f, 0.5f, 0.95f), 74);
        var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
        flashAnim.Animate(1.2f, 0.25f, true);

        // 2. Expanding Golden Shockwave
        GameObject ring = new GameObject("SafeRing");
        ring.transform.SetParent(safeRoot.transform, false);
        CreateFxSprite(ring, GetSoftCircleSprite(), new Color(1f, 0.82f, 0.15f, 0.85f), 73);
        var ringAnim = ring.AddComponent<QuickScaleFadeAnimator>();
        ringAnim.Animate(1.8f, 0.40f, false);

        // 3. 16 Rising Golden Treasure Sparkles
        for (int i = 0; i < 16; i++)
        {
            GameObject star = new GameObject($"SafeGoldStar_{i}");
            star.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.2f);
            Color starCol = Random.value > 0.3f ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 0.98f, 0.65f, 1f);
            CreateFxSprite(star, GetStarSparkleSprite(), starCol, 75);

            float rad = Random.Range(30f, 150f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(2.5f, 5.5f);

            var anim = star.AddComponent<ImpactParticleAnimator>();
            anim.Animate(dir, Random.Range(0.14f, 0.24f), Random.Range(0.35f, 0.60f));
        }

        Object.Destroy(safeRoot, 0.70f);
    }

    // ─── Room Teleport Depart Effect (Warp Implosion at Exit / Door) ─────────

    public static void CreateTeleportDepartEffect(Vector3 pos)
    {
        GameObject root = new GameObject("TeleportDepartFX");
        root.transform.position = pos;

        // 1. Imploding Energy Core (contracts to zero)
        GameObject core = new GameObject("DepartCore");
        core.transform.SetParent(root.transform, false);
        CreateFxSprite(core, GetSoftCircleSprite(), new Color(0.2f, 0.90f, 1f, 0.95f), 72);
        var coreAnim = core.AddComponent<QuickScaleFadeAnimator>();
        coreAnim.Animate(0.95f, 0.22f, true);

        // 2. Expanding Warp Shockwave Ring
        GameObject ring = new GameObject("DepartRing");
        ring.transform.SetParent(root.transform, false);
        CreateFxSprite(ring, GetSoftCircleSprite(), new Color(0.15f, 0.65f, 1f, 0.80f), 70);
        var ringAnim = ring.AddComponent<QuickScaleFadeAnimator>();
        ringAnim.Animate(1.1f, 0.25f, false);

        // 3. 8 Vanishing Warp Sparkles
        int sparkCount = 8;
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = new GameObject($"DepartSpark_{i}");
            spark.transform.position = pos;
            Color sparkCol = Random.value > 0.4f ? new Color(0.4f, 0.95f, 1f, 1f) : new Color(1f, 0.90f, 0.4f, 1f);
            CreateFxSprite(spark, GetStarSparkleSprite(), sparkCol, 74);

            float rad = (i * (360f / sparkCount) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(2.5f, 5.0f);

            var anim = spark.AddComponent<ImpactParticleAnimator>();
            anim.Animate(dir, Random.Range(0.12f, 0.20f), Random.Range(0.20f, 0.35f));
        }

        // 4. Ground Dust Puff
        GameObject dust = new GameObject("DepartDust");
        dust.transform.position = pos;
        CreateFxSprite(dust, GetSoftCircleSprite(), new Color(0.85f, 0.85f, 0.85f, 0.45f), 65);
        var dustAnim = dust.AddComponent<GunSmokeAnimator>();
        dustAnim.Animate(Vector2.zero, 0.2f, 0.55f, 0.30f);

        Object.Destroy(root, 0.45f);
    }

    // ─── Room Teleport Arrival Effect (Radiant Materialization Burst) ────────

    public static void CreateTeleportArriveEffect(Vector3 pos)
    {
        GameObject root = new GameObject("TeleportArriveFX");
        root.transform.position = pos;

        // 1. Central Arrival Flash
        GameObject flash = new GameObject("ArriveFlash");
        flash.transform.SetParent(root.transform, false);
        CreateFxSprite(flash, GetSoftCircleSprite(), new Color(0.4f, 0.95f, 1f, 0.98f), 74);
        var flashAnim = flash.AddComponent<QuickScaleFadeAnimator>();
        flashAnim.Animate(0.85f, 0.16f, true);

        // 2. Expanding Teleport Shockwave Ring
        GameObject ring = new GameObject("ArriveRing");
        ring.transform.SetParent(root.transform, false);
        CreateFxSprite(ring, GetSoftCircleSprite(), new Color(0.2f, 0.80f, 1f, 0.85f), 72);
        var ringAnim = ring.AddComponent<QuickScaleFadeAnimator>();
        ringAnim.Animate(1.35f, 0.30f, false);

        // 3. 10 Radiant Warp Sparkles Radiating Outward
        int count = 10;
        for (int i = 0; i < count; i++)
        {
            GameObject spark = new GameObject($"ArriveSpark_{i}");
            spark.transform.position = pos;
            Color sparkCol = Random.value > 0.35f ? new Color(0.3f, 0.95f, 1f, 1f) : new Color(1f, 0.92f, 0.5f, 1f);
            CreateFxSprite(spark, GetStarSparkleSprite(), sparkCol, 75);

            float rad = (i * (360f / count) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(3.0f, 5.5f);

            var anim = spark.AddComponent<ImpactParticleAnimator>();
            anim.Animate(dir, Random.Range(0.14f, 0.22f), Random.Range(0.28f, 0.45f));
        }

        // 4. Ground Dust Shock Puff
        for (int i = 0; i < 2; i++)
        {
            GameObject dust = new GameObject($"ArriveDust_{i}");
            dust.transform.position = pos;
            CreateFxSprite(dust, GetSoftCircleSprite(), new Color(0.85f, 0.85f, 0.85f, 0.40f), 65);
            Vector2 drift = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.8f);
            var dustAnim = dust.AddComponent<GunSmokeAnimator>();
            dustAnim.Animate(drift, 0.15f, 0.48f, 0.35f);
        }

        Object.Destroy(root, 0.55f);
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
            sr.sortingLayerName = "player";
            sr.sortingOrder = 90;
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
                    sr.sortingLayerName = "player"; // Below wall layer
                    sr.sortingOrder = 85;
                    Material mat = ProceduralEffectsGenerator.GetUnlitMaterial();
                    if (mat != null) sr.sharedMaterial = mat;
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

// ─── Shell Casing Physics Animator ──────────────────────────────────────────

public class ShellCasingAnimator : MonoBehaviour
{
    public void Animate(Vector2 initialVel, float angularVel, float lifetime)
    {
        StartCoroutine(Routine(initialVel, angularVel, lifetime));
    }

    private System.Collections.IEnumerator Routine(Vector2 vel, float angularVel, float lifetime)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        float gravity = 11.5f;
        Vector2 curVel = vel;
        float rot = Random.Range(0f, 360f);

        float floorY = transform.position.y - Random.Range(0.20f, 0.45f);
        bool bounced = false;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float dt = Time.deltaTime;

            if (transform.position.y > floorY || !bounced)
            {
                curVel.y -= gravity * dt;
                transform.position += (Vector3)(curVel * dt);
                rot += angularVel * dt;
                transform.rotation = Quaternion.Euler(0, 0, rot);

                if (transform.position.y <= floorY && !bounced)
                {
                    bounced = true;
                    curVel = new Vector2(curVel.x * 0.45f, -curVel.y * 0.32f); // Micro ground bounce
                    angularVel *= 0.35f;
                }
            }
            else
            {
                curVel = Vector2.zero;
            }

            // Smooth fade out at end of life
            if (elapsed > lifetime - 0.8f && sr != null)
            {
                float t = (elapsed - (lifetime - 0.8f)) / 0.8f;
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

