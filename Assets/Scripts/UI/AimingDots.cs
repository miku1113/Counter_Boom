using UnityEngine;

public class AimingDots : MonoBehaviour
{
    public static AimingDots Instance { get; private set; }

    [Header("Dot Settings")]
    [SerializeField] private GameObject dotPrefab;
    [SerializeField] private int numberOfDots = 5;
    [SerializeField] private float spacing = 0.3f;
    [SerializeField] private Color dotColor = Color.red;
    
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerAiming playerAiming;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Links dynamically spawned local player transforms and aiming scripts.
    /// </summary>
    public void SetLocalPlayer(Transform playerTransform, PlayerAiming aiming)
    {
        player = playerTransform;
        playerAiming = aiming;
        Debug.Log("[AimingDots] Local player reference registered successfully.");
    }

    
    private GameObject[] dots;
    
    private void Start()
    {
        CreateDots();
    }
    
    private void CreateDots()
    {
        dots = new GameObject[numberOfDots];
        
        for (int i = 0; i < numberOfDots; i++)
        {
            if (dotPrefab != null)
            {
                dots[i] = Instantiate(dotPrefab, transform);
            }
            else
            {
                // Create simple sprite dots if no prefab
                dots[i] = new GameObject($"Dot_{i}");
                dots[i].transform.SetParent(transform);
                
                SpriteRenderer sr = dots[i].AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = dotColor;
                
                dots[i].transform.localScale = Vector3.one * 0.1f;
            }
            
            dots[i].SetActive(false);
        }
    }
    
    public void HideDots()
    {
        if (dots == null) return;
        foreach (var dot in dots)
        {
            if (dot != null) dot.SetActive(false);
        }
    }

    private void Update()
    {
        // If local player is dead or aiming is disabled, hide all dots
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
        {
            HideDots();
            return;
        }

        // Auto-find local player if reference is lost or dynamically spawned late
        if (player == null || playerAiming == null)
        {
            FindLocalPlayer();
        }

        if (playerAiming == null || !playerAiming.enabled || player == null)
        {
            HideDots();
            return;
        }
        
        Vector2 aimDirection = playerAiming.GetAimDirection();
        
        UpdateDotPositions(aimDirection);
        
        foreach (var dot in dots)
        {
            if (dot != null)
            {
                dot.SetActive(true);
            }
        }
    }

    private void FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p != null && p.IsLocal)
            {
                SetLocalPlayer(p.transform, p.GetComponent<PlayerAiming>());
                break;
            }
        }
    }

    
    private void UpdateDotPositions(Vector2 direction)
    {
        // Get starting position from weapon (if available)
        Vector3 startPos = player.position;
        if (playerAiming != null)
        {
            startPos = playerAiming.GetAimStartPosition();
        }
        
        for (int i = 0; i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            
            float distance = (i + 1) * spacing;
            Vector3 dotPos = startPos + (Vector3)(direction * distance);
            dots[i].transform.position = dotPos;
        }
    }
    
    /// <summary>
    /// Creates a simple circle sprite for dots
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance < radius ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
