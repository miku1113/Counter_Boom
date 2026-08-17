using UnityEngine;
using Unity.Netcode;
using TMPro;

public class SafeKeyItemPickup : NetworkBehaviour
{
    [SerializeField] private float bobbingSpeed = 2.5f;
    [SerializeField] private float bobbingHeight = 0.15f;
    [SerializeField] private Color keyGlowColor = new Color(1f, 0.2f, 0.15f, 1f); // Vibrant Red-Gold Glow

    private Vector3 initialPos;
    private SpriteRenderer spriteRenderer;
    private bool isCollected = false;

    private void Start()
    {
        initialPos = transform.position;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
        circle.radius = 0.85f;
        circle.isTrigger = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateProceduralSafeKeySprite();
        spriteRenderer.color = keyGlowColor;
        spriteRenderer.sortingOrder = 145;

        EnsureLabel();
    }

    private Sprite CreateProceduralSafeKeySprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color crimson = new Color(1f, 0.15f, 0.1f, 1f);
        Color gold = new Color(1f, 0.85f, 0.2f, 1f);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        Vector2 ringCenter = new Vector2(10f, 21f);
        float outerR = 7f;
        float innerR = 3.5f;

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), ringCenter);
                if (dist <= outerR && dist >= innerR)
                {
                    tex.SetPixel(x, y, (dist > outerR - 1f) ? crimson : gold);
                }
            }
        }

        for (int i = 0; i < 18; i++)
        {
            int px = 14 + i;
            int py = 17 - i;
            if (px >= 0 && px < 32 && py >= 0 && py < 32)
            {
                tex.SetPixel(px, py, gold);
                tex.SetPixel(px + 1, py, gold);
                tex.SetPixel(px, py + 1, gold);
            }
        }

        for (int t = 0; t < 5; t++)
        {
            int tx = 25 - t;
            int ty = 6 + t;
            if (tx >= 0 && tx < 32 && ty >= 0 && ty < 32)
            {
                tex.SetPixel(tx, ty, crimson);
                tex.SetPixel(tx + 1, ty - 1, crimson);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
    }

    private void EnsureLabel()
    {
        Transform labelTrans = transform.Find("SafeKeyLabel");
        if (labelTrans == null)
        {
            GameObject txtGO = new GameObject("SafeKeyLabel");
            txtGO.transform.SetParent(transform, false);
            txtGO.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            TextMeshPro tmp = txtGO.AddComponent<TextMeshPro>();
            tmp.text = "🔑 SAFE KEY";
            tmp.fontSize = 2.0f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = keyGlowColor;
            tmp.sortingOrder = 150;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        // Visibility: Safe Key is visible ONLY to Thief players on their screen!
        UpdateVisibilityForLocalPlayer();

        float newY = initialPos.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
        transform.position = new Vector3(initialPos.x, newY, initialPos.z);

        CheckPlayerProximity();
    }

    private void UpdateVisibilityForLocalPlayer()
    {
        PlayerController localPlayer = null;
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && (p.IsLocal || p.IsOwner))
            {
                localPlayer = p;
                break;
            }
        }

        // Visible ONLY to Thief players!
        bool shouldBeVisible = false;
        if (localPlayer != null && localPlayer.playerRole.Value == PlayerRole.Thief)
        {
            shouldBeVisible = true;
        }

        if (spriteRenderer != null && spriteRenderer.enabled != shouldBeVisible)
        {
            spriteRenderer.enabled = shouldBeVisible;
        }

        Transform labelTrans = transform.Find("SafeKeyLabel");
        if (labelTrans != null && labelTrans.gameObject.activeSelf != shouldBeVisible)
        {
            labelTrans.gameObject.SetActive(shouldBeVisible);
        }
    }

    private void CheckPlayerProximity()
    {
        if (isCollected) return;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            // Only Thief players can collect the Safe Key!
            if (p != null && p.playerRole.Value == PlayerRole.Thief)
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= 1.8f)
                {
                    CollectSafeKey(p);
                    break;
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void TryPickup(Collider2D other)
    {
        if (isCollected) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) player = other.GetComponent<PlayerController>();

        // Only Thief players can collect the Safe Key!
        if (player != null && player.playerRole.Value == PlayerRole.Thief)
        {
            CollectSafeKey(player);
        }
    }

    private void CollectSafeKey(PlayerController player)
    {
        if (isCollected) return;
        isCollected = true;

        Debug.Log($"[SafeKeyItemPickup] Safe Key collected by Thief '{player.playerName.Value}'!");

        if (MatchRoleManager.Instance != null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
            {
                MatchRoleManager.Instance.SafeKeyCollectedByThief.Value = true;
            }
            else
            {
                MatchRoleManager.Instance.CollectSafeKeyServerRpc();
            }
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification("<color=gold>🔑 SAFE KEY COLLECTED! Locate and unlock the SAFE (\"seaf\") to steal the Treasure!</color>");
        }

        Destroy(gameObject);
    }
}
