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

    private int currentLocalHealth = 100;

    private void Awake()
    {
        currentLocalHealth = maxHealth;
        netHealth.OnValueChanged += OnHealthValueChanged;
    }

    private void OnHealthValueChanged(int oldVal, int newVal)
    {
        currentLocalHealth = newVal;
        OnHealthChanged?.Invoke(newVal, maxHealth);
        if (newVal <= 0)
        {
            Die();
        }
    }

    private void Start()
    {
        EvaluateIsLocal();

        if (!RelayNetworkManager.IsMigrating)
        {
            IsDead = false;
            currentLocalHealth = maxHealth;
            if (IsServer)
            {
                netHealth.Value = maxHealth;
            }
        }

        // Broadcast initial health
        OnHealthChanged?.Invoke(GetCurrentHealth(), maxHealth);
    }

    private void EvaluateIsLocal()
    {
        if (CompareTag("Bot") || GetComponent<AiBotController>() != null || gameObject.name.ToLower().Contains("bot"))
        {
            return; // Do not override human player instance
        }

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
    }

    public void RestoreHealthFromSnapshot(int targetHp)
    {
        currentLocalHealth = Mathf.Clamp(targetHp, 1, maxHealth);
        if (IsServer && IsSpawned)
        {
            netHealth.Value = currentLocalHealth;
        }
        OnHealthChanged?.Invoke(currentLocalHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        // Invincibility check: Ignore all damage in lobby scenes or non-gameplay scenes!
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "GameScene" && activeScene != "OfflineMode") return;

        if (IsSpawned)
        {
            if (IsServer)
            {
                if (netHealth.Value <= 0) return;
                netHealth.Value = Mathf.Clamp(netHealth.Value - amount, 0, maxHealth);
                currentLocalHealth = netHealth.Value;
            }
            else
            {
                TakeDamageServerRpc(amount);
            }
        }
        else
        {
            if (currentLocalHealth <= 0) return;
            currentLocalHealth = Mathf.Clamp(currentLocalHealth - amount, 0, maxHealth);
            OnHealthChanged?.Invoke(currentLocalHealth, maxHealth);
            if (currentLocalHealth <= 0)
            {
                Die();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "GameScene") return;

        if (netHealth.Value <= 0) return;
        netHealth.Value = Mathf.Clamp(netHealth.Value - amount, 0, maxHealth);
        currentLocalHealth = netHealth.Value;
    }

    public void Heal(int amount)
    {
        if (IsSpawned)
        {
            if (IsServer)
            {
                if (netHealth.Value <= 0) return;
                netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth);
                currentLocalHealth = netHealth.Value;
            }
            else
            {
                HealServerRpc(amount);
            }
        }
        else
        {
            if (currentLocalHealth <= 0) return;
            currentLocalHealth = Mathf.Clamp(currentLocalHealth + amount, 0, maxHealth);
            OnHealthChanged?.Invoke(currentLocalHealth, maxHealth);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealServerRpc(int amount)
    {
        if (netHealth.Value <= 0) return;
        netHealth.Value = Mathf.Clamp(netHealth.Value + amount, 0, maxHealth);
        currentLocalHealth = netHealth.Value;
    }

    public bool IsDead { get; private set; } = false;

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log($"[PlayerHealth] Player '{gameObject.name}' died! Starting death animation...");
        OnDeath?.Invoke();

        // Drop Safe Key when the designated Safe Key Holder Hostage dies (or when Bot dies in offline mode)
        bool isBot = CompareTag("Bot") || GetComponent<AiBotController>() != null || gameObject.name.ToLower().Contains("bot");
        if (isBot)
        {
            if (MatchRoleManager.Instance != null)
            {
                MatchRoleManager.Instance.DropSafeKey(transform.position);
            }
            else
            {
                GameObject safeKeyGO = new GameObject("Dropped_SafeKey", typeof(SafeKeyItemPickup));
                safeKeyGO.transform.position = transform.position;
            }
            
            Debug.Log($"[PlayerHealth] 🔑 Bot '{gameObject.name}' dropped Safe Key at {transform.position}!");
        }
        else if (MatchRoleManager.Instance != null)
        {
            var pc = GetComponent<PlayerController>();
            bool isHostage = (pc != null && pc.playerRole.Value == PlayerRole.Hostage);
            bool isSafeKeyHolder = MatchRoleManager.Instance.IsSafeKeyHolder(OwnerClientId);

            // If this player is the designated Safe Key holder hostage (or if there's only 1 hostage and holder was unassigned)
            if (isSafeKeyHolder || (isHostage && MatchRoleManager.Instance.SafeKeyHolderClientId.Value == 999999))
            {
                MatchRoleManager.Instance.DropSafeKey(transform.position);
            }
            else if (MatchRoleManager.Instance.IsGateKeyHolder(OwnerClientId))
            {
                MatchRoleManager.Instance.HandleGateKeyHolderDeath(transform.position);
            }

            MatchRoleManager.Instance.OnPlayerDied();
        }

        // Drop all equipped weapons on the ground for other players to pick up!
        var bagManager = GetComponent<BagManager>();
        if (bagManager == null) bagManager = GetComponentInChildren<BagManager>();
        if (bagManager != null)
        {
            bagManager.DropAllWeaponsOnDeath();
        }
        else
        {
            var wc = GetComponent<WeaponController>();
            if (wc == null) wc = GetComponentInChildren<WeaponController>();
            if (wc != null && wc.weaponSlots != null)
            {
                for (int i = 0; i < wc.weaponSlots.Length; i++)
                {
                    var weapon = wc.weaponSlots[i];
                    if (weapon != null && weapon.itemData != null && weapon.itemData.prefab != null)
                    {
                        Vector3 dropPos = transform.position + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.6f, 0.6f), 0f);
                        GameObject pickupObj = Instantiate(weapon.itemData.prefab, dropPos, Quaternion.identity);
                        var pickup = pickupObj.GetComponent<ItemPickup>();
                        if (pickup == null) pickup = pickupObj.AddComponent<ItemPickup>();
                        pickup.itemData = weapon.itemData;
                        pickup.amount = 1;
                        pickup.wasDropped = true;

                        var netObj = pickupObj.GetComponent<Unity.Netcode.NetworkObject>();
                        if (netObj != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                        {
                            netObj.Spawn(true);
                        }
                    }
                }
            }
        }

        // 1. Disable combat components (aiming & weapons)
        var playerAim = GetComponent<PlayerAiming>();
        if (playerAim != null) playerAim.enabled = false;

        var weaponCtrl = GetComponent<WeaponController>();
        if (weaponCtrl != null) weaponCtrl.enabled = false;

        // 2. Hide handheld weapons
        HandheldWeapon[] weapons = GetComponentsInChildren<HandheldWeapon>(true);
        foreach (var w in weapons)
        {
            if (w != null) w.gameObject.SetActive(false);
        }

        // 3. Disable Animator component(s)
        Animator[] animators = GetComponentsInChildren<Animator>();
        foreach (var anim in animators)
        {
            if (anim != null) anim.enabled = false;
        }

        bool isLocalPlayer = false;
        if (CompareTag("Bot") || GetComponent<AiBotController>() != null || gameObject.name.ToLower().Contains("bot"))
        {
            isLocalPlayer = false;
        }
        else if (IsSpawned)
        {
            if (IsOwner) isLocalPlayer = true;
        }
        else
        {
            isLocalPlayer = true;
        }

        // 4. Hide Aiming Dots ONLY if local player died
        if (isLocalPlayer && AimingDots.Instance != null)
        {
            AimingDots.Instance.HideDots();
        }

        // 5. Explode body parts immediately into physics gibs!
        PlayerBodyExploder.ExplodePlayer(transform);

        // 6. Enable Ghost Mode on PlayerController (hides old body parts, activates floating ghost sprite)
        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null)
        {
            playerCtrl.enabled = true;
            playerCtrl.EnableGhostMode();
        }

        // 7. Update UI controls ONLY if the local player died (switch to Ghost mode UI with only move control)
        if (isLocalPlayer)
        {
            if (MobileInputManager.Instance != null)
            {
                MobileInputManager.Instance.SetGhostUI(true);
            }
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetGhostUI(true);
            }
        }
    }

    public int GetCurrentHealth() => IsSpawned ? netHealth.Value : currentLocalHealth;
    public int GetMaxHealth() => maxHealth;
}
