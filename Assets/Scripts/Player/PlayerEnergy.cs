using UnityEngine;
using System;

public class PlayerEnergy : MonoBehaviour
{
    public static PlayerEnergy Instance { get; private set; }

    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float currentEnergy = 100f;
    [SerializeField] private float regenRate = 5f; // Energy regenerated per second

    public event Action<float, float> OnEnergyChanged; // current, max

    private bool isLocal = false;

    private void Awake()
    {
        currentEnergy = maxEnergy;
    }

    private void Start()
    {
        EvaluateIsLocal();
        if (isLocal)
        {
            Instance = this;
        }

        // Broadcast initial energy state
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    private void EvaluateIsLocal()
    {
        var netObj = GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            isLocal = netObj.IsLocalPlayer || netObj.IsOwner;
        }
        else
        {
            isLocal = true; // Offline / local fallback
        }

        if (isLocal)
        {
            Instance = this;
        }
    }

    private void Update()
    {
        // Regenerate energy over time
        if (currentEnergy < maxEnergy)
        {
            currentEnergy = Mathf.Min(currentEnergy + regenRate * Time.deltaTime, maxEnergy);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }

    public bool UseEnergy(float amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy = Mathf.Clamp(currentEnergy - amount, 0f, maxEnergy);
            OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
            return true;
        }
        return false;
    }

    public void RestoreEnergy(float amount)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    public float GetCurrentEnergy() => currentEnergy;
    public float GetMaxEnergy() => maxEnergy;
}
