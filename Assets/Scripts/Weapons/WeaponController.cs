using UnityEngine;
using Unity.Netcode;

public class WeaponController : NetworkBehaviour
{
    public static WeaponController Instance;

    [Header("Setup")]
    [SerializeField] private GameObject startingWeaponPrefab;
    [SerializeField] private Transform  weaponAttachPoint;

    [Header("References")]
    [SerializeField] private PlayerAiming       playerAiming;
    [SerializeField] private CharacterAssembler characterAssembler;

    [Header("Grenade")]
    [SerializeField] private GameObject grenadePrefab;

    // ─── Runtime state ───────────────────────────────────────────────────────
    private HandheldWeapon[] weaponSlots = new HandheldWeapon[2];
    private int currentSlot = 0;

    private HandheldWeapon CurrentWeapon => weaponSlots[currentSlot];

    // ─── Events ──────────────────────────────────────────────────────────────
    public System.Action<int, int> OnAmmoChanged;
    public System.Action           OnWeaponFired;
    public System.Action           OnReloadStart;
    public System.Action           OnReloadComplete;
    /// <summary>Fires whenever a slot gains or loses a weapon (icon/ammo should refresh).</summary>
    public System.Action<int>      OnWeaponSlotUpdated;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (weaponAttachPoint == null)
            Debug.LogError("[WeaponController] ⚠️ weaponAttachPoint is NOT assigned! " +
                           "Please assign it in the Inspector. Weapons cannot be equipped without it.");
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

        // Auto-find PlayerAiming if not wired in Inspector
        if (playerAiming == null)
            playerAiming = GetComponent<PlayerAiming>();

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower();
        bool isLobbyScene = activeScene.Contains("lobby");

        if (isLobbyScene)
        {
            ClearAttachPointChildren();
        }
        else if (startingWeaponPrefab != null)
        {
            EquipWeaponToSlot(0, startingWeaponPrefab);
        }
    }

    /// <summary>
    /// Destroys any pre-attached default gun objects saved inside the Player prefab.
    /// </summary>
    public void ClearAttachPointChildren()
    {
        if (weaponAttachPoint != null)
        {
            for (int i = weaponAttachPoint.childCount - 1; i >= 0; i--)
            {
                Destroy(weaponAttachPoint.GetChild(i).gameObject);
            }
        }
        weaponSlots[0] = null;
        weaponSlots[1] = null;
        if (playerAiming == null) playerAiming = GetComponent<PlayerAiming>();
        playerAiming?.SetWeapon(null);
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public HandheldWeapon GetWeaponInSlot(int slot)
    {
        if (slot < 0 || slot >= weaponSlots.Length) return null;
        return weaponSlots[slot];
    }

    public int GetCurrentSlot() => currentSlot;

    /// <summary>
    /// Instantiates weaponPrefab into the given slot.
    /// If slotIndex == currentSlot the weapon is immediately shown and PlayerAiming is notified.
    /// </summary>
    public void EquipWeaponToSlot(int slotIndex, GameObject weaponPrefab)
    {
        if (weaponPrefab == null || slotIndex < 0 || slotIndex >= 2) return;

        EquipWeaponToSlotInternal(slotIndex, weaponPrefab);

        if (IsSpawned && IsOwner)
        {
            EquipWeaponServerRpc(slotIndex, weaponPrefab.name);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void EquipWeaponServerRpc(int slotIndex, string prefabName)
    {
        EquipWeaponClientRpc(slotIndex, prefabName);
    }

    [ClientRpc]
    private void EquipWeaponClientRpc(int slotIndex, string prefabName)
    {
        if (IsOwner) return; // Owner already equipped locally

        GameObject prefab = FindWeaponPrefabByName(prefabName);
        if (prefab != null)
        {
            EquipWeaponToSlotInternal(slotIndex, prefab);
        }
    }

    private GameObject FindWeaponPrefabByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        string cleanName = prefabName.Replace("(Clone)", "").Trim();

        if (startingWeaponPrefab != null && startingWeaponPrefab.name.Replace("(Clone)", "").Trim() == cleanName)
            return startingWeaponPrefab;

        if (BagManager.Instance != null && BagManager.Instance.allItemData != null)
        {
            var data = BagManager.Instance.allItemData.Find(x => x != null && x.prefab != null && x.prefab.name.Replace("(Clone)", "").Trim() == cleanName);
            if (data != null) return data.prefab;
        }

        return null;
    }

    private void EquipWeaponToSlotInternal(int slotIndex, GameObject weaponPrefab)
    {
        if (weaponPrefab == null || slotIndex < 0 || slotIndex >= 2) return;

        if (weaponAttachPoint == null)
        {
            Debug.LogError("[WeaponController] Cannot equip — weaponAttachPoint is null!");
            return;
        }

        // Clean up existing weapon in this slot
        if (weaponSlots[slotIndex] != null)
        {
            UnsubscribeFromWeapon(weaponSlots[slotIndex]);
            Destroy(weaponSlots[slotIndex].gameObject);
            weaponSlots[slotIndex] = null;
        }

        // Instantiate and parent to the hand anchor
        GameObject weaponObj = Instantiate(weaponPrefab, weaponAttachPoint);

        HandheldWeapon newWeapon = weaponObj.GetComponent<HandheldWeapon>();
        if (newWeapon == null)
        {
            Debug.LogError($"[WeaponController] Prefab '{weaponPrefab.name}' has no HandheldWeapon component!");
            Destroy(weaponObj);
            return;
        }

        weaponObj.transform.localPosition = -newWeapon.gripOffset;
        weaponObj.transform.localRotation = Quaternion.identity;

        weaponSlots[slotIndex] = newWeapon;
        SubscribeToWeapon(newWeapon);

        if (slotIndex == currentSlot)
        {
            newWeapon.gameObject.SetActive(true);

            if (playerAiming == null)
                playerAiming = GetComponent<PlayerAiming>();

            playerAiming?.SetWeapon(newWeapon);
            HandleAmmoChanged(newWeapon.GetCurrentAmmo(), newWeapon.maxAmmo);
            CheckZoom();

            Debug.Log($"[WeaponController] ✅ Equipped '{newWeapon.weaponName}' to active slot {slotIndex}.");
        }
        else
        {
            // Non-active slot — keep it hidden until player switches to it
            newWeapon.gameObject.SetActive(false);
            Debug.Log($"[WeaponController] Equipped '{newWeapon.weaponName}' to inactive slot {slotIndex}.");
        }

        // Notify HUD so the slot icon/ammo text refreshes immediately
        OnWeaponSlotUpdated?.Invoke(slotIndex);
    }

    /// <summary>
    /// Destroys and clears a slot. If it was the active slot, switches to the other.
    /// Used by BagManager when dropping a weapon.
    /// </summary>
    public void ClearWeaponSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        if (weaponSlots[slotIndex] == null) return;

        UnsubscribeFromWeapon(weaponSlots[slotIndex]);
        Destroy(weaponSlots[slotIndex].gameObject);
        weaponSlots[slotIndex] = null;

        // If the cleared slot was active, switch to the other
        if (slotIndex == currentSlot)
        {
            int other = 1 - slotIndex;
            if (weaponSlots[other] != null)
            {
                currentSlot = other;
                weaponSlots[other].gameObject.SetActive(true);

                if (playerAiming == null)
                    playerAiming = GetComponent<PlayerAiming>();

                playerAiming?.SetWeapon(weaponSlots[other]);
                HandleAmmoChanged(weaponSlots[other].GetCurrentAmmo(), weaponSlots[other].maxAmmo);
                CheckZoom();
                Debug.Log($"[WeaponController] Slot {slotIndex} cleared — switched to slot {other}.");
            }
            else
            {
                playerAiming?.SetWeapon(null);
                Debug.Log($"[WeaponController] Slot {slotIndex} cleared — no other weapon available.");
            }
        }

        // Notify HUD so the slot icon clears immediately
        OnWeaponSlotUpdated?.Invoke(slotIndex);
    }

    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 2 || weaponSlots[slotIndex] == null) return;
        if (slotIndex == currentSlot && weaponSlots[currentSlot].gameObject.activeSelf) return;

        SwitchToSlotInternal(slotIndex);

        if (IsSpawned && IsOwner)
        {
            SwitchToSlotServerRpc(slotIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SwitchToSlotServerRpc(int slotIndex)
    {
        SwitchToSlotClientRpc(slotIndex);
    }

    [ClientRpc]
    private void SwitchToSlotClientRpc(int slotIndex)
    {
        if (IsOwner) return;
        SwitchToSlotInternal(slotIndex);
    }

    private void SwitchToSlotInternal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 2 || weaponSlots[slotIndex] == null) return;

        // Deactivate current
        if (weaponSlots[currentSlot] != null)
        {
            weaponSlots[currentSlot].StopFiring();
            weaponSlots[currentSlot].gameObject.SetActive(false);
        }

        currentSlot = slotIndex;
        weaponSlots[currentSlot].gameObject.SetActive(true);

        if (playerAiming == null)
            playerAiming = GetComponent<PlayerAiming>();

        playerAiming?.SetWeapon(weaponSlots[currentSlot]);
        HandleAmmoChanged(weaponSlots[currentSlot].GetCurrentAmmo(), weaponSlots[currentSlot].maxAmmo);
        CheckZoom();

        Debug.Log($"[WeaponController] Switched to slot {slotIndex}: '{weaponSlots[currentSlot].weaponName}'.");
    }

    public void CheckZoom()
    {
        if (CameraController.Instance == null || CurrentWeapon == null) return;
        float zoom = CurrentWeapon.hasScope ? CurrentWeapon.scopeZoom : 1f;
        CameraController.Instance.SetZoom(zoom);
    }

    // ─── Input delegates ─────────────────────────────────────────────────────

    public void StartFiring()  => CurrentWeapon?.StartFiring();
    public void StopFiring()   => CurrentWeapon?.StopFiring();
    public void StartReload()  => CurrentWeapon?.Reload();

    // ─── Networked Firing Replication ────────────────────────────────────────

    public void NotifyFired(Vector3 position, Quaternion rotation, Vector2 direction, float speed, int damage)
    {
        if (IsSpawned && IsOwner)
        {
            FireServerRpc(position, rotation, direction, speed, damage);
            CurrentWeapon?.SpawnBulletLocal(position, rotation, direction, speed, damage, gameObject);
        }
        else if (!IsSpawned)
        {
            CurrentWeapon?.SpawnBulletLocal(position, rotation, direction, speed, damage, gameObject);
        }
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 position, Quaternion rotation, Vector2 direction, float speed, int damage)
    {
        FireClientRpc(position, rotation, direction, speed, damage);
    }

    [ClientRpc]
    private void FireClientRpc(Vector3 position, Quaternion rotation, Vector2 direction, float speed, int damage)
    {
        if (IsOwner) return; // Owner already spawned locally for instant feedback
        CurrentWeapon?.SpawnBulletLocal(position, rotation, direction, speed, damage, gameObject);
    }

    // ─── Networked Grenade Throwing ─────────────────────────────────────────

    public void ThrowGrenade()
    {
        if (BagManager.Instance == null) return;
        
        GrenadeType activeType = BagManager.Instance.activeGrenadeType;
        int count = BagManager.Instance.GetGrenadeCount(activeType);
        Debug.Log($"[WeaponController] ThrowGrenade requested. Active Type: {activeType}, Count: {count}");

        if (count <= 0)
        {
            Debug.LogWarning($"[WeaponController] Cannot throw: Count of active grenade type '{activeType}' is {count}.");
            return;
        }

        GameObject activePrefab = BagManager.Instance.GetActiveGrenadePrefab();
        if (activePrefab == null)
        {
            Debug.LogError($"[WeaponController] No prefab found for active grenade type '{activeType}'!");
            return;
        }

        Vector3 spawnPos = playerAiming != null ? playerAiming.GetGrenadeThrowPoint() : weaponAttachPoint.position;
        Vector2 aimDir   = playerAiming != null ? playerAiming.GetAimDirection()      : Vector2.right;

        // Decrement local inventory count
        BagManager.Instance.ConsumeGrenade(activeType);

        if (IsSpawned)
        {
            ThrowGrenadeServerRpc(activeType, spawnPos, aimDir);
            if (IsOwner)
            {
                SpawnGrenadeLocal(activeType, spawnPos, aimDir);
            }
        }
        else
        {
            SpawnGrenadeLocal(activeType, spawnPos, aimDir);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ThrowGrenadeServerRpc(GrenadeType grenadeType, Vector3 position, Vector2 direction)
    {
        ThrowGrenadeClientRpc(grenadeType, position, direction);
    }

    [ClientRpc]
    private void ThrowGrenadeClientRpc(GrenadeType grenadeType, Vector3 position, Vector2 direction)
    {
        if (IsOwner) return; // Owner already threw locally for instant feedback
        SpawnGrenadeLocal(grenadeType, position, direction);
    }

    private void SpawnGrenadeLocal(GrenadeType grenadeType, Vector3 position, Vector2 direction)
    {
        if (playerAiming != null)
        {
            playerAiming.TriggerGrenadeThrowAnimation();
        }

        if (BagManager.Instance == null) return;

        GameObject activePrefab = BagManager.Instance.GetGrenadePrefabByType(grenadeType);
        if (activePrefab == null) return;

        GameObject gObj = Instantiate(activePrefab, position, Quaternion.identity);

        // Set grenade sorting order to -4 when facing right
        bool facingRight = playerAiming != null ? (playerAiming.GetAimDirection().x >= 0) : true;
        SpriteRenderer[] srs = gObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            sr.sortingLayerName = "Default";
            sr.sortingOrder = facingRight ? -4 : 4;
        }

        Grenade g = gObj.GetComponent<Grenade>();
        if (g != null)
        {
            Collider2D myCollider = GetComponentInParent<Collider2D>();
            if (myCollider == null) myCollider = GetComponent<Collider2D>();
            g.Throw(direction, myCollider);
        }
    }


    // ─── Getters ─────────────────────────────────────────────────────────────

    public int GetCurrentAmmo() => CurrentWeapon != null ? CurrentWeapon.GetCurrentAmmo() : 0;
    public int GetMaxAmmo()     => CurrentWeapon != null ? CurrentWeapon.maxAmmo          : 0;

    // ─── Event wiring ────────────────────────────────────────────────────────

    private void SubscribeToWeapon(HandheldWeapon weapon)
    {
        weapon.OnAmmoChanged    += HandleAmmoChanged;
        weapon.OnFired          += HandleFired;
        weapon.OnReloadStart    += HandleReloadStart;
        weapon.OnReloadComplete += HandleReloadComplete;
    }

    private void UnsubscribeFromWeapon(HandheldWeapon weapon)
    {
        weapon.OnAmmoChanged    -= HandleAmmoChanged;
        weapon.OnFired          -= HandleFired;
        weapon.OnReloadStart    -= HandleReloadStart;
        weapon.OnReloadComplete -= HandleReloadComplete;
    }

    private void HandleAmmoChanged(int current, int max) => OnAmmoChanged?.Invoke(current, max);
    private void HandleFired()                           => OnWeaponFired?.Invoke();
    private void HandleReloadStart()                     => OnReloadStart?.Invoke();
    private void HandleReloadComplete()                  => OnReloadComplete?.Invoke();
}
