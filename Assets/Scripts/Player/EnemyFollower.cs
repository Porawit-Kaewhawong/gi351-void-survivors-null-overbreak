using UnityEngine;
using UnityEngine.UIElements;

public class EnemyFollower : EnemyMovement
{
    protected override void Update()
    {
        base.Update(); // Updates attack cooldown timer

        if (IsRespawning()) return;
        if (CheckFall()) return;

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

    private void FollowPlayer()
    {
        Vector3 direction = playerTransform.position - transform.position;
        direction.y = 0f;

        // Use squared magnitude check for performance
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

        // Uses cached CharacterController on player from EnemyBase
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
    }
}