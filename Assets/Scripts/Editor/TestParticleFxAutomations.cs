#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TestParticleFxAutomations
{
    // Can be run anytime via Unity menu: Tools -> Counter Boom -> Run Automated FX Tests
    [MenuItem("Tools/Counter Boom/Run Automated FX Tests", false, 1)]
    public static void ExecuteAutomatedFxTests()
    {
        Debug.Log("================ [AUTOMATED FX TEST SUITE START] ================");

        int passed = 0;
        int failed = 0;

        // Test 1: Shared Unlit Material
        Material mat = ProceduralEffectsGenerator.GetUnlitMaterial();
        if (mat != null && mat.shader != null && mat.renderQueue == (int)UnityEngine.Rendering.RenderQueue.Transparent)
        {
            Debug.Log($"[FX TEST PASS] Unlit Material initialized correctly. Shader: {mat.shader.name}, Queue: {mat.renderQueue}");
            passed++;
        }
        else
        {
            Debug.LogError($"[FX TEST FAIL] Unlit Material invalid or not transparent. Mat: {mat}, Shader: {mat?.shader?.name}");
            failed++;
        }

        // Test 2: Soft Circle Sprite
        Sprite circle = ProceduralEffectsGenerator.GetSoftCircleSprite();
        if (circle != null && circle.texture != null)
        {
            Texture2D tex = circle.texture;
            Color corner = tex.GetPixel(0, 0);
            Color center = tex.GetPixel(tex.width / 2, tex.height / 2);
            bool clampOk = tex.wrapMode == TextureWrapMode.Clamp;
            bool noBlackClear = corner.a == 0f && corner.r > 0.9f; // transparent white, not black!

            if (clampOk && noBlackClear && center.a > 0.9f)
            {
                Debug.Log($"[FX TEST PASS] SoftCircleSprite valid. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FX TEST FAIL] SoftCircleSprite issue. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                failed++;
            }
        }
        else
        {
            Debug.LogError("[FX TEST FAIL] SoftCircleSprite is null!");
            failed++;
        }

        // Test 3: Star Sparkle Sprite
        Sprite star = ProceduralEffectsGenerator.GetStarSparkleSprite();
        if (star != null && star.texture != null)
        {
            Texture2D tex = star.texture;
            Color corner = tex.GetPixel(0, 0);
            Color center = tex.GetPixel(tex.width / 2, tex.height / 2);
            bool clampOk = tex.wrapMode == TextureWrapMode.Clamp;
            bool noBlackClear = corner.a == 0f && corner.r > 0.9f;

            if (clampOk && noBlackClear && center.a > 0.9f)
            {
                Debug.Log($"[FX TEST PASS] StarSparkleSprite valid. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FX TEST FAIL] StarSparkleSprite issue. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                failed++;
            }
        }
        else
        {
            Debug.LogError("[FX TEST FAIL] StarSparkleSprite is null!");
            failed++;
        }

        // Test 4: Realistic Fire Sprite
        Sprite fire = ProceduralEffectsGenerator.GetRealisticFireSprite();
        if (fire != null && fire.texture != null)
        {
            Texture2D tex = fire.texture;
            Color corner = tex.GetPixel(0, 0);
            Color center = tex.GetPixel(tex.width / 2, tex.height / 2);
            bool clampOk = tex.wrapMode == TextureWrapMode.Clamp;
            bool zeroAlphaCorner = corner.a == 0f;

            if (clampOk && zeroAlphaCorner && center.a > 0.9f)
            {
                Debug.Log($"[FX TEST PASS] RealisticFireSprite valid. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FX TEST FAIL] RealisticFireSprite issue. Wrap: {tex.wrapMode}, Corner: {corner}, Center: {center}");
                failed++;
            }
        }
        else
        {
            Debug.LogError("[FX TEST FAIL] RealisticFireSprite is null!");
            failed++;
        }

        // Test 5: CreateFxSprite Sorting & Material
        GameObject dummyGO = new GameObject("DummyFxTest");
        try
        {
            var sr = ProceduralEffectsGenerator.CreateFxSprite(dummyGO, star, Color.yellow, 75);
            bool layerOk = sr.sortingLayerName == "player";
            bool matOk = sr.sharedMaterial != null && sr.sharedMaterial == mat;

            if (layerOk && matOk)
            {
                Debug.Log($"[FX TEST PASS] CreateFxSprite sortingLayer='{sr.sortingLayerName}', order={sr.sortingOrder}, material={sr.sharedMaterial.name}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FX TEST FAIL] CreateFxSprite layer: '{sr.sortingLayerName}', mat: {sr.sharedMaterial?.name}");
                failed++;
            }
        }
        finally
        {
            GameObject.DestroyImmediate(dummyGO);
        }

        // Test 6: PlayerMovementDust / MoveDust Setup
        PlayerDustSetupUtility.SetupPlayerLegDustParticles();
        Debug.Log("[FX TEST PASS] PlayerDustSetupUtility executed smoothly.");
        passed++;

        // Test 7: Shell Casing Sprite Scale & PPU
        Sprite casing = ProceduralEffectsGenerator.GetShellCasingSprite();
        if (casing != null && casing.pixelsPerUnit >= 90f)
        {
            Debug.Log($"[FX TEST PASS] Shell Casing Sprite valid. PPU: {casing.pixelsPerUnit}, Rect: {casing.rect}");
            passed++;
        }
        else
        {
            Debug.LogError($"[FX TEST FAIL] Shell Casing Sprite invalid. PPU: {casing?.pixelsPerUnit}");
            failed++;
        }

        // Test 8: HandheldWeapon Audio Initialization
        GameObject weaponTestGO = new GameObject("WeaponAudioTest");
        try
        {
            var hw = weaponTestGO.AddComponent<HandheldWeapon>();
            hw.PlayShootSound(Vector3.zero);
            hw.StopLoopingAudio();
            Debug.Log("[FX TEST PASS] HandheldWeapon audio methods executed smoothly.");
            passed++;
        }
        finally
        {
            GameObject.DestroyImmediate(weaponTestGO);
        }

        Debug.Log($"================ [AUTOMATED FX TEST SUITE FINISHED: {passed} PASSED, {failed} FAILED] ================");
    }
}
#endif
