using UnityEngine;
using System.Collections;

public class WeaponController : MonoBehaviour
{
    public static WeaponController Instance;

    [Header("Setup")]
    [SerializeField] private GameObject startingWeaponPrefab;
    [SerializeField] private Transform weaponAttachPoint;
    
    [Header("References")]
    [SerializeField] private PlayerAiming playerAiming;
    [SerializeField] private CharacterAssembler characterAssembler; // Just purely for reference/sorting if needed logic later
    
    [Header("Grenade")]
    [SerializeField] private GameObject grenadePrefab;
    
    // Runtime
    private HandheldWeapon[] weaponSlots = new HandheldWeapon[2];
    public int currentSlot = 0;
    private HandheldWeapon currentWeaponInstance => weaponSlots[currentSlot];
    
    // Events (Proxies for UI)
    public System.Action<int, int> OnAmmoChanged;
    public System.Action OnWeaponFired;
    public System.Action OnReloadStart;
    public System.Action OnReloadComplete;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (startingWeaponPrefab != null)
        {
            EquipWeaponToSlot(0, startingWeaponPrefab);
        }
    }
    
    public void EquipWeaponToSlot(int slotIndex, GameObject weaponPrefab)
    {
        if (weaponPrefab == null || slotIndex < 0 || slotIndex >= 2) return;
        
        // 1. Cleanup Old in this slot
        if (weaponSlots[slotIndex] != null)
        {
            UnsubscribeFromWeapon(weaponSlots[slotIndex]);
            Destroy(weaponSlots[slotIndex].gameObject);
            weaponSlots[slotIndex] = null;
        }
        
        // 2. Instantiate New
        GameObject weaponObj = Instantiate(weaponPrefab, weaponAttachPoint);
        weaponObj.transform.localPosition = Vector3.zero;
        weaponObj.transform.localRotation = Quaternion.identity;
        
        // 3. Get Script
        HandheldWeapon newWeapon = weaponObj.GetComponent<HandheldWeapon>();
        if (newWeapon != null)
        {
            weaponSlots[slotIndex] = newWeapon;
            SubscribeToWeapon(newWeapon);
            
            // Inform BagManager
            if (BagManager.Instance != null)
            {
                BagManager.Instance.SetWeaponInSlot(slotIndex, newWeapon);
            }

            // If it's the current slot or first weapon, activate it
            if (slotIndex == currentSlot || (weaponSlots[0] == null && weaponSlots[1] == null))
            {
                SwitchToSlot(slotIndex);
            }
            else
            {
                newWeapon.gameObject.SetActive(false);
            }
        }
    }

    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 2 || weaponSlots[slotIndex] == null) return;
        
        // Prevent re-equipping the same weapon (avoids re-playing animation)
        if (slotIndex == currentSlot && weaponSlots[currentSlot].gameObject.activeSelf) return;

        // Deactivate current
        if (weaponSlots[currentSlot] != null)
        {
            weaponSlots[currentSlot].StopFiring();
            weaponSlots[currentSlot].gameObject.SetActive(false);
        }

        currentSlot = slotIndex;
        if (BagManager.Instance != null) BagManager.Instance.currentWeaponIndex = slotIndex;
        
        // Activate new
        weaponSlots[currentSlot].gameObject.SetActive(true);
        
        if (playerAiming != null)
        {
            playerAiming.SetWeapon(weaponSlots[currentSlot]);
        }
        
        HandleAmmoChanged(weaponSlots[currentSlot].GetCurrentAmmo(), weaponSlots[currentSlot].maxAmmo);
        
        // Update Zoom based on weapon scope
        CheckZoom();
    }

    public void CheckZoom()
    {
        if (CameraController.Instance != null && currentWeaponInstance != null)
        {
            // If weapon has a scope, apply its zoom. Otherwise default (1f).
            float zoom = currentWeaponInstance.hasScope ? currentWeaponInstance.scopeZoom : 1f;
            CameraController.Instance.SetZoom(zoom);
        }
    }

    private void SubscribeToWeapon(HandheldWeapon weapon)
    {
        weapon.OnAmmoChanged += HandleAmmoChanged;
        weapon.OnFired += HandleFired;
        weapon.OnReloadStart += HandleReloadStart;
        weapon.OnReloadComplete += HandleReloadComplete;
    }

    private void UnsubscribeFromWeapon(HandheldWeapon weapon)
    {
        weapon.OnAmmoChanged -= HandleAmmoChanged;
        weapon.OnFired -= HandleFired;
        weapon.OnReloadStart -= HandleReloadStart;
        weapon.OnReloadComplete -= HandleReloadComplete;
    }
    
    // Input Delegates
    public void StartFiring()
    {
        if (currentWeaponInstance != null) currentWeaponInstance.StartFiring();
    }
    
    public void StopFiring()
    {
        if (currentWeaponInstance != null) currentWeaponInstance.StopFiring();
    }
    
    public void StartReload()
    {
        if (currentWeaponInstance != null) currentWeaponInstance.Reload();
    }

    public void ThrowGrenade()
    {
        if (BagManager.Instance != null && BagManager.Instance.grenadeCount > 0)
        {
            if (grenadePrefab != null)
            {
                Vector3 spawnPos = playerAiming != null ? playerAiming.GetFirePoint() : weaponAttachPoint.position;
                Vector2 aimDir = playerAiming != null ? playerAiming.GetAimDirection() : Vector2.right;
                
                GameObject gObj = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
                Grenade g = gObj.GetComponent<Grenade>();
                if (g != null) g.Throw(aimDir);
                
                BagManager.Instance.ConsumeGrenade();
            }
        }
    }
    
    // Event Handlers
    private void HandleAmmoChanged(int current, int max) => OnAmmoChanged?.Invoke(current, max);
    private void HandleFired() => OnWeaponFired?.Invoke();
    private void HandleReloadStart() => OnReloadStart?.Invoke();
    private void HandleReloadComplete() => OnReloadComplete?.Invoke();
    
    // Getters for UI/Other
    public int GetCurrentAmmo() => currentWeaponInstance != null ? currentWeaponInstance.GetCurrentAmmo() : 0;
    public int GetMaxAmmo() => currentWeaponInstance != null ? currentWeaponInstance.maxAmmo : 0;
}
