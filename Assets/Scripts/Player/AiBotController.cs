using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum AiBotState
{
    Idle,
    Patrol,
    Scavenge,
    Investigate,
    Engage,
    TakeCover,
    Heal,
    UnstuckEscape
}

public class AiBotController : MonoBehaviour
{
    [Header("Bot Identification")]
    public string botName = "AI Bot";
    public string targetTag = "Player";

    [Header("Perception & Vision")]
    [Tooltip("Maximum sight distance for visual target acquisition.")]
    public float sightRadius = 18f;
    [Tooltip("Proximity radius for 360-degree awareness.")]
    public float proximityRadius = 6f;
    [Tooltip("Layers that block bot vision.")]
    public LayerMask visionBlockerMask;

    [Header("Thinking & Reaction Times")]
    [Tooltip("How fast the bot rotates its aim towards targets (degrees per sec).")]
    public float aimRotationSpeed = 540f;
    [Tooltip("Interval for AI decision making tick.")]
    public float thinkInterval = 0.12f;

    [Header("Combat Settings")]
    [Tooltip("Optimal combat engagement distance.")]
    public float idealCombatDistance = 4.2f;
    [Tooltip("Minimum distance before bot backs away.")]
    public float minCombatDistance = 2.2f;
    [Tooltip("Maximum distance to fire weapon.")]
    public float maxFireDistance = 14f;
    [Tooltip("Burst fire shot duration in seconds.")]
    public float burstDuration = 0.45f;
    [Tooltip("Pause between firing bursts in seconds.")]
    public float burstPause = 0.15f;

    [Header("Obstacle Avoidance / Navigation")]
    public LayerMask obstacleMask;
    public float raycastWhiskerDistance = 1.4f;

    // Components on this Bot Prefab
    private PlayerController playerController;
    private PlayerAiming playerAiming;
    private WeaponController weaponController;
    private PlayerHealth playerHealth;
    private BagManager bagManager;
    private CharacterAssembler characterAssembler;
    private Rigidbody2D rb;

    // AI State
    public AiBotState CurrentState { get; private set; } = AiBotState.Scavenge;
    private Transform currentTarget;
    private Vector2 lastKnownTargetPos;
    private Vector2 currentDestination;
    private Vector2 currentAimDirection = Vector2.right;
    private float stateTimer = 0f;
    private bool isShootingBurst = false;
    private float nextThinkTime = 0f;
    private float nextGrenadeTime = 0f;
    private float roomExploreTimer = 0f;
    private float nextDoorCheckTime = 0f;

    private RoomController botCurrentRoom;

    // ─── Adaptive Stuck Detection & Learning Memory ─────────────────────────
    private Vector2 lastTrackedPosition;
    private float stuckDuration = 0f;
    private int consecutiveStuckCount = 0;
    private Vector2 escapeSteerDirection = Vector2.zero;
    private float escapeTimer = 0f;

    private struct BlockedMemoryPoint
    {
        public Vector2 position;
        public float expireTime;
    }
    private List<BlockedMemoryPoint> blockedMemory = new List<BlockedMemoryPoint>();

    private void Awake()
    {
        gameObject.tag = "Bot";

        playerController   = GetComponent<PlayerController>();
        playerAiming       = GetComponent<PlayerAiming>();
        weaponController   = GetComponent<WeaponController>();
        playerHealth       = GetComponent<PlayerHealth>();
        bagManager         = GetComponent<BagManager>();
        characterAssembler = GetComponent<CharacterAssembler>();
        rb                 = GetComponent<Rigidbody2D>();

        visionBlockerMask = LayerMask.GetMask("Obstacle", "Wall");
        if (visionBlockerMask.value == 0) visionBlockerMask = LayerMask.GetMask("Obstacle");

        obstacleMask = LayerMask.GetMask("Obstacle", "Wall");
        if (obstacleMask.value == 0) obstacleMask = LayerMask.GetMask("Obstacle");

        lastTrackedPosition = transform.position;
    }

    private void Start()
    {
        // Randomize bot appearance
        if (characterAssembler != null && characterAssembler.availableSkins != null && characterAssembler.availableSkins.Length > 0)
        {
            int rndIndex = Random.Range(0, characterAssembler.availableSkins.Length);
            characterAssembler.ApplySkinByIndex(rndIndex);
        }

        // Bots spawn unarmed, start in Scavenge mode to find floor loot
        SetState(AiBotState.Scavenge);
        PickScavengeOrPatrolDestination();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (playerController != null) playerController.SetMoveInput(Vector2.zero);
            if (weaponController != null) weaponController.StopFiring();
            return;
        }

        stateTimer += Time.deltaTime;

        // Clean expired memories
        CleanExpiredBlockedMemory();

        // Monitor if bot is stuck in a corner
        MonitorStuckCondition();

        // Auto-pickup floor items in touching distance
        CollectNearbyFloorPickups();

        // High-Level AI Decision Making
        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + thinkInterval;
            ThinkAndMakeDecisions();
        }

        // Execute active state actions
        ExecuteState();

        // Apply Aim Direction to PlayerAiming
        if (playerAiming != null && currentAimDirection.sqrMagnitude > 0.01f)
        {
            playerAiming.SetAimInput(currentAimDirection);
        }
    }

    // ─── Adaptive Stuck Detection & Dynamic Escape ───────────────────────────

    private void MonitorStuckCondition()
    {
        // Don't flag stuck while in Idle
        if (CurrentState == AiBotState.Idle)
        {
            stuckDuration = 0f;
            lastTrackedPosition = transform.position;
            return;
        }

        float distMoved = Vector2.Distance(transform.position, lastTrackedPosition);
        if (distMoved < 0.12f)
        {
            stuckDuration += Time.deltaTime;
        }
        else
        {
            stuckDuration = 0f;
            lastTrackedPosition = transform.position;
            consecutiveStuckCount = 0;
        }

        // Stuck for more than 0.55 seconds!
        if (stuckDuration >= 0.55f && escapeTimer <= 0f)
        {
            TriggerAdaptiveUnstuck();
        }

        if (escapeTimer > 0f)
        {
            escapeTimer -= Time.deltaTime;
        }
    }

    private void TriggerAdaptiveUnstuck()
    {
        stuckDuration = 0f;
        consecutiveStuckCount++;

        // Learn this blocked position: store in short-term spatial memory
        RecordBlockedPoint(transform.position);
        if (currentDestination != Vector2.zero) RecordBlockedPoint(currentDestination);

        // Calculate 4 sequential escape vectors depending on consecutive stuck attempts:
        // 1st attempt: 90° right
        // 2nd attempt: 90° left
        // 3rd attempt: 135° reverse-diagonal
        // 4th attempt: 180° complete reverse
        Vector2 desiredForward = (currentDestination - (Vector2)transform.position).normalized;
        if (desiredForward.sqrMagnitude < 0.01f) desiredForward = currentAimDirection;

        float escapeAngle = consecutiveStuckCount switch
        {
            1 => 90f,
            2 => -90f,
            3 => 135f,
            _ => 180f
        };

        Vector2 trialEscape = Quaternion.Euler(0, 0, escapeAngle) * desiredForward;

        // If trial angle is blocked by a wall, find best open 8-direction corridor
        if (IsDirectionBlocked(trialEscape, 1.2f))
        {
            trialEscape = FindBestOpenDirection();
        }

        escapeSteerDirection = trialEscape.normalized;
        escapeTimer = 0.75f;

        // Re-route destination away from blocked zone
        PickSmartCorridorDestination(escapeSteerDirection);
        SetState(AiBotState.UnstuckEscape);
    }

    private bool IsDirectionBlocked(Vector2 direction, float distance)
    {
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, 0.35f, direction, distance, obstacleMask);
        return hit.collider != null && !hit.collider.isTrigger && hit.transform.root != transform.root;
    }

    private Vector2 FindBestOpenDirection()
    {
        // 8-Compass Direction Probe (0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°)
        float[] probeAngles = new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        Vector2 bestDir = -currentAimDirection;
        float maxClearance = 0f;

        Vector2 origin = transform.position;

        foreach (float ang in probeAngles)
        {
            Vector2 dir = Quaternion.Euler(0, 0, ang) * Vector2.right;
            RaycastHit2D hit = Physics2D.CircleCast(origin, 0.35f, dir, 8f, obstacleMask);

            float dist = (hit.collider != null && !hit.collider.isTrigger && hit.transform.root != transform.root) ? hit.distance : 8f;

            // Penalize directions leading to recently blocked points
            if (IsPointRecentlyBlocked(origin + dir * Mathf.Min(dist, 3f)))
            {
                dist *= 0.2f;
            }

            if (dist > maxClearance)
            {
                maxClearance = dist;
                bestDir = dir;
            }
        }

        return bestDir.normalized;
    }

    private void PickSmartCorridorDestination(Vector2 chosenDirection)
    {
        // Probe how far along chosenDirection is walkable
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, 0.35f, chosenDirection, 6f, obstacleMask);
        float safeDistance = (hit.collider != null && !hit.collider.isTrigger && hit.transform.root != transform.root)
            ? Mathf.Max(1.2f, hit.distance - 0.6f)
            : Random.Range(3.5f, 6.0f);

        currentDestination = (Vector2)transform.position + chosenDirection.normalized * safeDistance;
    }

    private void RecordBlockedPoint(Vector2 pt)
    {
        blockedMemory.Add(new BlockedMemoryPoint
        {
            position = pt,
            expireTime = Time.time + 8f // Remember this blocked area for 8 seconds
        });
    }

    private bool IsPointRecentlyBlocked(Vector2 pt)
    {
        foreach (var m in blockedMemory)
        {
            if (Vector2.Distance(pt, m.position) < 2.0f) return true;
        }
        return false;
    }

    private void CleanExpiredBlockedMemory()
    {
        for (int i = blockedMemory.Count - 1; i >= 0; i--)
        {
            if (Time.time >= blockedMemory[i].expireTime)
            {
                blockedMemory.RemoveAt(i);
            }
        }
    }

    // ─── Proximity Item Pickup ───────────────────────────────────────────────

    private void CollectNearbyFloorPickups()
    {
        ItemPickup[] pickups = FindObjectsOfType<ItemPickup>();
        foreach (var p in pickups)
        {
            if (p == null || p.itemData == null) continue;
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist <= 1.4f)
            {
                if (p.itemData.itemType == ItemType.Weapon)
                {
                    if (weaponController != null && p.itemData.prefab != null)
                    {
                        int slot = weaponController.GetWeaponInSlot(0) == null ? 0 : (weaponController.GetWeaponInSlot(1) == null ? 1 : 0);
                        weaponController.EquipWeaponToSlot(slot, p.itemData.prefab);
                        weaponController.SwitchToSlot(slot);
                        if (bagManager != null) bagManager.AddAmmo(p.itemData.ammoType, 90, 0);
                        p.TriggerDespawn();
                        break;
                    }
                }
                else if (p.itemData.itemType == ItemType.Ammo)
                {
                    if (bagManager != null)
                    {
                        bagManager.AddAmmo(p.itemData.ammoType, p.amount, 0);
                        p.TriggerDespawn();
                        break;
                    }
                }
                else if (p.itemData.itemType == ItemType.Grenade)
                {
                    if (bagManager != null)
                    {
                        bagManager.AddGrenade(p.itemData.grenadeType, p.amount, 0);
                        p.TriggerDespawn();
                        break;
                    }
                }
                else if (p.itemData.itemType == ItemType.Medikit)
                {
                    if (bagManager != null)
                    {
                        bagManager.AddMedikit(p.amount, 0);
                        p.TriggerDespawn();
                        break;
                    }
                }
                else if (p.itemData.itemType == ItemType.ProteinShake)
                {
                    if (bagManager != null)
                    {
                        bagManager.AddProteinShake(p.amount, 0);
                        p.TriggerDespawn();
                        break;
                    }
                }
            }
        }
    }

    // ─── Thinking & Decision Making ──────────────────────────────────────────

    private void ThinkAndMakeDecisions()
    {
        // Don't interrupt active escape maneuvers
        if (CurrentState == AiBotState.UnstuckEscape && escapeTimer > 0f) return;

        UpdateBotCurrentRoom();

        // 1. Health Management: Heal when HP < 45%
        if (playerHealth != null && playerHealth.GetCurrentHealth() <= 45)
        {
            if (bagManager != null && (bagManager.medikitCount > 0 || bagManager.proteinShakeCount > 0))
            {
                if (CurrentState != AiBotState.Heal && CurrentState != AiBotState.TakeCover)
                {
                    SetState(AiBotState.TakeCover);
                    return;
                }
            }
        }

        // 2. Scan for Visible Enemy / Player in Line of Sight
        Transform visibleTarget = ScanForVisibleTarget();
        if (visibleTarget != null)
        {
            currentTarget = visibleTarget;
            lastKnownTargetPos = visibleTarget.position;
            SetState(AiBotState.Engage);
            return;
        }

        // 3. If lost target during combat, investigate last seen position
        if (CurrentState == AiBotState.Engage)
        {
            currentDestination = lastKnownTargetPos;
            SetState(AiBotState.Investigate);
            return;
        }

        // 4. Room Navigation (Autonomous exploration of rooms & hallways)
        if (botCurrentRoom != null)
        {
            roomExploreTimer += thinkInterval;
            if (roomExploreTimer >= 5.5f)
            {
                ExitCurrentRoomAsBot();
                return;
            }
        }
        else
        {
            roomExploreTimer = 0f;

            if (Time.time >= nextDoorCheckTime)
            {
                nextDoorCheckTime = Time.time + 3.5f;
                DoorController nearbyDoor = FindNearestDoor();
                if (nearbyDoor != null && nearbyDoor.linkedRoom != null)
                {
                    float distToDoor = Vector2.Distance(transform.position, nearbyDoor.transform.position);
                    if (distToDoor < 2.0f)
                    {
                        EnterRoomAsBot(nearbyDoor);
                        return;
                    }
                    else if (distToDoor < 7.0f && Random.value < 0.45f)
                    {
                        currentDestination = nearbyDoor.transform.position;
                        SetState(AiBotState.Patrol);
                        return;
                    }
                }
            }
        }

        // 5. Scavenging: Seek floor weapons if unarmed
        bool hasWeapon = (weaponController != null && weaponController.CurrentWeapon != null);
        if (!hasWeapon)
        {
            Transform nearestLoot = FindNearestWeaponPickup();
            if (nearestLoot != null)
            {
                currentDestination = nearestLoot.position;
                SetState(AiBotState.Scavenge);
                return;
            }
        }
    }

    // ─── State Execution ─────────────────────────────────────────────────────

    private void ExecuteState()
    {
        switch (CurrentState)
        {
            case AiBotState.Idle:
                if (playerController != null) playerController.SetMoveInput(Vector2.zero);
                if (stateTimer >= Random.Range(0.6f, 1.4f))
                {
                    PickScavengeOrPatrolDestination();
                    SetState(AiBotState.Patrol);
                }
                break;

            case AiBotState.UnstuckEscape:
                // Move forcefully in computed escape direction
                MoveInDirection(escapeSteerDirection, 1.0f);
                if (escapeTimer <= 0f || stateTimer > 1.2f)
                {
                    PickScavengeOrPatrolDestination();
                    SetState(AiBotState.Patrol);
                }
                break;

            case AiBotState.Patrol:
            case AiBotState.Scavenge:
                MoveTowardsDestination(currentDestination, 0.85f);
                if (Vector2.Distance(transform.position, currentDestination) < 0.8f || stateTimer > 6f)
                {
                    PickScavengeOrPatrolDestination();
                    SetState(AiBotState.Patrol);
                }
                break;

            case AiBotState.Investigate:
                MoveTowardsDestination(currentDestination, 0.95f);
                AimTowards(currentDestination);
                if (Vector2.Distance(transform.position, currentDestination) < 1.0f || stateTimer > 4f)
                {
                    PickScavengeOrPatrolDestination();
                    SetState(AiBotState.Patrol);
                }
                break;

            case AiBotState.Engage:
                ExecuteCombat();
                break;

            case AiBotState.TakeCover:
                ExecuteTakeCoverAndHeal();
                break;
        }
    }

    private void ExecuteCombat()
    {
        if (currentTarget == null)
        {
            PickScavengeOrPatrolDestination();
            SetState(AiBotState.Patrol);
            return;
        }

        Vector2 targetPos = currentTarget.position;
        float dist = Vector2.Distance(transform.position, targetPos);

        // Aim directly at target's center
        AimTowards(targetPos);

        var weapon = weaponController != null ? weaponController.CurrentWeapon : null;

        if (weapon != null)
        {
            // Tactical Movement: Close in, Backpedal, or Strafe
            if (dist > idealCombatDistance)
            {
                MoveTowardsDestination(targetPos, 0.95f);
            }
            else if (dist < minCombatDistance)
            {
                // Backpedal to give player space
                Vector2 backDir = ((Vector2)transform.position - targetPos).normalized;
                MoveInDirection(backDir, 0.95f);
            }
            else
            {
                // Strafe perpendicular around the player to dodge and maintain spacing
                Vector2 strafeDir = Vector2.Perpendicular((targetPos - (Vector2)transform.position).normalized);
                if (Mathf.Sin(Time.time * 2.5f) > 0) strafeDir = -strafeDir;
                MoveInDirection(strafeDir, 0.75f);
            }

            // Check Ammo & Reload / Swap
            if (weapon.GetCurrentAmmo() <= 0)
            {
                int otherSlot = weaponController.GetCurrentSlot() == 0 ? 1 : 0;
                var otherWeapon = weaponController.GetWeaponInSlot(otherSlot);
                if (otherWeapon != null && otherWeapon.GetCurrentAmmo() > 0)
                {
                    weaponController.SwitchToSlot(otherSlot);
                }
                else
                {
                    weaponController.StartReload();
                }
            }
            else if (dist <= maxFireDistance && CanSeeTarget(currentTarget))
            {
                if (!isShootingBurst)
                {
                    StartCoroutine(CombatBurstRoutine());
                }
            }
            else if (!CanSeeTarget(currentTarget) && Time.time >= nextGrenadeTime)
            {
                if (bagManager != null && bagManager.GetGrenadeCount(bagManager.activeGrenadeType) > 0)
                {
                    nextGrenadeTime = Time.time + 4f;
                    weaponController.ThrowGrenade();
                }
            }
        }
        else
        {
            // Unarmed: Approach to punch range (~1.3m), punch, and maintain personal space
            if (dist > 1.35f)
            {
                MoveTowardsDestination(targetPos, 1.0f);
            }
            else if (dist < 0.95f)
            {
                // Step back slightly so we don't stick to the player
                Vector2 backDir = ((Vector2)transform.position - targetPos).normalized;
                MoveInDirection(backDir, 0.6f);
                if (weaponController != null) weaponController.StartFiring();
            }
            else
            {
                // Sidestep in punch range
                Vector2 strafeDir = Vector2.Perpendicular((targetPos - (Vector2)transform.position).normalized);
                MoveInDirection(strafeDir, 0.5f);
                if (weaponController != null) weaponController.StartFiring();
            }

            if (bagManager != null && bagManager.GetGrenadeCount(bagManager.activeGrenadeType) > 0 && Time.time >= nextGrenadeTime)
            {
                nextGrenadeTime = Time.time + 3f;
                weaponController?.ThrowGrenade();
            }
        }
    }

    private IEnumerator CombatBurstRoutine()
    {
        isShootingBurst = true;
        float burstEndTime = Time.time + burstDuration;

        while (Time.time < burstEndTime)
        {
            if (weaponController != null)
            {
                weaponController.StartFiring();
            }
            yield return new WaitForSeconds(0.09f);
        }

        if (weaponController != null) weaponController.StopFiring();
        yield return new WaitForSeconds(burstPause);

        isShootingBurst = false;
    }

    private void ExecuteTakeCoverAndHeal()
    {
        if (bagManager != null)
        {
            if (bagManager.medikitCount > 0) bagManager.UseMedikit();
            else if (bagManager.proteinShakeCount > 0) bagManager.UseProteinShake();
        }

        if (currentTarget != null)
        {
            Vector2 away = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
            MoveInDirection(away, 1f);
        }

        if (stateTimer >= 2f)
        {
            PickScavengeOrPatrolDestination();
            SetState(AiBotState.Patrol);
        }
    }

    // ─── Navigation, Obstacle Steering & Separation ──────────────────────────

    private void MoveTowardsDestination(Vector2 destination, float speedMultiplier)
    {
        Vector2 directDir = (destination - (Vector2)transform.position).normalized;
        Vector2 steerDir = ComputeObstacleAvoidanceDirection(directDir);

        MoveInDirection(steerDir, speedMultiplier);
        if (currentTarget == null)
        {
            AimTowards((Vector2)transform.position + steerDir * 3f);
        }
    }

    private void MoveInDirection(Vector2 direction, float speedMultiplier)
    {
        if (playerController != null)
        {
            playerController.SetMoveInput(direction * speedMultiplier);
        }
        else if (rb != null)
        {
            rb.velocity = direction * speedMultiplier * 5f;
        }
    }

    private Vector2 ComputeObstacleAvoidanceDirection(Vector2 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.001f) return Vector2.zero;

        Vector2 origin = (Vector2)transform.position;

        // 1. Bot Separation Force (prevents bots clumping together)
        Vector2 separationForce = Vector2.zero;
        Collider2D[] nearby = Physics2D.OverlapCircleAll(origin, 1.8f);
        foreach (var col in nearby)
        {
            if (col == null || col.transform.root == transform.root || col.isTrigger) continue;
            if (col.CompareTag("Bot") || col.GetComponent<AiBotController>() != null)
            {
                Vector2 diff = origin - (Vector2)col.transform.position;
                float d = diff.magnitude;
                if (d > 0.01f && d < 1.8f)
                {
                    separationForce += (diff.normalized / d) * 1.6f;
                }
            }
        }

        // 2. Personal Space Repulsion from Human Player
        if (PlayerController.LocalPlayer != null)
        {
            Vector2 pPos = PlayerController.LocalPlayer.transform.position;
            float pDist = Vector2.Distance(origin, pPos);
            if (pDist < 2.2f && pDist > 0.01f)
            {
                separationForce += ((origin - pPos).normalized / pDist) * 2.0f;
            }
        }

        // 2. Multi-Whisker CircleCast Avoidance
        float[] testAngles = new float[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f, 135f, -135f };
        Vector2 bestDir = desiredDir;
        float bestScore = -9999f;

        foreach (float ang in testAngles)
        {
            Vector2 castDir = Quaternion.Euler(0, 0, ang) * desiredDir;
            RaycastHit2D hit = Physics2D.CircleCast(origin, 0.35f, castDir, raycastWhiskerDistance, obstacleMask);

            float clearance = (hit.collider != null && !hit.collider.isTrigger && hit.transform.root != transform.root) ? hit.distance : raycastWhiskerDistance;
            float alignment = Vector2.Dot(castDir, desiredDir);
            float score = (clearance / raycastWhiskerDistance) * 2.5f + alignment;

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = castDir;
            }
        }

        Vector2 finalSteer = (bestDir + separationForce).normalized;
        return finalSteer;
    }

    private void AimTowards(Vector2 targetPos)
    {
        Vector2 targetDir = (targetPos - (Vector2)transform.position).normalized;
        if (targetDir.sqrMagnitude > 0.001f)
        {
            currentAimDirection = targetDir;
        }
    }

    private void PickScavengeOrPatrolDestination()
    {
        // 1. If unarmed, prioritize nearby floor weapon that is NOT in blocked memory
        if (weaponController == null || weaponController.CurrentWeapon == null)
        {
            Transform nearestLoot = FindNearestWeaponPickup();
            if (nearestLoot != null && !IsPointRecentlyBlocked(nearestLoot.position))
            {
                currentDestination = nearestLoot.position;
                return;
            }
        }

        // 2. Try sampling from walkable map zones (excluding recently blocked areas)
        if (OfflineManager.Instance != null && OfflineManager.Instance.walkableZones != null && OfflineManager.Instance.walkableZones.Count > 0)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (OfflineManager.Instance.TryFindValidPointInZones(OfflineManager.Instance.walkableZones, out Vector3 zonePos))
                {
                    if (!IsPointRecentlyBlocked(zonePos))
                    {
                        currentDestination = zonePos;
                        return;
                    }
                }
            }
        }

        // 3. Fallback: Probe best open corridor direction with radar
        Vector2 openDir = FindBestOpenDirection();
        PickSmartCorridorDestination(openDir);
    }

    // ─── Target, Floor & Room Helpers ────────────────────────────────────────

    private Transform ScanForVisibleTarget()
    {
        Transform closest = null;
        float minDistance = sightRadius;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p == null || p.gameObject == this.gameObject) continue;

            var health = p.GetComponent<PlayerHealth>() ?? p.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsDead) continue;

            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < minDistance && CanSeeTarget(p.transform))
            {
                minDistance = dist;
                closest = p.transform;
            }
        }

        return closest;
    }

    private bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 targetPos = target.position;
        Vector2 dirToTarget = (targetPos - origin).normalized;
        float dist = Vector2.Distance(origin, targetPos);

        if (dist <= proximityRadius)
        {
            RaycastHit2D hitProx = Physics2D.Raycast(origin, dirToTarget, dist, visionBlockerMask);
            return !hitProx || hitProx.collider.isTrigger || hitProx.transform == target || hitProx.transform.IsChildOf(target);
        }

        if (dist > sightRadius) return false;

        RaycastHit2D hit = Physics2D.Raycast(origin, dirToTarget, dist, visionBlockerMask);
        if (hit.collider != null && !hit.collider.isTrigger && hit.transform != target && !hit.transform.IsChildOf(target))
        {
            return false;
        }

        return true;
    }

    private Transform FindNearestWeaponPickup()
    {
        ItemPickup[] pickups = FindObjectsOfType<ItemPickup>();
        Transform nearest = null;
        float minDist = 25f;

        foreach (var p in pickups)
        {
            if (p == null || p.itemData == null || p.itemData.itemType != ItemType.Weapon) continue;
            if (IsPointRecentlyBlocked(p.transform.position)) continue;

            float d = Vector2.Distance(transform.position, p.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = p.transform;
            }
        }

        return nearest;
    }

    private void UpdateBotCurrentRoom()
    {
        botCurrentRoom = GetRoomForPosition(transform.position);
    }

    private RoomController GetRoomForPosition(Vector3 pos)
    {
        RoomController[] rooms = FindObjectsOfType<RoomController>();
        Vector2 p = pos;
        RoomController closest = null;
        float minDist = 14f;

        foreach (var r in rooms)
        {
            if (r == null) continue;
            if (r.exitBoxCollider != null && r.exitBoxCollider.bounds.Contains(p)) return r;

            Collider2D col = r.GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(p)) return r;

            float d = Vector2.Distance(p, r.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = r;
            }
        }
        return closest;
    }

    private void ExitCurrentRoomAsBot()
    {
        DoorController door = null;
        if (botCurrentRoom != null)
        {
            door = botCurrentRoom.linkedDoor;
            if (door == null) door = FindDoorForRoom(botCurrentRoom.roomId);
            if (door == null)
            {
                DoorController[] allDoors = FindObjectsOfType<DoorController>();
                foreach (var d in allDoors)
                {
                    if (d != null && (d.linkedRoom == botCurrentRoom || d.roomId == botCurrentRoom.roomId))
                    {
                        door = d;
                        break;
                    }
                }
            }
        }

        if (door == null)
        {
            door = FindNearestDoor();
        }

        if (door != null)
        {
            Vector3 exitPos = door.transform.position;
            if (playerController != null) playerController.Teleport(exitPos);
            else transform.position = exitPos;

            botCurrentRoom = null;
            roomExploreTimer = 0f;
            PickScavengeOrPatrolDestination();
            SetState(AiBotState.Patrol);
        }
    }

    private void EnterRoomAsBot(DoorController door)
    {
        if (door != null && door.linkedRoom != null)
        {
            Vector3 targetPos = door.linkedRoom.GetSpawnPosition();
            if (playerController != null) playerController.Teleport(targetPos);
            else transform.position = targetPos;

            botCurrentRoom = door.linkedRoom;
            roomExploreTimer = 0f;
            PickScavengeOrPatrolDestination();
            SetState(AiBotState.Patrol);
        }
    }

    private DoorController FindDoorForRoom(string roomId)
    {
        DoorController[] doors = FindObjectsOfType<DoorController>();
        foreach (var d in doors)
        {
            if (d != null && d.roomId == roomId) return d;
        }
        return null;
    }

    private DoorController FindNearestDoor()
    {
        DoorController[] doors = FindObjectsOfType<DoorController>();
        DoorController nearest = null;
        float minDist = 25f;
        foreach (var d in doors)
        {
            if (d == null) continue;
            float dist = Vector2.Distance(transform.position, d.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = d;
            }
        }
        return nearest;
    }

    public void SetState(AiBotState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        stateTimer = 0f;
    }
}
