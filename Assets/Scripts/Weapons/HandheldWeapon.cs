using UnityEngine;
using System.Collections;

public class HandheldWeapon : MonoBehaviour
{
    [Header("Weapon Info")]
    public string weaponName;
    public Sprite weaponSprite; // Optional, maybe for UI
    
    [Header("Stats")]
    public int damage = 10;
    public float fireRate = 0.2f;
    public float bulletSpeed = 15f;
    public int maxAmmo = 30;
    public AmmoType ammoType;
    public InventoryItemData itemData; // Reference to data for dropping
    public float reloadTime = 1.5f;
    
    [Header("Mode")]
    public FireMode fireMode;
    public WeaponType weaponType;
    public WeaponHoldStyle holdStyle = WeaponHoldStyle.TwoHanded;

    [Header("Mini Militia Alignment")]
    public Vector3 gripOffset = Vector3.zero;
    public float rotationOffset = 0f;

    // Procedural animation offsets for recoil and equip
    public Vector3 AnimPosOffset { get; private set; } = Vector3.zero;
    public float AnimRotOffset { get; private set; } = 0f;
    
    [Header("Scope Settings")]
    public bool supportsScope = false;
    public bool hasScope = false;       // Runtime state
    public float scopeZoom = 2f;        // Multiplier (2x means camera size * 2 for zoom out)

    [Header("Prefabs")]
    public GameObject bulletPrefab;
    public GameObject fireEffectPrefab;
    public float fireEffectLifetime = 0.5f;

    [Header("Burst Settings")]
    public int burstCount = 3;
    public float burstShotInterval = 0.1f;

    [Header("Visual Feedback")]
    public float shakeAmount = 0.05f;
    public float shakeDuration = 0.1f;
    public float equipDuration = 0.25f;
    public float equipDropAmount = 0.5f;
    public float equipRotationAmount = -30f;

    [Header("Visual References")]
    public Transform firePoint;     // Assign in Prefab
    public Transform offHandGrip;   // Assign in Prefab
    public bool spriteFacesLeft = true;

    // Runtime State
    private int ammoInMag;
    private bool isReloading = false;
    private float lastFireTime;
    private bool isFiring = false;

    // Events
    public System.Action<int, int> OnAmmoChanged;
    public System.Action OnReloadStart;
    public System.Action OnReloadComplete;
    public System.Action OnFired;

    private void Awake()
    {
        ammoInMag = maxAmmo;
    }

    private void OnEnable()
    {
        // Only play equip animation if we are actually equipped (parented to a hand/holder)
        // This prevents dropped items (which might reuse this prefab) from flying to 0,0,0
        if (transform.parent != null)
        {
            if (equipDuration > 0)
            {
                StartCoroutine(EquipRoutine());
            }
            else
            {
                transform.localPosition = -gripOffset;
                transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void Update()
    {
        if (isFiring && fireMode == FireMode.Automatic)
        {
            TryFire();
        }
    }

    public void StartFiring()
    {
        isFiring = true;
        if (fireMode == FireMode.Single)
        {
            TryFire();
        }
        else if (fireMode == FireMode.Burst)
        {
            TryFireBurst();
        }
    }

    public void StopFiring()
    {
        isFiring = false;
    }

    public void Reload()
    {
        if (!isReloading && ammoInMag < maxAmmo)
        {
            // Check if we have ammo in bag
            if (BagManager.Instance != null && BagManager.Instance.GetAmmo(ammoType) > 0)
            {
                StartCoroutine(ReloadRoutine());
            }
        }
    }

    private void TryFire()
    {
        if (isReloading || ammoInMag <= 0 || Time.time - lastFireTime < fireRate) return;

        lastFireTime = Time.time;
        Shoot();
    }

    private void TryFireBurst()
    {
        if (isReloading || ammoInMag <= 0 || Time.time - lastFireTime < fireRate) return;

        lastFireTime = Time.time;
        StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        for (int i = 0; i < burstCount; i++)
        {
            if (ammoInMag <= 0 || isReloading) yield break; // Stop if out of ammo or reloading started
            
            Shoot();
            
            // Wait for next shot in burst, but don't wait after the last one
            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstShotInterval);
            }
        }
    }

    private void Shoot()
    {
        if (ammoInMag <= 0) return;

        ammoInMag--;

        WeaponController parentController = GetComponentInParent<WeaponController>();
        if (parentController != null)
        {
            parentController.NotifyFired(firePoint.position, firePoint.rotation, firePoint.right, bulletSpeed, damage);
        }
        else
        {
            SpawnBulletLocal(firePoint.position, firePoint.rotation, firePoint.right, bulletSpeed, damage);
        }

        // Shake Effect
        StopCoroutine("ShakeRoutine");
        StopCoroutine("EquipRoutine"); // Ensure equip doesn't fight shake
        StartCoroutine(ShakeRoutine());

        OnFired?.Invoke();
        OnAmmoChanged?.Invoke(ammoInMag, maxAmmo);
    }

    public void SpawnBulletLocal(Vector3 position, Quaternion rotation, Vector2 direction, float speed, int damage)
    {
        if (bulletPrefab != null)
        {
             GameObject bullet = Instantiate(bulletPrefab, position, rotation);
             Bullet b = bullet.GetComponent<Bullet>();
             if (b != null)
             {
                 b.Initialize(direction, speed, damage);
             }

             // Visual Fire Effect
             if (fireEffectPrefab != null && firePoint != null)
             {
                 GameObject effect = Instantiate(fireEffectPrefab, position, rotation, firePoint);
                 Destroy(effect, fireEffectLifetime);
             }
        }
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeAmount;
            float y = Random.Range(-1f, 1f) * shakeAmount;

            AnimPosOffset = new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        AnimPosOffset = Vector3.zero;
    }

    private IEnumerator EquipRoutine()
    {
        float elapsed = 0f;
        float startDrop = equipDropAmount;
        float startRot = equipRotationAmount;

        // Simple ease out
        while (elapsed < equipDuration)
        {
             float t = elapsed / equipDuration;
             // Cubic ease out
             t = 1f - Mathf.Pow(1f - t, 3f);

             AnimPosOffset = Vector3.Lerp(new Vector3(0f, startDrop, 0f), Vector3.zero, t);
             AnimRotOffset = Mathf.Lerp(startRot, 0f, t);

             elapsed += Time.deltaTime;
             yield return null;
        }

        AnimPosOffset = Vector3.zero;
        AnimRotOffset = 0f;
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        OnReloadStart?.Invoke();
        
        yield return new WaitForSeconds(reloadTime);
        
        if (BagManager.Instance != null)
        {
            int needed = maxAmmo - ammoInMag;
            int available = BagManager.Instance.GetAmmo(ammoType);
            int toLoad = Mathf.Min(needed, available);
            
            ammoInMag += toLoad;
            BagManager.Instance.ConsumeAmmo(ammoType, toLoad);
        }
        else
        {
            // Fallback for testing if BagManager is missing
            ammoInMag = maxAmmo;
        }

        isReloading = false;
        OnReloadComplete?.Invoke();
        OnAmmoChanged?.Invoke(ammoInMag, maxAmmo);
    }
    
    // Getters
    public int GetCurrentAmmo() => ammoInMag;

    public void AttachScope()
    {
        // Visual logic can go here (enable a sub-mesh, etc.)
        Debug.Log($"[HandheldWeapon] Scope attached to {weaponName}");
    }
}

public enum FireMode
{
    Single,
    Automatic,
    Burst
}

public enum WeaponType
{
    Ranged,
    Melee
}

public enum WeaponHoldStyle
{
    SingleHanded,
    TwoHanded
}
