using UnityEngine;

public class EnemyFollower : EnemyMovement
{
    [Header("Void Rescue Settings")]
    [Tooltip("World Y height below which the enemy is considered in the void.")]
    [SerializeField] private float voidYThreshold = -10f;

    [Tooltip("Minimum distance away from the player when finding ground.")]
    [SerializeField] private float minRescueRadius = 4f;

    [Tooltip("Maximum distance away from the player when finding ground.")]
    [SerializeField] private float maxRescueRadius = 12f;

    protected override void Awake()
    {
        base.Awake();
        // Prevent EnemyBase/EnemyMovement from destroying this enemy when falling
        destroyOnFall = false;
    }

    private void Start()
    {
        // Immediately verify ground on spawn; if over air, rescue to a ring around player
        if (transform.position.y < voidYThreshold || !HasGroundUnderneath(transform.position))
        {
            RescueFromVoid();
        }
    }

    protected override void Update()
    {
        base.Update(); // Updates attack cooldown timer

        // Detect if enemy has fallen below threshold or triggered standard fall
        if (transform.position.y < voidYThreshold || CheckFall())
        {
            RescueFromVoid();
            return;
        }

        if (IsRespawning()) return;

        FindPlayer();
        if (playerTransform == null) return;

        // Try attacking if within range
        TryAttackPlayer();

        if (jumping)
        {
            MoveJump();
        }
        else
        {
            FollowPlayer();
            CheckPlayerJump();
        }

        ApplyGravity();
    }

    /// <summary>
    /// Finds solid ground in an outer ring around the player and teleports the enemy there.
    /// </summary>
    public void RescueFromVoid()
    {
        FindPlayer();

        Vector3 targetGroundPosition;

        // Step 1: Find ground in a ring around the player (minRescueRadius to maxRescueRadius)
        if (playerTransform != null && TryFindGroundInRingAround(playerTransform.position, out targetGroundPosition))
        {
            TeleportTo(targetGroundPosition);
            return;
        }

        // Step 2: Fallback to ground around initial spawn position
        if (TryFindGroundInRingAround(spawnPosition, out targetGroundPosition))
        {
            TeleportTo(targetGroundPosition);
            return;
        }

        // Step 3: Emergency fallback offset horizontally from player (never directly on top)
        if (playerTransform != null)
        {
            Vector3 offsetDir = playerTransform.forward * -minRescueRadius; // Spawns behind player
            TeleportTo(playerTransform.position + offsetDir + Vector3.up * 0.5f);
        }
    }

    /// <summary>
    /// Searches for solid ground in a ring (donut shape) surrounding the target center point.
    /// </summary>
    private bool TryFindGroundInRingAround(Vector3 centerPoint, out Vector3 safePosition)
    {
        safePosition = Vector3.zero;
        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;

        // Sample random directions at distances strictly between minRescueRadius and maxRescueRadius
        for (int i = 0; i < 15; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minRescueRadius, maxRescueRadius);

            Vector3 samplePos = centerPoint + new Vector3(randomDir.x * randomDistance, 10f, randomDir.y * randomDistance);

            if (Physics.Raycast(samplePos, Vector3.down, out RaycastHit hit, 30f, layerMaskToUse, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger)
                {
                    safePosition = hit.point + GetCapsuleOffset();
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasGroundUnderneath(Vector3 position)
    {
        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;
        return Physics.Raycast(position + Vector3.up * 1f, Vector3.down, 15f, layerMaskToUse, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetCapsuleOffset()
    {
        float offset = 0.1f;
        if (controller != null)
        {
            offset += (controller.height * 0.5f) - controller.center.y;
        }
        return Vector3.up * offset;
    }

    /// <summary>
    /// Teleports the enemy, resetting CharacterController physics momentum.
    /// </summary>
    private void TeleportTo(Vector3 targetPosition)
    {
        if (controller != null) controller.enabled = false;

        transform.position = targetPosition;
        moveVelocity = Vector3.zero;
        verticalVelocity = -2f;
        jumping = false;

        // Re-anchor spawn position to new ground height
        spawnPosition = targetPosition;

        if (controller != null) controller.enabled = true;

        Debug.Log($"[{gameObject.name}] Rescued from void -> Teleported near player at {targetPosition}");
    }

    private void FollowPlayer()
    {
        Vector3 direction = playerTransform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= attackDistance * attackDistance)
        {
            SlowDown();
            return;
        }

        MoveTo(direction);
    }

    private void CheckPlayerJump()
    {
        if (playerController == null) return;

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null && !cc.isGrounded)
        {
            Vector3 direction = playerTransform.position - transform.position;
            TryJump(direction);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxJumpDistance);

        // Draw inner and outer rescue ring bounds around player position in editor
        if (playerTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerTransform.position, minRescueRadius);
            Gizmos.DrawWireSphere(playerTransform.position, maxRescueRadius);
        }
    }
}