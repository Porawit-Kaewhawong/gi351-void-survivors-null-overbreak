using UnityEngine;

public class EnemyFollow : EnemyMovement
{
    [Header("Target")]
    public Transform player;

    [Header("Follow")]
    public float followRange = 100f;

    [Tooltip("Enemy speed compared to the player's speed.")]
    [Range(0.1f, 1f)]
    public float speedRatio = 0.8f;

    [Header("Attack")]
    public float attackDistance = 1.2f;

    [Header("Jump Control")]
    [Tooltip("Prevents the enemy from immediately jumping again after landing.")]
    public float jumpRetryDelay = 1.0f;

    private PlayerController playerController;
    private bool playerDead;

    // Prevents repeated jump commands.
    private bool hasJumpedForCurrentObstacle;

    private float jumpRetryTimer;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        FindPlayer();
        UpdateSpeedFromPlayer();
    }

    private void Update()
    {
        if (playerDead)
            return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        // Run base movement logic.
        base.Update();

        UpdateJumpRetryTimer();
        UpdateSpeedFromPlayer();

        FollowPlayer();
        CheckAttack();
    }

    // -------------------------
    // Find Player
    // -------------------------

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogWarning(
                "EnemyFollow could not find a GameObject " +
                "with the Player tag."
            );

            return;
        }

        player = playerObject.transform;

        playerController =
            playerObject.GetComponent<PlayerController>();
    }

    // -------------------------
    // Enemy Speed
    // -------------------------

    private void UpdateSpeedFromPlayer()
    {
        if (playerController == null)
            return;

        float enemySpeed =
            playerController.moveSpeed *
            speedRatio;

        SetMoveSpeed(enemySpeed);
    }

    // -------------------------
    // Follow Player
    // -------------------------

    private void FollowPlayer()
    {
        if (!IsReady())
            return;

        if (IsJumping())
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (distance > followRange)
        {
            StopMoving();
            return;
        }

        // If a normal path exists,
        // continue normal movement.
        bool hasPath =
            HasCompletePathTo(player.position);

        if (hasPath)
        {
            // We have successfully reached
            // a NavMesh-connected area again.
            hasJumpedForCurrentObstacle = false;

            MoveTo(player.position);

            return;
        }

        // No path -> probably a gap or obstacle.
        // Do not immediately jump again.
        if (hasJumpedForCurrentObstacle)
            return;

        if (jumpRetryTimer > 0f)
            return;

        if (CanJumpTo(player.position))
        {
            hasJumpedForCurrentObstacle = true;

            jumpRetryTimer = jumpRetryDelay;

            JumpTo(player.position);
        }
    }

    // -------------------------
    // Jump Retry Timer
    // -------------------------

    private void UpdateJumpRetryTimer()
    {
        if (jumpRetryTimer <= 0f)
            return;

        jumpRetryTimer -= Time.deltaTime;

        if (jumpRetryTimer < 0f)
            jumpRetryTimer = 0f;
    }

    // -------------------------
    // Attack
    // -------------------------

    private void CheckAttack()
    {
        if (!IsReady())
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (distance <= attackDistance)
        {
            KillPlayer();
        }
    }

    private void KillPlayer()
    {
        if (playerDead)
            return;

        playerDead = true;

        player.gameObject.SetActive(false);

        StopMoving();
    }

    // -------------------------
    // Debug Gizmos
    // -------------------------

    private void OnDrawGizmosSelected()
    {
        // Follow range.
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            followRange
        );

        // Attack range.
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackDistance
        );

        // Jump range.
        Gizmos.color = Color.blue;

        Gizmos.DrawWireSphere(
            transform.position,
            maxJumpDistance
        );
    }
}

// NEW ENEMY EXTENSION POINT:
//
// Example:
//
// public class EnemyPatrol : EnemyMovement
// {
//     private void Update()
//     {
//         base.Update();
//
//         // Add unique enemy behavior here.
//     }
// }