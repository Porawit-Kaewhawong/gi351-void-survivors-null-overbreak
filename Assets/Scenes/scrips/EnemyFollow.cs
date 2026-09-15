using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class EnemyFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 12f;
    public float deceleration = 18f;
    public float rotationSpeed = 10f;

    [Header("Attack")]
    public float attackDistance = 1.2f;

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

    private CharacterController controller;

    private Vector3 moveVelocity;
    private float verticalVelocity;

    private bool jumping;
    private Vector3 jumpTarget;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private bool respawning;
    private float respawnTimer;

    private void Awake()
    {
        // Get the CharacterController used for movement.
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Save the original spawn position.
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // Automatically find the player by tag.
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (respawning)
        {
            HandleRespawn();
            return;
        }

        if (player == null)
            return;

        // Respawn when the enemy falls too far.
        if (transform.position.y <
            spawnPosition.y - fallDistance)
        {
            StartRespawn();
            return;
        }

        CheckAttack();

        if (jumping)
        {
            MoveDuringJump();
        }
        else
        {
            FollowPlayer();
            CheckJump();
        }

        ApplyGravity();
    }

    private void FollowPlayer()
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance = direction.magnitude;

        // Stop only when close enough to attack.
        if (distance <= attackDistance)
        {
            SlowDown();
            return;
        }

        if (distance < 0.05f)
            return;

        direction.Normalize();

        // Check whether the next step has safe ground.
        if (!HasGroundAhead(direction))
        {
            // Try to find a platform to jump onto.
            if (FindLandingPosition(
                direction,
                out Vector3 landingPosition))
            {
                StartJump(landingPosition);
                return;
            }

            // Stop at the edge instead of falling.
            SlowDown();
            return;
        }

        Vector3 targetVelocity =
            direction * moveSpeed;

        moveVelocity =
            Vector3.MoveTowards(
                moveVelocity,
                targetVelocity,
                acceleration *
                Time.deltaTime
            );

        controller.Move(
            moveVelocity *
            Time.deltaTime
        );

        RotateSmoothly(direction);
    }

    private void MoveDuringJump()
    {
        Vector3 direction =
            jumpTarget - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            direction.Normalize();

            Vector3 targetVelocity =
                direction * moveSpeed;

            moveVelocity =
                Vector3.MoveTowards(
                    moveVelocity,
                    targetVelocity,
                    acceleration *
                    Time.deltaTime
                );

            RotateSmoothly(direction);
        }

        controller.Move(
            moveVelocity *
            Time.deltaTime
        );
    }

    private void CheckJump()
    {
        if (!controller.isGrounded)
            return;

        if (verticalVelocity > 0f)
            return;

        CharacterController playerController =
            player.GetComponent<CharacterController>();

        // Follow the player's jump.
        if (playerController != null &&
            !playerController.isGrounded)
        {
            if (FindLandingPosition(
                DirectionToPlayer(),
                out Vector3 landingPosition))
            {
                StartJump(landingPosition);
            }
        }
    }

    private bool HasGroundAhead(Vector3 direction)
    {
        Vector3 origin =
            transform.position +
            direction * 0.8f;

        origin.y += 0.5f;

        // Check for ground directly in front.
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

        // Search farther ahead for another ground surface.
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
                hit.point.y -
                transform.position.y;

            // Only accept reachable ground.
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

    private void StartJump(Vector3 target)
    {
        jumping = true;
        jumpTarget = target;

        // Calculate vertical jump velocity.
        verticalVelocity =
            Mathf.Sqrt(
                jumpHeight *
                -2f *
                gravity
            );
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded &&
            verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            jumping = false;
        }

        verticalVelocity +=
            gravity *
            Time.deltaTime;

        controller.Move(
            Vector3.up *
            verticalVelocity *
            Time.deltaTime
        );
    }

    private void SlowDown()
    {
        moveVelocity =
            Vector3.MoveTowards(
                moveVelocity,
                Vector3.zero,
                deceleration *
                Time.deltaTime
            );

        controller.Move(
            moveVelocity *
            Time.deltaTime
        );
    }

    private void RotateSmoothly(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.deltaTime
            );
    }

    private Vector3 DirectionToPlayer()
    {
        Vector3 direction =
            player.position -
            transform.position;

        direction.y = 0f;

        return direction.normalized;
    }

    private void CheckAttack()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        // Kill the player when the enemy reaches them.
        if (distance <= attackDistance)
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
        }
    }

    private void StartRespawn()
    {
        // Start the respawn countdown.
        respawning = true;
        respawnTimer = respawnDelay;

        moveVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumping = false;
    }

    private void HandleRespawn()
    {
        respawnTimer -= Time.deltaTime;

        if (respawnTimer > 0f)
            return;

        // Reset the enemy at its original spawn position.
        controller.enabled = false;

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        controller.enabled = true;

        moveVelocity = Vector3.zero;
        verticalVelocity = -2f;
        jumping = false;

        respawning = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Show the attack distance.
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackDistance
        );

        // Show the maximum jump distance.
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            maxJumpDistance
        );

        // Show the fall limit.
        Gizmos.color = Color.blue;

        Vector3 fallPosition =
            Application.isPlaying
                ? new Vector3(
                    spawnPosition.x,
                    spawnPosition.y - fallDistance,
                    spawnPosition.z
                )
                : transform.position;

        Gizmos.DrawLine(
            fallPosition + Vector3.left * 2f,
            fallPosition + Vector3.right * 2f
        );
    }
}