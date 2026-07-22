using UnityEngine;
using Unity.Netcode;
using System;

public class PlayerHealth : NetworkBehaviour
{
    public static PlayerHealth Instance;

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    // Synced health over the network
    private readonly NetworkVariable<int> netHealth = new NetworkVariable<int>(
        100, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public event Action<int, int> OnHealthChanged; // current, max
    public event Action OnDeath;

    private void Awake()
    {
        netHealth.OnValueChanged += OnHealthValueChanged;
    }

    private void OnHealthValueChanged(int oldVal, int newVal)
    {
        OnHealthChanged?.Invoke(newVal, maxHealth);
        if (newVal <= 0)
        {
            Die();
        }
    }

    private void Start()
    {
        bool isLocal = false;
        if (IsSpawned)
        {
            if (IsOwner) isLocal = true;
        }
        else
        {
            isLocal = true; // Offline fallback
        }

        if (isLocal)
        {
            Instance = this;
        }

        if (IsServer)
        {
            netHealth.Value = maxHealth;
        }

        // Broadcast initial health
        OnHealthChanged?.Invoke(netHealth.Value, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (IsServer)
        {
            if (netHealth.Value <= 0) return;
            netHealth.Value = Mathf.Clamp(netHealth.Value - amount, 0, maxHealth);
        }
        else
        {
            TakeDamageServerRpc(amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        if (netHealth.Value <= 0) return;
        netHealth.Value = Mathf.Clamp(netHealth.Value - amount, 0, maxHealth);
    }

    public void Heal(int amount)
    {
        if (IsServer)
        {
            if (netHealth.Value <= 0) return;
            netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth);
        }
        else
        {
            HealServerRpc(amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealServerRpc(int amount)
    {
        if (netHealth.Value <= 0) return;
        netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth);
    }

    public bool IsDead { get; private set; } = false;

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log($"[PlayerHealth] Player '{gameObject.name}' died!");
        OnDeath?.Invoke();

        // 1. Disable colliders
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        // 2. Disable controls & physics
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;

        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null) playerCtrl.enabled = false;

        var playerAim = GetComponent<PlayerAiming>();
        if (playerAim != null) playerAim.enabled = false;

        var weaponCtrl = GetComponent<WeaponController>();
        if (weaponCtrl != null) weaponCtrl.enabled = false;

        // 3. Play procedural code death animation
        StartCoroutine(PlayDeathAnimationRoutine());
    }

    private System.Collections.IEnumerator PlayDeathAnimationRoutine()
    {
        float elapsed = 0f;
        float duration = 1.2f;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(0, 0, 90f); // Fall flat sideways

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Tilt & pop slightly upward/backward
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t * 2f);
            transform.position = startPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.25f, 0f);

            // Fade renderers
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    Color c = r.color;
                    c.a = Mathf.Lerp(1f, 0.2f, t);
                    r.color = c;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRot;

        // If local player, start Spectator Mode
        bool isLocal = false;
        if (IsSpawned) { if (IsOwner) isLocal = true; }
        else { isLocal = true; }

        if (isLocal)
        {
            Debug.Log("[PlayerHealth] Local player died. Transitioning to Spectator Mode...");
            if (CameraController.Instance != null)
            {
                CameraController.Instance.StartSpectating();
            }
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.EnableSpectatorUI(true);
            }
        }
    }

    public int GetCurrentHealth() => IsSpawned ? netHealth.Value : maxHealth;
    public int GetMaxHealth() => maxHealth;
}
