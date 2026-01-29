using UnityEngine;

public class PlayerAiming : MonoBehaviour
{
    [Header("Aiming")]
    [SerializeField] private float aimDistance = 2f;
    [SerializeField] private Transform aimIndicator;
    
    [Header("References")]
    [SerializeField] private CharacterAssembler characterAssembler;
    [SerializeField] private Transform weaponAttachPoint;       // The pivot point (Hand)
    
    // CHANGED: No longer directly managing a SpriteRenderer or WeaponData
    private HandheldWeapon currentWeapon;          // Assigned via SetWeapon()

    [Header("Eye Rotation")]
    [SerializeField] private Transform leftEyeTransform;
    [SerializeField] private Transform rightEyeTransform;
    [Range(0.01f, 0.2f)] [SerializeField] private float eyeMoveRadius = 0.05f; 
    [SerializeField] private float eyeLerpSpeed = 12f;
    
    private Vector3 leftEyeDefaultPos;
    private Vector3 rightEyeDefaultPos;
    
    private Vector2 aimInput;
    private Vector2 lastAimDirection = Vector2.right;
    
    [Header("Arm Positions - Facing LEFT")]
    [SerializeField] private Vector3 leftArm_LeftFacing = new Vector3(0.243f, -0.114f, 0f);
    [SerializeField] private Vector3 rightArm_LeftFacing = new Vector3(-0.182f, -0.079f, 0f);

    [Header("Arm Positions - Facing RIGHT")]
    [SerializeField] private Vector3 leftArm_RightFacing = new Vector3(-0.182f, -0.079f, 0f);
    [SerializeField] private Vector3 rightArm_RightFacing = new Vector3(0.243f, -0.114f, 0f);

    private void Start()
    {
        if (leftEyeTransform != null) leftEyeDefaultPos = leftEyeTransform.localPosition;
        if (rightEyeTransform != null) rightEyeDefaultPos = rightEyeTransform.localPosition;
    }

    [ContextMenu("Capture Current as Left Facing")]
    public void CaptureLeft()
    {
        if (characterAssembler == null) return;
        leftArm_LeftFacing = characterAssembler.GetLeftArmTransform().localPosition;
        rightArm_LeftFacing = characterAssembler.GetRightArmTransform().localPosition;
    }

    [ContextMenu("Capture Current as Right Facing")]
    public void CaptureRight()
    {
        if (characterAssembler == null) return;
        leftArm_RightFacing = characterAssembler.GetLeftArmTransform().localPosition;
        rightArm_RightFacing = characterAssembler.GetRightArmTransform().localPosition;
    }
    
    [ContextMenu("Capture Weapon Left")]
    public void CaptureWeaponLeft()
    {
        if (weaponAttachPoint != null)
            weapon_LeftFacing = weaponAttachPoint.localPosition;
    }

    [ContextMenu("Capture Weapon Right")]
    public void CaptureWeaponRight()
    {
        if (weaponAttachPoint != null)
            weapon_RightFacing = weaponAttachPoint.localPosition;
    }
    
    // ... existing SetWeapon/Aim methods ...

    /// <summary>
    /// Called by WeaponController when a new weapon prefab is spawned.
    /// </summary>
    public void SetWeapon(HandheldWeapon newWeapon)
    {
        currentWeapon = newWeapon;
    }
    
    public void SetAimInput(Vector2 input)
    {
        aimInput = input;
        
        if (input.magnitude > 0.1f)
        {
            lastAimDirection = input.normalized;
        }
    }
    
    private void Update()
    {
        UpdateAimIndicator();
        UpdateCharacterRotation();
    }
    
    private void UpdateAimIndicator()
    {
        if (aimIndicator == null || aimIndicator == transform) return;
        
        Vector3 indicatorPos = transform.position + (Vector3)(lastAimDirection * aimDistance);
        aimIndicator.position = indicatorPos;
        aimIndicator.gameObject.SetActive(aimInput.magnitude > 0.1f);
    }
    
    [Header("Weapon Positioning")]
    [SerializeField] private Vector3 weapon_LeftFacing = Vector3.zero;
    [SerializeField] private Vector3 weapon_RightFacing = Vector3.zero;
    [SerializeField] private Vector3 pivotOffset = Vector3.zero;

    private void UpdateCharacterRotation()
    {
        bool facingRight = lastAimDirection.x >= 0;
        
        // Flip Character Body
        if (characterAssembler != null)
        {
            characterAssembler.SetFacingDirection(facingRight);
            
            Transform l = characterAssembler.GetLeftArmTransform();
            Transform r = characterAssembler.GetRightArmTransform();
            
            if (l != null && r != null)
            {
                if (facingRight)
                {
                    l.localPosition = leftArm_RightFacing;
                    r.localPosition = rightArm_RightFacing;
                }
                else
                {
                    l.localPosition = leftArm_LeftFacing;
                    r.localPosition = rightArm_LeftFacing;
                }
            }
        }
        
        // Rotate & Flip Weapon
        if (weaponAttachPoint != null && currentWeapon != null)
        {
            // 0. Apply Pivot Offset (Based on facing direction)
            if (facingRight)
            {
                weaponAttachPoint.localPosition = weapon_RightFacing;
            }
            else
            {
                weaponAttachPoint.localPosition = weapon_LeftFacing;
            }
            
            // 1. Rotation (Pure Aim)
            float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg;
            weaponAttachPoint.rotation = Quaternion.Euler(0, 0, angle);

            // 2. Visual Flipping logic based on Weapon Settings
            float scaleX = currentWeapon.spriteFacesLeft ? -1f : 1f;
            float scaleY = (!facingRight) ? -1f : 1f;

            // Apply Scale to the Attach Point (which holds the Weapon Prefab)
            // This flips the child weapon correctly.
            weaponAttachPoint.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        
        RotateEyes();
        // Removed UpdateArms() - arms now just flip with body via CharacterAssembler
    }

    private void RotateEyes()
    {
        if (leftEyeTransform != null)
        {
            Vector3 targetLocal = leftEyeDefaultPos + new Vector3(lastAimDirection.x, lastAimDirection.y, 0f) * eyeMoveRadius;
            leftEyeTransform.localPosition = Vector3.Lerp(leftEyeTransform.localPosition, targetLocal, Time.deltaTime * eyeLerpSpeed);
        }
        
        if (rightEyeTransform != null)
        {
            Vector3 targetLocal = rightEyeDefaultPos + new Vector3(lastAimDirection.x, lastAimDirection.y, 0f) * eyeMoveRadius;
            rightEyeTransform.localPosition = Vector3.Lerp(rightEyeTransform.localPosition, targetLocal, Time.deltaTime * eyeLerpSpeed);
        }
    }
    
    public Vector2 GetAimDirection()
    {
        return lastAimDirection;
    }
    
    public Vector3 GetFirePoint()
    {
        if (currentWeapon != null && currentWeapon.firePoint != null) 
            return currentWeapon.firePoint.position;
            
        return weaponAttachPoint.position;
    }
    
    public Vector3 GetAimStartPosition()
    {
        return GetFirePoint();
    }
}
