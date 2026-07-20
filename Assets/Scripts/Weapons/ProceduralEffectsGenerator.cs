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

    public static void CreateStunBlast(Vector3 position, float radius)
    {
        GameObject blastObj = new GameObject("ProceduralStunBlast");
        blastObj.transform.position = position;

        SpriteRenderer sr = blastObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftCircleSprite();
        sr.color = new Color(0.9f, 0.95f, 1f, 0.8f); // Bright blue-white flash
        sr.sortingOrder = 5;

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

public class SmokePuffAnimator : MonoBehaviour
{
    public void Animate(float blastRadius, float lifetime)
    {
        StartCoroutine(AnimateRoutine(blastRadius, lifetime));
    }

    private System.Collections.IEnumerator AnimateRoutine(float blastRadius, float lifetime)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        
        // Force the smoke puffs to render on top of characters
        if (renderers != null)
        {
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    sr.sortingLayerName = "Default";
                    sr.sortingOrder = 15;
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
