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

        if (IsServer)
        {
            netHealth.Value = maxHealth;
            currentLocalHealth = maxHealth;
        }

        // Broadcast initial health
        OnHealthChanged?.Invoke(GetCurrentHealth(), maxHealth);
    }

    private void EvaluateIsLocal()
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
        if (activeScene != "GameScene") return;

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
        if (IsSpawned)
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

        // 5. Play death animation routine (character falls flat on floor, then spawns corpse and activates Ghost Mode)
        StartCoroutine(PlayDeathAnimationRoutine());
    }

    private void SpawnDeadCorpse()
    {
        GameObject corpse = new GameObject($"{gameObject.name}_Corpse");
        corpse.transform.position = transform.position;
        corpse.transform.rotation = Quaternion.Euler(0, 0, 90f); // Fixed flat position on floor

        // Find character visual root or clone character hierarchy
        Transform visualRoot = transform.Find("Visuals");
        if (visualRoot == null) visualRoot = transform.Find("Character");
        if (visualRoot == null) visualRoot = transform;

        // ── Snapshot the dead player's actual sprites BEFORE cloning ──────────
        // CharacterAssembler.Start() runs after Instantiate and overwrites sprites
        // with the LOCAL viewer's PlayerPrefs skin index. We capture the real skin
        // first so we can re-stamp it onto the corpse immediately after cloning.
        var spriteSnapshot = new System.Collections.Generic.Dictionary<string, Sprite>();
        foreach (var r in visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r != null && r.sprite != null)
                spriteSnapshot[r.gameObject.name] = r.sprite;
        }
        // ──────────────────────────────────────────────────────────────────────

        GameObject corpseVisuals = Instantiate(visualRoot.gameObject, corpse.transform);
        corpseVisuals.transform.localPosition = Vector3.zero;
        corpseVisuals.transform.localRotation = Quaternion.identity;

        // Reset all arm and hand rotations on corpse to straight
        Transform[] allTransforms = corpseVisuals.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name.ToLower();
            if (n.Contains("arm") || n.Contains("hand") || n.Contains("pivot") || n.Contains("weapon"))
            {
                t.localRotation = Quaternion.identity;
            }
        }

        // Strip runtime scripts and colliders from corpse
        MonoBehaviour[] scripts = corpseVisuals.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts) Destroy(s);

        Collider2D[] colliders = corpseVisuals.GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders) Destroy(col);

        Animator[] animators = corpseVisuals.GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators) Destroy(anim);

        // Process corpse renderers: re-stamp correct skin, fade opacity, hide eyes
        SpriteRenderer[] renderers = corpseVisuals.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;

            string rName = r.gameObject.name.ToLower();

            // Hide GhostVisual if present on visual root clone
            if (rName.Contains("ghostvisual"))
            {
                r.gameObject.SetActive(false);
                continue;
            }

            // Disable eyes completely on dead body
            if (rName.Contains("eye"))
            {
                r.enabled = false;
                r.gameObject.SetActive(false);
                continue;
            }

            // ── Re-stamp the dead player's actual sprite ───────────────────────
            // Overrides whatever CharacterAssembler.Start() may have loaded
            // using the local viewer's PlayerPrefs skin index.
            if (spriteSnapshot.TryGetValue(r.gameObject.name, out Sprite originalSprite))
            {
                r.sprite = originalSprite;
            }
            // ──────────────────────────────────────────────────────────────────

            r.enabled = true;

            // Decrease opacity for dead body (faded dead appearance)
            Color c = r.color;
            c.a = 0.6f;
            r.color = c;

            r.sortingOrder = r.sortingOrder - 5;
        }

        Debug.Log($"[PlayerHealth] Corpse created for '{gameObject.name}' at {transform.position} with original skin preserved.");
    }


    private System.Collections.IEnumerator PlayDeathAnimationRoutine()
    {
        float elapsed = 0f;
        float duration = 1.0f;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(0, 0, 90f); // Fall flat sideways onto floor

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // Tilt & pop slightly upward/backward flat onto floor
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t * 2f);
            transform.position = startPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.15f, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRot;

        // 1. Spawn intact flat corpse on floor
        SpawnDeadCorpse();

        // 2. Reset player rotation to upright
        transform.rotation = Quaternion.identity;

        // 3. Enable Ghost Mode on PlayerController (hides old body parts, activates floating ghost sprite, -20% speed)
        var playerCtrl = GetComponent<PlayerController>();
        if (playerCtrl != null)
        {
            playerCtrl.enabled = true;
            playerCtrl.EnableGhostMode();
        }

        bool isLocalPlayer = false;
        if (IsSpawned)
        {
            if (IsOwner) isLocalPlayer = true;
        }
        else
        {
            isLocalPlayer = true;
        }

        // 4. Update UI controls ONLY if the local player died: Disable Aim Joystick & combat buttons, KEEP Move Joystick active!
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
