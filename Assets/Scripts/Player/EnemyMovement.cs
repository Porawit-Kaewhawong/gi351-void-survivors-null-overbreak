using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 12f;
    public float deceleration = 18f;
    public float rotationSpeed = 10f;

    [Header("Jump")]
    public float jumpHeight = 2.5f;
    public float gravity = -20f;
    public float maxJumpDistance = 6f;
    public float jumpCheckHeight = 5f;

    [Header("Ground")]
    public LayerMask groundLayer;

    [Header("Respawn")]
    public float fallDistance = 20f;
    public float respawnDelay = 0.5f;

    protected CharacterController controller;

    protected Vector3 moveVelocity;
    protected float verticalVelocity;

    protected bool jumping;
    protected Vector3 jumpTarget;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private bool respawning;
    private float respawnTimer;

    protected virtual void Awake()
    {
        // Get the CharacterController used by the enemy.
        controller = GetComponent<CharacterController>();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

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

        moveVelocity = Vector3.MoveTowards(
            moveVelocity,
            targetVelocity,
            acceleration * Time.deltaTime
        );

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

            moveVelocity = Vector3.MoveTowards(
                moveVelocity,
                targetVelocity,
                acceleration * Time.deltaTime
            );

            RotateSmoothly(direction);
        }

        controller.Move(moveVelocity * Time.deltaTime);
    }

    protected void TryJump(Vector3 direction)
    {
        if (!controller.isGrounded || verticalVelocity > 0f)
            return;

        if (FindLandingPosition(direction, out Vector3 landing))
            StartJump(landing);
    }

    private bool HasGroundAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + direction * 0.8f;
        origin.y += 0.5f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            2f,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool FindLandingPosition(
        Vector3 direction,
        out Vector3 landingPosition)
    {
        landingPosition = Vector3.zero;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return false;

        direction.Normalize();

        for (float distance = 1f;
             distance <= maxJumpDistance;
             distance += 0.5f)
        {
            Vector3 checkPosition =
                transform.position +
                direction * distance;

            checkPosition.y += jumpCheckHeight;

            if (!Physics.Raycast(
                checkPosition,
                Vector3.down,
                out RaycastHit hit,
                jumpCheckHeight * 2f,
                groundLayer,
                QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float heightDifference =
                hit.point.y - transform.position.y;

            if (heightDifference < -2f ||
                heightDifference > jumpHeight)
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

        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );
    }

    protected void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            jumping = false;
        }

        verticalVelocity += gravity * Time.deltaTime;

        controller.Move(
            Vector3.up *
            verticalVelocity *
            Time.deltaTime
        );
    }

    protected void SlowDown()
    {
        moveVelocity = Vector3.MoveTowards(
            moveVelocity,
            Vector3.zero,
            deceleration * Time.deltaTime
        );

        controller.Move(moveVelocity * Time.deltaTime);
    }

    protected void RotateSmoothly(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    protected bool CheckFall()
    {
        if (transform.position.y <
            spawnPosition.y - fallDistance)
        {
            StartRespawn();
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
        if (!respawning)
            return false;

        respawnTimer -= Time.deltaTime;

        if (respawnTimer > 0f)
            return true;

        controller.enabled = false;

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        controller.enabled = true;

        moveVelocity = Vector3.zero;
        verticalVelocity = -2f;
        jumping = false;
        respawning = false;

        return false;
    }
}
