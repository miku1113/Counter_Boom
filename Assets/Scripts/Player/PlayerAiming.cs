using UnityEngine;

public class PlayerAiming : MonoBehaviour
{
    [Header("Aiming")]
    [SerializeField] private float     aimDistance = 2f;
    [SerializeField] private Transform aimIndicator;

    [Header("References")]
    [SerializeField] private CharacterAssembler characterAssembler;
    [SerializeField] private Transform          weaponAttachPoint;  // The pivot point (Hand)

    [Header("Weapon Positioning")]
    [SerializeField] private Vector3 weapon_LeftFacing  = Vector3.zero;
    [SerializeField] private Vector3 weapon_RightFacing = Vector3.zero;
    [SerializeField] private Vector3 pivotOffset        = Vector3.zero;

    [Header("Arm Positions - Facing LEFT")]
    [SerializeField] private Vector3 leftArm_LeftFacing  = new Vector3( 0.243f, -0.114f, 0f);
    [SerializeField] private Vector3 rightArm_LeftFacing = new Vector3(-0.182f, -0.079f, 0f);

    [Header("Arm Positions - Facing RIGHT")]
    [SerializeField] private Vector3 leftArm_RightFacing  = new Vector3(-0.182f, -0.079f, 0f);
    [SerializeField] private Vector3 rightArm_RightFacing = new Vector3( 0.243f, -0.114f, 0f);

    [Header("Eye Rotation")]
    [SerializeField] private Transform leftEyeTransform;
    [SerializeField] private Transform rightEyeTransform;
    [Range(0.01f, 0.2f)]
    [SerializeField] private float eyeMoveRadius = 0.05f;
    [SerializeField] private float eyeLerpSpeed  = 12f;

    // Runtime state
    private HandheldWeapon currentWeapon;
    private Vector2        aimInput;
    private Vector2        lastAimDirection = Vector2.right;
    private Vector3        leftEyeDefaultPos;
    private Vector3        rightEyeDefaultPos;

    private void Start()
    {
        if (leftEyeTransform  != null) leftEyeDefaultPos  = leftEyeTransform.localPosition;
        if (rightEyeTransform != null) rightEyeDefaultPos = rightEyeTransform.localPosition;
    }

    // ─── Inspector Context Menus ─────────────────────────────────────────────

    [ContextMenu("Capture Current as Left Facing")]
    public void CaptureLeft()
    {
        if (characterAssembler == null) return;
        leftArm_LeftFacing  = characterAssembler.GetLeftArmTransform().localPosition;
        rightArm_LeftFacing = characterAssembler.GetRightArmTransform().localPosition;
    }

    [ContextMenu("Capture Current as Right Facing")]
    public void CaptureRight()
    {
        if (characterAssembler == null) return;
        leftArm_RightFacing  = characterAssembler.GetLeftArmTransform().localPosition;
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

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Called by WeaponController when a new weapon prefab is spawned.</summary>
    public void SetWeapon(HandheldWeapon newWeapon)
    {
        currentWeapon = newWeapon;
    }

    public void SetAimInput(Vector2 input)
    {
        aimInput = input;
        if (input.magnitude > 0.1f)
            lastAimDirection = input.normalized;
    }

    public Vector2 GetAimDirection()  => lastAimDirection;

    public Vector3 GetFirePoint()
    {
        if (currentWeapon != null && currentWeapon.firePoint != null)
            return currentWeapon.firePoint.position;
        return weaponAttachPoint.position;
    }

    public Vector3 GetAimStartPosition() => GetFirePoint();

    // ─── Update Loop ─────────────────────────────────────────────────────────

    private void Update()
    {
        UpdateAimIndicator();
        UpdateCharacterRotation();
    }

    private void UpdateAimIndicator()
    {
        if (aimIndicator == null || aimIndicator == transform) return;

        aimIndicator.position = transform.position + (Vector3)(lastAimDirection * aimDistance);
        aimIndicator.gameObject.SetActive(aimInput.magnitude > 0.1f);
    }

    private void UpdateCharacterRotation()
    {
        bool facingRight = lastAimDirection.x >= 0;

        // Flip character body + arms
        if (characterAssembler != null)
        {
            characterAssembler.SetFacingDirection(facingRight);

            Transform l = characterAssembler.GetLeftArmTransform();
            Transform r = characterAssembler.GetRightArmTransform();

            if (l != null && r != null)
            {
                l.localPosition = facingRight ? leftArm_RightFacing  : leftArm_LeftFacing;
                r.localPosition = facingRight ? rightArm_RightFacing : rightArm_LeftFacing;
            }
        }

        // Rotate & flip weapon attach point
        if (weaponAttachPoint != null && currentWeapon != null)
        {
            // 1. Position offset based on facing
            weaponAttachPoint.localPosition = facingRight ? weapon_RightFacing : weapon_LeftFacing;

            // 2. Rotation — pure aim angle
            float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg;
            weaponAttachPoint.rotation = Quaternion.Euler(0, 0, angle);

            // 3. Sprite flip — scale the attach point to flip the child weapon prefab
            float scaleX = currentWeapon.spriteFacesLeft ? -1f : 1f;
            float scaleY = !facingRight ? -1f : 1f;
            weaponAttachPoint.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        RotateEyes();
    }

    private void RotateEyes()
    {
        if (leftEyeTransform != null)
        {
            Vector3 target = leftEyeDefaultPos + new Vector3(lastAimDirection.x, lastAimDirection.y, 0f) * eyeMoveRadius;
            leftEyeTransform.localPosition = Vector3.Lerp(leftEyeTransform.localPosition, target, Time.deltaTime * eyeLerpSpeed);
        }

        if (rightEyeTransform != null)
        {
            Vector3 target = rightEyeDefaultPos + new Vector3(lastAimDirection.x, lastAimDirection.y, 0f) * eyeMoveRadius;
            rightEyeTransform.localPosition = Vector3.Lerp(rightEyeTransform.localPosition, target, Time.deltaTime * eyeLerpSpeed);
        }
    }
}
