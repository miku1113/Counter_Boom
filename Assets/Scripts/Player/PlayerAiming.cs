using UnityEngine;
using Unity.Netcode;

public class PlayerAiming : NetworkBehaviour
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
    [SerializeField] private Vector3 leftArm_RightFacing  = new Vector3( 0.182f, -0.079f, 0f);
    [SerializeField] private Vector3 rightArm_RightFacing = new Vector3(-0.243f, -0.114f, 0f);

    [Header("Eye Rotation")]
    [SerializeField] private Transform leftEyeTransform;
    [SerializeField] private Transform rightEyeTransform;
    [Range(0.005f, 0.1f)]
    [SerializeField] private float eyeMoveRadius = 0.02f;
    [SerializeField] private float eyeLerpSpeed  = 12f;

    [Header("Mini Militia Aiming Setup")]
    [SerializeField] private float mainArmLength = 0.45f;
    [SerializeField] private float leftArmAngleOffset = 0f;
    [SerializeField] private float rightArmAngleOffset = 0f;
    [SerializeField] private Vector3 handPositionOffset = Vector3.zero;

    [Header("Hand Point Fine Tuning Offsets")]
    [SerializeField] private Vector3 leftArmHandOffset  = Vector3.zero;
    [SerializeField] private Vector3 rightArmHandOffset = Vector3.zero;

    // Runtime state
    private int defaultLeftSortingOrder;
    private int defaultRightSortingOrder;
    private SpriteRenderer leftArmSr;
    private SpriteRenderer rightArmSr;
    private HandheldWeapon currentWeapon;
    private Vector3        weaponDefaultScale = Vector3.one;
    private Vector2        aimInput;
    private Vector2        lastAimDirection = Vector2.right;
    private Vector3        leftEyeDefaultPos;
    private Vector3        rightEyeDefaultPos;

    // Networked aim synchronization
    private readonly NetworkVariable<Vector2> netAimDirection = new NetworkVariable<Vector2>(
        Vector2.right, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        if (leftEyeTransform  != null) leftEyeDefaultPos  = leftEyeTransform.localPosition;
        if (rightEyeTransform != null) rightEyeDefaultPos = rightEyeTransform.localPosition;
    }

    private void Start()
    {
        if (characterAssembler != null)
        {
            Transform l = characterAssembler.GetLeftArmTransform();
            if (l == null) l = transform.Find("Arms/LeftArm");
            if (l != null)
            {
                leftArmSr = l.GetComponent<SpriteRenderer>();
                if (leftArmSr != null) defaultLeftSortingOrder = leftArmSr.sortingOrder;
            }

            Transform r = characterAssembler.GetRightArmTransform();
            if (r == null) r = transform.Find("Arms/RightArm");
            if (r != null)
            {
                rightArmSr = r.GetComponent<SpriteRenderer>();
                if (rightArmSr != null) defaultRightSortingOrder = rightArmSr.sortingOrder;
            }
        }
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

    public void SetWeapon(HandheldWeapon newWeapon)
    {
        currentWeapon = newWeapon;

        if (newWeapon != null)
        {
            weaponDefaultScale = newWeapon.transform.localScale;

            // Cache leftArmSr if not done yet
            if (leftArmSr == null)
            {
                Transform l = characterAssembler != null ? characterAssembler.GetLeftArmTransform() : null;
                if (l == null) l = transform.Find("Arms/LeftArm");
                if (l == null) l = transform.Find("Body/LeftArm");
                if (l != null) leftArmSr = l.GetComponent<SpriteRenderer>();
            }

            // Automate weapon sorting layer/order setup (placed 1 order behind front LeftArm, i.e. order 3)
            SpriteRenderer[] srs = newWeapon.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                sr.sortingLayerName = "player";
                sr.sortingOrder = leftArmSr != null ? leftArmSr.sortingOrder - 1 : 3;
            }
        }
    }

    public void SetAimInput(Vector2 input)
    {
        aimInput = input;
        if (input.magnitude > 0.05f)
        {
            lastAimDirection = input.normalized;
            if (IsSpawned && IsOwner)
            {
                netAimDirection.Value = lastAimDirection;
            }
        }
    }

    public bool IsAiming => aimInput.magnitude > 0.05f;
    public Vector2 RawAimInput => aimInput;
    public Vector2 GetAimDirection()  => lastAimDirection;

    public Vector3 GetFirePoint()
    {
        if (currentWeapon != null && currentWeapon.gameObject.activeSelf)
        {
            if (currentWeapon.firePoint != null)
                return currentWeapon.firePoint.position;

            // Search for FirePoint, FirePOint, Muzzle or any child containing "fire" or "muzzle"
            Transform fp = currentWeapon.transform.Find("FirePoint");
            if (fp == null) fp = currentWeapon.transform.Find("FirePOint");
            if (fp == null) fp = currentWeapon.transform.Find("Muzzle");
            if (fp == null)
            {
                foreach (Transform child in currentWeapon.transform)
                {
                    string lower = child.name.ToLower();
                    if (lower.Contains("fire") || lower.Contains("muzzle"))
                    {
                        fp = child;
                        break;
                    }
                }
            }
            if (fp != null)
                return fp.position;

            return currentWeapon.transform.position;
        }

        // --- UNARMED / NO WEAPON HELD ---
        // Calculate head-level position
        Vector3 headPos;
        if (leftEyeTransform != null && rightEyeTransform != null)
        {
            headPos = (leftEyeTransform.position + rightEyeTransform.position) * 0.5f;
        }
        else if (characterAssembler != null && characterAssembler.GetHeadTransform() != null)
        {
            headPos = characterAssembler.GetHeadTransform().position;
        }
        else
        {
            // Default vertical offset for head level
            headPos = transform.position + new Vector3(0f, 0.45f, 0f);
        }

        // Offset outside of the head in the aim direction so dots start outside the head mesh
        float headRadiusOffset = 0.4f;
        return headPos + (Vector3)(lastAimDirection * headRadiusOffset);
    }

    public Vector3 GetAimStartPosition() => GetFirePoint();

    public Vector3 GetGrenadeThrowPoint()
    {
        bool facingRight = lastAimDirection.x >= 0;
        Transform leftArm  = characterAssembler != null ? characterAssembler.GetLeftArmTransform()  : transform.Find("Arms/LeftArm");
        Transform rightArm = characterAssembler != null ? characterAssembler.GetRightArmTransform() : transform.Find("Arms/RightArm");

        Transform offHandArm = facingRight ? rightArm : leftArm;
        if (offHandArm != null)
        {
            Transform handPoint = offHandArm.Find("HandPoint");
            if (handPoint != null)
            {
                return handPoint.position;
            }
        }
        return GetFirePoint();
    }

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
        if (IsSpawned && !IsOwner)
        {
            lastAimDirection = netAimDirection.Value;
        }

        bool facingRight = lastAimDirection.x >= 0;

        // Flip character body + arms
        if (characterAssembler != null)
        {
            characterAssembler.SetFacingDirection(facingRight);
        }

        // Rotation and flipping of weapon attach point is now handled in LateUpdate to sync with arm rotations.
        RotateEyes();
    }

    private void LateUpdate()
    {
        RotateArmsAndWeapon();
    }

    /// <summary>
    /// Aligns the main holding arm (left arm) with the weapon handle using its internal HandPoint child reference,
    /// and positions the weapon exactly at the hand.
    /// </summary>
    private void RotateArmsAndWeapon()
    {
        Transform leftArm = null;
        Transform rightArm = null;
        if (characterAssembler != null)
        {
            leftArm  = characterAssembler.GetLeftArmTransform();
            rightArm = characterAssembler.GetRightArmTransform();
        }
        if (leftArm == null)  leftArm  = transform.Find("Arms/LeftArm");
        if (rightArm == null) rightArm = transform.Find("Arms/RightArm");

        if (leftArm == null && rightArm == null) return;

        bool facingRight = lastAimDirection.x >= 0;
        float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg;

        // Align arm pivots to LeftShoulderPoint and RightShoulderPoint defined on the body
        AlignArmsToShoulderPoints(leftArm, rightArm);

        // When facing right: LeftArm is main holding arm, RightArm is off-hand.
        // When facing left: RightArm (the back arm) is main holding arm, LeftArm is off-hand.
        Transform mainArm    = facingRight ? leftArm  : rightArm;
        Transform offHandArm = facingRight ? rightArm : leftArm;

        if (mainArm == null) return;

        // 1. Rotate main holding arm when a weapon is equipped, or keep both arms at rest when unequipped
        if (currentWeapon != null)
        {
            float mainArmRotation = angle;
            if (facingRight)
            {
                mainArmRotation += leftArmAngleOffset;
            }
            else
            {
                mainArmRotation = (angle - 180f) - rightArmAngleOffset;
            }

            // Apply procedural weapon switch hand dip animation
            if (weaponSwitchOffsetTimer > 0f)
            {
                mainArmRotation += weaponSwitchOffsetTimer * -35f;
            }

            mainArm.rotation = Quaternion.Euler(0, 0, mainArmRotation);

            if (offHandArm != null && grenadeThrowTimer <= 0f)
            {
                offHandArm.localRotation = Quaternion.identity;
            }
        }
        else
        {
            // Unarmed mode: Keep light front arm (leftArm) at rest pose, rotate & move ONLY dark back arm (rightArm)
            if (leftArm != null && grenadeThrowTimer <= 0f)
            {
                leftArm.localRotation = Quaternion.identity;
            }

            if (rightArm != null)
            {
                float darkArmRotation = angle;
                if (facingRight)
                {
                    darkArmRotation += rightArmAngleOffset;
                }
                else
                {
                    darkArmRotation = (angle - 180f) - rightArmAngleOffset;
                }

                // Add dynamic wrist rotation snap when striking
                if (punchAnimTimer > 0f)
                {
                    darkArmRotation += punchAnimTimer * (facingRight ? 18f : -18f);
                }

                rightArm.rotation = Quaternion.Euler(0, 0, darkArmRotation);

                // Punch animation: ONLY move the dark back arm (rightArm) forward along aim direction!
                if (punchAnimTimer > 0f)
                {
                    rightArm.position += (Vector3)(lastAimDirection * (punchAnimTimer * 0.75f));

                    // Scale impact pop at peak extension
                    float scaleFactor = 1f + (punchAnimTimer * 0.22f);
                    rightArm.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
                }
                else
                {
                    rightArm.localScale = Vector3.one;
                }
            }
        }

        // Keep hand sorting layers fixed (never change hand layers!)
        if (leftArmSr != null)  leftArmSr.sortingOrder  = defaultLeftSortingOrder;  // Order 4
        if (rightArmSr != null) rightArmSr.sortingOrder = defaultRightSortingOrder; // Order -1

        // 2. Position and rotate the weapon at mainArm's HandPoint
        if (weaponAttachPoint != null && currentWeapon != null)
        {
            Transform handPoint = mainArm.Find("HandPoint");
            Vector3 handPos;
            if (handPoint != null)
            {
                handPos = handPoint.position;
            }
            else
            {
                Vector3 armDir = facingRight ? mainArm.right : -mainArm.right;
                handPos = mainArm.position + armDir * mainArmLength;
            }

            Vector3 handOffset = (mainArm == leftArm) ? leftArmHandOffset : rightArmHandOffset;
            handPos += mainArm.rotation * handOffset;

            // Determine grip offset
            Vector3 finalGripOffset = Vector3.zero;
            Transform gripPoint = currentWeapon.transform.Find("Off-Hand Grip");
            if (gripPoint == null) gripPoint = currentWeapon.transform.Find("Grip");
            if (gripPoint == null) gripPoint = currentWeapon.transform.Find("Handle");
            if (gripPoint == null) gripPoint = currentWeapon.transform.Find("HandlePoint");
            if (gripPoint == null) gripPoint = currentWeapon.transform.Find("Handal");
            if (gripPoint == null)
            {
                foreach (Transform child in currentWeapon.transform)
                {
                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("grip") || nameLower.Contains("handle") || nameLower.Contains("handal"))
                    {
                        gripPoint = child;
                        break;
                    }
                }
            }

            if (gripPoint != null)
            {
                finalGripOffset = gripPoint.localPosition;
            }
            else
            {
                finalGripOffset = currentWeapon.gripOffset != Vector3.zero ? currentWeapon.gripOffset : pivotOffset;
            }

            float scaleX = (currentWeapon.spriteFacesLeft ? -1f : 1f) * Mathf.Abs(weaponDefaultScale.x);
            float scaleY = (!facingRight ? -1f : 1f) * Mathf.Abs(weaponDefaultScale.y);
            float scaleZ = Mathf.Abs(weaponDefaultScale.z);

            Vector3 scaledGripOffset = new Vector3(finalGripOffset.x * scaleX, finalGripOffset.y * scaleY, finalGripOffset.z * scaleZ);
            Vector3 worldGripOffset = Quaternion.Euler(0, 0, angle) * scaledGripOffset;

            weaponAttachPoint.position = handPos;
            weaponAttachPoint.rotation = Quaternion.Euler(0, 0, angle);
            weaponAttachPoint.localScale = Vector3.one;

            Vector3 animPos = currentWeapon != null ? currentWeapon.AnimPosOffset : Vector3.zero;
            float animRot = currentWeapon != null ? currentWeapon.AnimRotOffset : 0f;

            Vector3 finalWorldGripOffset = handPos - (worldGripOffset + Quaternion.Euler(0, 0, angle) * animPos);

            currentWeapon.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            currentWeapon.transform.rotation = Quaternion.Euler(0, 0, angle + currentWeapon.rotationOffset + animRot);
            currentWeapon.transform.position = finalWorldGripOffset;

            // Only change the layer for the gun! (Order 3 when facing right, Order -2 when facing left)
            int baseOrder = characterAssembler != null ? characterAssembler.baseSortingOrder : 0;
            int gunOrder  = facingRight ? (baseOrder + 3) : (baseOrder - 2);

            SpriteRenderer[] srs = currentWeapon.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                sr.sortingOrder = gunOrder;
            }
        }

        // 3. Handle Off-Hand Arm Overhand Grenade Throw Animation
        if (offHandArm != null && grenadeThrowTimer > 0f)
        {
            grenadeThrowTimer -= Time.deltaTime;
            float t = 1f - (grenadeThrowTimer / grenadeThrowDuration);

            float throwPeakAngle  = facingRight ? 70f : -70f;
            float throwPitchAngle = facingRight ? 30f : -30f;

            float localThrowAngle;
            if (t < 0.35f)
            {
                float windT = t / 0.35f;
                windT = 1f - Mathf.Pow(1f - windT, 2f);
                localThrowAngle = Mathf.Lerp(0f, throwPeakAngle, windT);
            }
            else if (t < 0.75f)
            {
                float swingT = (t - 0.35f) / 0.40f;
                localThrowAngle = Mathf.Lerp(throwPeakAngle, throwPitchAngle, swingT);
            }
            else
            {
                float restT = (t - 0.75f) / 0.25f;
                localThrowAngle = Mathf.Lerp(throwPitchAngle, 0f, restT);
            }

            offHandArm.localRotation = Quaternion.Euler(0, 0, localThrowAngle);
        }
    }

    [Header("Grenade Animation")]
    [SerializeField] private float grenadeThrowDuration = 0.45f;
    private float grenadeThrowTimer = 0f;

    public void TriggerGrenadeThrowAnimation()
    {
        grenadeThrowTimer = grenadeThrowDuration;
    }

    private void RotateEyes()
    {
        bool facingRight = lastAimDirection.x >= 0;
        float aimX = facingRight ? -lastAimDirection.x : lastAimDirection.x;

        if (leftEyeTransform != null)
        {
            Vector3 target = leftEyeDefaultPos + new Vector3(aimX, lastAimDirection.y, 0f) * eyeMoveRadius;
            leftEyeTransform.localPosition = Vector3.Lerp(leftEyeTransform.localPosition, target, Time.deltaTime * eyeLerpSpeed);
        }

        if (rightEyeTransform != null)
        {
            Vector3 target = rightEyeDefaultPos + new Vector3(aimX, lastAimDirection.y, 0f) * eyeMoveRadius;
            rightEyeTransform.localPosition = Vector3.Lerp(rightEyeTransform.localPosition, target, Time.deltaTime * eyeLerpSpeed);
        }
    }

    private void AlignArmsToShoulderPoints(Transform leftArm, Transform rightArm)
    {
        if (leftArm != null)
        {
            Transform ls = FindShoulderPoint("left");
            if (ls != null)
            {
                leftArm.position = ls.position;
            }
        }

        if (rightArm != null)
        {
            Transform rs = FindShoulderPoint("right");
            if (rs != null)
            {
                rightArm.position = rs.position;
            }
        }
    }

    private Transform FindShoulderPoint(string side)
    {
        Transform searchRoot = characterAssembler != null ? characterAssembler.transform : transform;
        string targetName = side.ToLower() == "left" ? "leftshoulderpoint" : "rightshoulderpoint";

        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            string nameLower = child.name.ToLower();
            if (nameLower == targetName || (nameLower.Contains(side) && (nameLower.Contains("shoulder") || nameLower.Contains("holder") || nameLower.Contains("solder"))))
            {
                return child;
            }
        }
        return null;
    }

    private float weaponSwitchOffsetTimer = 0f;
    private Coroutine weaponSwitchCoroutine;

    /// <summary>
    /// Triggers a procedural hand/arm dip and re-equip animation when switching weapons.
    /// </summary>
    public void PlayWeaponSwitchAnimation()
    {
        if (weaponSwitchCoroutine != null) StopCoroutine(weaponSwitchCoroutine);
        weaponSwitchCoroutine = StartCoroutine(WeaponSwitchRoutine());
    }

    private System.Collections.IEnumerator WeaponSwitchRoutine()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.enabled)
        {
            anim.SetTrigger("switchWeapon");
        }

        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Parabola: dips down to max offset at t=0.5, returns to 0 at t=1
            weaponSwitchOffsetTimer = Mathf.Sin(t * Mathf.PI);
            elapsed += Time.deltaTime;
            yield return null;
        }

        weaponSwitchOffsetTimer = 0f;
        weaponSwitchCoroutine = null;
    }

    private float punchAnimTimer = 0f;
    private bool punchRightArm = false;
    private Coroutine punchCoroutine;

    /// <summary>
    /// Triggers a procedural boxing punch arm animation when unarmed.
    /// </summary>
    public void PlayMeleePunchAnimation(bool useRightArm)
    {
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(MeleePunchRoutine(useRightArm));
    }

    private System.Collections.IEnumerator MeleePunchRoutine(bool useRightArm)
    {
        punchRightArm = useRightArm;
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.enabled)
        {
            anim.SetTrigger("punch");
        }

        float duration = 0.24f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float curve;
            if (t < 0.35f)
            {
                float norm = t / 0.35f;
                curve = Mathf.Sin(norm * Mathf.PI * 0.5f); // Fast explosive thrust out
            }
            else
            {
                float norm = (t - 0.35f) / 0.65f;
                curve = Mathf.Cos(norm * Mathf.PI * 0.5f); // Smooth recovery back
            }

            punchAnimTimer = curve;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Transform rArm = GetRightArmTransform();
        if (rArm != null) rArm.localScale = Vector3.one;
        punchAnimTimer = 0f;
        punchCoroutine = null;
    }

    private Transform GetRightArmTransform()
    {
        Transform r = characterAssembler != null ? characterAssembler.GetRightArmTransform() : null;
        if (r == null) r = transform.Find("Arms/RightArm");
        if (r == null) r = transform.Find("Body/RightArm");
        return r;
    }
}
