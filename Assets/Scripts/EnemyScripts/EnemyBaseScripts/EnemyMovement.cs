using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public abstract class EnemyMovement : EnemyBase
{
    [Header("Movement Acceleration & Rotation")]
    public float acceleration = 12f;
    public float deceleration = 18f;
    public float rotationSpeed = 10f;

    [Tooltip("Disable if using FaceCamera or 2D billboard sprites on this enemy.")]
    public bool rotateTowardsMovement = true;

    [Header("Jump Settings")]
    public float jumpHeight = 2.5f;
    public float gravity = -20f;
    public float maxJumpDistance = 6f;
    public float jumpCheckHeight = 5f;

    [Header("Ground & Collision")]
    [Tooltip("Select the layer(s) for ground/platforms.")]
    public LayerMask groundLayer;

    [Header("Respawn Settings")]
    public float respawnDelay = 0.5f;
    public float minRespawnRadius = 12f;
    public float maxRespawnRadius = 22f;
    public int maxPlatformSearchAttempts = 15;

    protected CharacterController controller;
    protected Vector3 moveVelocity;
    protected float verticalVelocity;

    protected bool jumping;
    protected Vector3 jumpTarget;

    private bool respawning;
    private float respawnTimer;

    protected override void Awake()
    {
        base.Awake();
        controller = GetComponent<CharacterController>();

        // Check using generic type parameter
        if (GetComponent<FaceCamera>() != null || GetComponentInChildren<FaceCamera>() != null)
        {
            rotateTowardsMovement = false;
        }
    }

    protected override void Update()
    {
        base.Update();
    }

    // --- LOCOMOTION ---

    protected void MoveTo(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.Normalize();

        if (!HasGroundAhead(direction))
        {
            if (FindLandingPosition(direction, out Vector3 landing))
            {
                StartJump(landing);
                return;
            }

            SlowDown();
            return;
        }

        Vector3 targetVelocity = direction * moveSpeed;
        moveVelocity = Vector3.MoveTowards(moveVelocity, targetVelocity, acceleration * Time.deltaTime);

        controller.Move(moveVelocity * Time.deltaTime);
        RotateSmoothly(direction);
    }

    protected void MoveJump()
    {
        Vector3 direction = jumpTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            direction.Normalize();
            Vector3 targetVelocity = direction * moveSpeed;
            moveVelocity = Vector3.MoveTowards(moveVelocity, targetVelocity, acceleration * Time.deltaTime);
            RotateSmoothly(direction);
        }

        controller.Move(moveVelocity * Time.deltaTime);
    }

    protected void TryJump(Vector3 direction)
    {
        if (!controller.isGrounded || verticalVelocity > 0f) return;

        if (FindLandingPosition(direction, out Vector3 landing))
        {
            StartJump(landing);
        }
    }

    private bool HasGroundAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + direction * 0.8f;
        origin.y += 0.5f;

        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;
        return Physics.Raycast(origin, Vector3.down, 2f, layerMaskToUse, QueryTriggerInteraction.Ignore);
    }

    private bool FindLandingPosition(Vector3 direction, out Vector3 landingPosition)
    {
        landingPosition = Vector3.zero;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f) return false;

        direction.Normalize();

        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;

        for (float distance = 1f; distance <= maxJumpDistance; distance += 0.5f)
        {
            Vector3 checkPosition = transform.position + direction * distance;
            checkPosition.y += jumpCheckHeight;

            if (!Physics.Raycast(checkPosition, Vector3.down, out RaycastHit hit, jumpCheckHeight * 2f, layerMaskToUse, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float heightDifference = hit.point.y - transform.position.y;
            if (heightDifference < -2f || heightDifference > jumpHeight)
            {
                continue;
            }

            landingPosition = hit.point;
            return true;
        }

        return false;
    }

    protected void StartJump(Vector3 target)
    {
        jumping = true;
        jumpTarget = target;
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    protected void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            jumping = false;
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    protected void SlowDown()
    {
        moveVelocity = Vector3.MoveTowards(moveVelocity, Vector3.zero, deceleration * Time.deltaTime);
        controller.Move(moveVelocity * Time.deltaTime);
    }

    protected void RotateSmoothly(Vector3 direction)
    {
        if (!rotateTowardsMovement || direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // --- FALL & DYNAMIC RESPAWN ---

    protected bool CheckFall()
    {
        if (transform.position.y < spawnPosition.y - fallDistance)
        {
            if (destroyOnFall)
            {
                Die();
            }
            else
            {
                StartRespawn();
            }
            return true;
        }

        return false;
    }

    private void StartRespawn()
    {
        respawning = true;
        respawnTimer = respawnDelay;

        moveVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumping = false;
    }

    protected bool IsRespawning()
    {
        if (!respawning) return false;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer > 0f) return true;

        Vector3 targetRespawnPosition = spawnPosition;
        if (playerTransform != null && TryFindPlatformNearPlayer(out Vector3 nearPlayerPosition))
        {
            targetRespawnPosition = nearPlayerPosition;
        }

        controller.enabled = false;
        transform.position = targetRespawnPosition;
        transform.rotation = spawnRotation;
        controller.enabled = true;

        moveVelocity = Vector3.zero;
        verticalVelocity = -2f;
        jumping = false;
        respawning = false;

        return false;
    }

    private bool TryFindPlatformNearPlayer(out Vector3 platformPosition)
    {
        platformPosition = Vector3.zero;
        int layerMaskToUse = (groundLayer.value == 0) ? ~0 : groundLayer.value;

        for (int i = 0; i < maxPlatformSearchAttempts; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minRespawnRadius, maxRespawnRadius);

            Vector3 samplePosition = playerTransform.position + new Vector3(randomDir.x * randomDist, 10f, randomDir.y * randomDist);

            if (Physics.Raycast(samplePosition, Vector3.down, out RaycastHit hit, 30f, layerMaskToUse, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger)
                {
                    platformPosition = hit.point + Vector3.up * 0.5f;
                    return true;
                }
            }
        }

        return false;
    }
}