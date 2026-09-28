using UnityEngine;
using System.Collections;

/// <summary>
/// Adds an ambient, subtle twinkling shimmer to weapons, medkits, and objective keys lying on the floor.
/// Makes floor loot intuitively noticeable to players without needing clunky oversized UI arrows.
/// </summary>
public class AmbientLootShimmer : MonoBehaviour
{
    [SerializeField] private Color shimmerColor = new Color(1f, 0.95f, 0.65f, 0.95f);
    [SerializeField] private float minInterval = 0.8f;
    [SerializeField] private float maxInterval = 1.6f;

    private Coroutine shimmerRoutine;

    private void Start()
    {
        // Auto-adapt color if attached to SafeKeyItemPickup
        if (GetComponent<SafeKeyItemPickup>() != null)
        {
            shimmerColor = new Color(1f, 0.85f, 0.2f, 1f);
        }

        shimmerRoutine = StartCoroutine(TwinkleLoop());
    }

    private void OnDisable()
    {
        if (shimmerRoutine != null)
        {
            StopCoroutine(shimmerRoutine);
            shimmerRoutine = null;
        }
    }

    private IEnumerator TwinkleLoop()
    {
        // Brief initial delay so floor items twinkle staggered
        yield return new WaitForSeconds(Random.Range(0.1f, 0.6f));

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (!gameObject.activeInHierarchy) yield break;

            SpawnSingleTwinkleGlint();
        }
    }

    private void SpawnSingleTwinkleGlint()
    {
        var parentSr = GetComponent<SpriteRenderer>();
        if (parentSr != null && !parentSr.enabled) return;

        Vector3 glintPos = transform.position + (Vector3)(Random.insideUnitCircle * 0.22f);

        GameObject glint = new GameObject("AmbientLootGlint");
        glint.transform.position = glintPos;
        glint.transform.localScale = Vector3.zero;

        ProceduralEffectsGenerator.CreateFxSprite(glint, ProceduralEffectsGenerator.GetStarSparkleSprite(), shimmerColor, 45);

        var anim = glint.AddComponent<QuickScaleFadeAnimator>();
        anim.Animate(Random.Range(0.25f, 0.40f), Random.Range(0.35f, 0.55f), true);
    }
}
