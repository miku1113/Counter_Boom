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

    private void Die()
    {
        Debug.Log("Player died!");
        OnDeath?.Invoke();
    }

    public int GetCurrentHealth() => IsSpawned ? netHealth.Value : maxHealth;
    public int GetMaxHealth() => maxHealth;
}
