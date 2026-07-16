using UnityEngine;

public class WeaponController : MonoBehaviour
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
        if (Instance == null) Instance = this;

        if (weaponAttachPoint == null)
            Debug.LogError("[WeaponController] ⚠️ weaponAttachPoint is NOT assigned! " +
                           "Please assign it in the Inspector. Weapons cannot be equipped without it.");
    }

    private void Start()
    {
        // Auto-find PlayerAiming if not wired in Inspector
        if (playerAiming == null)
            playerAiming = FindObjectOfType<PlayerAiming>();

        if (startingWeaponPrefab != null)
            EquipWeaponToSlot(0, startingWeaponPrefab);
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
        weaponObj.transform.localPosition = Vector3.zero;
        weaponObj.transform.localRotation = Quaternion.identity;

        HandheldWeapon newWeapon = weaponObj.GetComponent<HandheldWeapon>();
        if (newWeapon == null)
        {
            Debug.LogError($"[WeaponController] Prefab '{weaponPrefab.name}' has no HandheldWeapon component!");
            Destroy(weaponObj);
            return;
        }

        weaponSlots[slotIndex] = newWeapon;
        SubscribeToWeapon(newWeapon);

        if (slotIndex == currentSlot)
        {
            // ─── THIS IS THE KEY FIX ─────────────────────────────────────────
            // We CANNOT call SwitchToSlot() here because its guard returns early
            // when the weapon is already active (which it always is after Instantiate).
            // Instead, directly activate and notify all dependents.
            newWeapon.gameObject.SetActive(true);

            if (playerAiming == null)
                playerAiming = FindObjectOfType<PlayerAiming>();

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
                // Force-switch: set currentSlot first so SwitchToSlot guard doesn't block
                currentSlot = other;
                weaponSlots[other].gameObject.SetActive(true);

                if (playerAiming == null)
                    playerAiming = FindObjectOfType<PlayerAiming>();

                playerAiming?.SetWeapon(weaponSlots[other]);
                HandleAmmoChanged(weaponSlots[other].GetCurrentAmmo(), weaponSlots[other].maxAmmo);
                CheckZoom();
                Debug.Log($"[WeaponController] Slot {slotIndex} cleared — switched to slot {other}.");
            }
            else
            {
                // No other weapon available
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

        // Deactivate current
        if (weaponSlots[currentSlot] != null)
        {
            weaponSlots[currentSlot].StopFiring();
            weaponSlots[currentSlot].gameObject.SetActive(false);
        }

        currentSlot = slotIndex;
        weaponSlots[currentSlot].gameObject.SetActive(true);

        if (playerAiming == null)
            playerAiming = FindObjectOfType<PlayerAiming>();

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

    public void ThrowGrenade()
    {
        if (BagManager.Instance == null || BagManager.Instance.grenadeCount <= 0) return;
        if (grenadePrefab == null) return;

        Vector3 spawnPos = playerAiming != null ? playerAiming.GetFirePoint()    : weaponAttachPoint.position;
        Vector2 aimDir   = playerAiming != null ? playerAiming.GetAimDirection() : Vector2.right;

        GameObject gObj = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
        Grenade g = gObj.GetComponent<Grenade>();
        if (g != null) g.Throw(aimDir);

        BagManager.Instance.ConsumeGrenade();
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
