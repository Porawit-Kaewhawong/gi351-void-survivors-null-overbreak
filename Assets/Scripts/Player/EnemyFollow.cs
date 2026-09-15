using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyFollow : EnemyMovement
{
    [Header("Target")]
    public Transform player;

    [Header("Attack")]
    public float attackDistance = 1.2f;

    protected override void Awake()
    {
        base.Awake();

        // Find the player automatically if no target is assigned.
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
        if (IsRespawning())
            return;

        if (player == null)
            return;

        if (CheckFall())
            return;

        CheckAttack();

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

    private void FollowPlayer()
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.magnitude <= attackDistance)
        {
            SlowDown();
            return;
        }

        MoveTo(direction);
    }

    private void CheckPlayerJump()
    {
        CharacterController playerController =
            player.GetComponent<CharacterController>();

        if (playerController == null)
            return;

        if (!playerController.isGrounded)
        {
            Vector3 direction =
                player.position - transform.position;

            TryJump(direction);
        }
    }

    private void CheckAttack()
    {
        if (Vector3.Distance(
            transform.position,
            player.position) <= attackDistance)
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().buildIndex
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            attackDistance
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            maxJumpDistance
        );
    }
}