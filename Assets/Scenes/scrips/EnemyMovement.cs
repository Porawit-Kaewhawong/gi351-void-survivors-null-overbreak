using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 20f;
    public float rotationSpeed = 10f;
    public float stoppingDistance = 1.2f;

    [Header("Jump")]
    public float jumpHeight = 2f;
    public float jumpDuration = 0.5f;
    public float maxJumpDistance = 6f;
    public float maxJumpHeightDifference = 3f;
    public float jumpCooldown = 0.5f;

    protected NavMeshAgent agent;

    private bool jumping;
    private float jumpCooldownTimer;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = rotationSpeed;
        agent.stoppingDistance = stoppingDistance;

        agent.autoBraking = true;

        // We use our own JumpTo() instead of OffMeshLink movement.
        agent.autoTraverseOffMeshLink = false;

        // EnemyMovement controls rotation manually.
        agent.updateRotation = false;
    }

    protected virtual void Update()
    {
        UpdateJumpCooldown();
        RotateToMovement();
    }

    // -------------------------
    // Movement
    // -------------------------

    protected void MoveTo(Vector3 targetPosition)
    {
        if (jumping)
            return;

        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.SetDestination(targetPosition);
    }

    protected void StopMoving()
    {
        if (!agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    protected void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;

        if (agent != null)
            agent.speed = moveSpeed;
    }

    // -------------------------
    // Path Check
    // -------------------------

    protected bool HasCompletePathTo(Vector3 targetPosition)
    {
        if (!agent.isOnNavMesh)
            return false;

        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(targetPosition, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    // -------------------------
    // Jump Check
    // -------------------------

    protected bool CanJumpTo(Vector3 targetPosition)
    {
        if (jumping)
            return false;

        if (jumpCooldownTimer > 0f)
            return false;

        float horizontalDistance = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(targetPosition.x, 0f, targetPosition.z)
        );

        float heightDifference = Mathf.Abs(
            targetPosition.y - transform.position.y
        );

        if (horizontalDistance > maxJumpDistance)
            return false;

        if (heightDifference > maxJumpHeightDifference)
            return false;

        return true;
    }

    // -------------------------
    // Jump
    // -------------------------

    protected void JumpTo(Vector3 targetPosition)
    {
        if (!CanJumpTo(targetPosition))
            return;

        // Lock jumping immediately to prevent multiple jump calls.
        jumping = true;

        // Start cooldown immediately.
        jumpCooldownTimer = jumpCooldown;

        StartCoroutine(JumpRoutine(targetPosition));
    }

    private IEnumerator JumpRoutine(Vector3 targetPosition)
    {
        if (!agent.isOnNavMesh)
        {
            jumping = false;
            yield break;
        }

        Vector3 startPosition = transform.position;

        // Stop NavMesh movement while performing the custom jump.
        agent.isStopped = true;
        agent.ResetPath();
        agent.updatePosition = false;

        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // Smooth horizontal movement.
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            // Create an arc using sine.
            float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            position.y += arc;

            transform.position = position;

            yield return null;
        }

        // Make sure the enemy lands exactly at the target.
        transform.position = targetPosition;

        // Sync NavMeshAgent with the new position.
        agent.Warp(targetPosition);

        agent.updatePosition = true;
        agent.isStopped = false;

        jumping = false;
    }

    // -------------------------
    // Cooldown
    // -------------------------

    private void UpdateJumpCooldown()
    {
        if (jumpCooldownTimer <= 0f)
            return;

        jumpCooldownTimer -= Time.deltaTime;

        if (jumpCooldownTimer < 0f)
            jumpCooldownTimer = 0f;
    }

    // -------------------------
    // Rotation
    // -------------------------

    private void RotateToMovement()
    {
        if (jumping)
            return;

        if (!agent.isOnNavMesh)
            return;

        Vector3 direction = agent.desiredVelocity;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    // -------------------------
    // Public Status
    // -------------------------

    protected bool IsJumping()
    {
        return jumping;
    }

    protected bool IsReady()
    {
        return agent != null &&
               agent.isOnNavMesh &&
               !jumping;
    }
}