using UnityEngine;
using Unity.Netcode;
using TMPro;

public class KeyItemPickup : NetworkBehaviour
{
    [Header("Key Info")]
    public int keyIndex = 1;
    [SerializeField] private float bobbingSpeed = 2.5f;
    [SerializeField] private float bobbingHeight = 0.15f;
    [SerializeField] private Color keyGlowColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Sprite Settings")]
    [Tooltip("Drag and drop any Key Sprite here in Inspector!")]
    public Sprite customKeySprite;

    private Vector3 initialPos;
    private SpriteRenderer spriteRenderer;
    private bool isCollected = false;

    private void Start()
    {
        initialPos = transform.position;

        // Ensure Kinematic Rigidbody2D so 2D trigger events fire reliably
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // Ensure CircleCollider2D trigger
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null) circle = gameObject.AddComponent<CircleCollider2D>();
        circle.radius = 0.85f;
        circle.isTrigger = true;

        // Setup SpriteRenderer & Sprite
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (customKeySprite != null)
        {
            spriteRenderer.sprite = customKeySprite;
        }
        else
        {
            // Generate procedural crisp golden key sprite if no custom asset is provided
            spriteRenderer.sprite = CreateProceduralKeySprite();
        }

        spriteRenderer.color = keyGlowColor;
        spriteRenderer.sortingOrder = 140;

        EnsureLabel();
    }

    private Sprite CreateProceduralKeySprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color gold = new Color(1f, 0.85f, 0.1f, 1f);
        Color darkGold = new Color(0.8f, 0.6f, 0.05f, 1f);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        // Draw Key Handle Ring
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
                    tex.SetPixel(x, y, (dist > outerR - 1f) ? darkGold : gold);
                }
            }
        }

        // Draw Key Shaft (diagonal)
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

        // Draw Key Teeth
        for (int t = 0; t < 5; t++)
        {
            int tx = 25 - t;
            int ty = 6 + t;
            if (tx >= 0 && tx < 32 && ty >= 0 && ty < 32)
            {
                tex.SetPixel(tx, ty, darkGold);
                tex.SetPixel(tx + 1, ty - 1, darkGold);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
    }

    private void EnsureLabel()
    {
        Transform labelTrans = transform.Find("KeyLabel");
        if (labelTrans == null)
        {
            GameObject txtGO = new GameObject("KeyLabel");
            txtGO.transform.SetParent(transform, false);
            txtGO.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            TextMeshPro tmp = txtGO.AddComponent<TextMeshPro>();
            tmp.text = $"🔑 KEY #{keyIndex}";
            tmp.fontSize = 1.8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = keyGlowColor;
            tmp.sortingOrder = 150;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        // Hide key sprite & floating label on local client screen if player is a Thief
        UpdateVisibilityForLocalPlayer();

        // Vertical floating animation
        float newY = initialPos.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
        transform.position = new Vector3(initialPos.x, newY, initialPos.z);

        // Distance proximity check to 100% guarantee pickup when player is near
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

        bool shouldBeVisible = true;
        if (localPlayer != null && localPlayer.playerRole.Value == PlayerRole.Thief)
        {
            // Thief players can see Exit Keys ONLY AFTER stealing the Treasure!
            bool treasureStolen = MatchRoleManager.Instance != null && MatchRoleManager.Instance.TreasureStolen.Value;
            shouldBeVisible = treasureStolen;
        }

        if (spriteRenderer != null && spriteRenderer.enabled != shouldBeVisible)
        {
            spriteRenderer.enabled = shouldBeVisible;
        }

        Transform labelTrans = transform.Find("KeyLabel");
        if (labelTrans != null && labelTrans.gameObject.activeSelf != shouldBeVisible)
        {
            labelTrans.gameObject.SetActive(shouldBeVisible);
        }
    }

    private bool CanPlayerPickupKey(PlayerController player)
    {
        if (player == null) return false;
        if (player.playerRole.Value == PlayerRole.Hostage) return true;

        // Thief can pick up Exit Keys ONLY AFTER stealing the Treasure!
        bool treasureStolen = MatchRoleManager.Instance != null && MatchRoleManager.Instance.TreasureStolen.Value;
        return player.playerRole.Value == PlayerRole.Thief && treasureStolen;
    }

    private void CheckPlayerProximity()
    {
        if (isCollected) return;

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (CanPlayerPickupKey(p))
            {
                float dist = Vector3.Distance(transform.position, p.transform.position);
                if (dist <= 1.8f)
                {
                    CollectKey(p);
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

        if (CanPlayerPickupKey(player))
        {
            CollectKey(player);
        }
    }

    private void CollectKey(PlayerController player)
    {
        if (isCollected) return;
        isCollected = true;
        Debug.Log($"[KeyItemPickup] Key #{keyIndex} collected by player '{player.playerName.Value}'!");

        if (MatchRoleManager.Instance != null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
            {
                if (MatchRoleManager.Instance.KeysCollected.Value < 2)
                {
                    MatchRoleManager.Instance.KeysCollected.Value++;
                }
            }
            else
            {
                MatchRoleManager.Instance.CollectKeyServerRpc();
            }
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowNotification($"🔑 KEY #{keyIndex} COLLECTED! Bring keys to Main Gate!");
        }

        // Network Despawn or Disable
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
                return;
            }
        }

        gameObject.SetActive(false);
    }
}
