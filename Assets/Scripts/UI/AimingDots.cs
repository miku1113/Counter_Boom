using UnityEngine;

public class AimingDots : MonoBehaviour
{
    [Header("Dot Settings")]
    [SerializeField] private GameObject dotPrefab;
    [SerializeField] private int numberOfDots = 5;
    [SerializeField] private float spacing = 0.3f;
    [SerializeField] private Color dotColor = Color.red;
    
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerAiming playerAiming;
    
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
    
    private void Update()
    {
        if (playerAiming == null || player == null) return;
        
        Vector2 aimDirection = playerAiming.GetAimDirection();
        
        // Show dots only when aiming
        bool showDots = aimDirection.magnitude > 0.1f;
        
        if (showDots)
        {
            UpdateDotPositions(aimDirection);
        }
        
        foreach (var dot in dots)
        {
            if (dot != null)
            {
                dot.SetActive(showDots);
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
