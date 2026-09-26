using UnityEngine;

public class BalloonEnemy : EnemyBase
{
    [Header("Balloon Movement Settings")]
    [Tooltip("Height offset to keep the balloon hovering around the player.")]
    public float heightOffset = 1.5f;

    [Header("Floating Bobbing Effect")]
    [Tooltip("Amplitude of the up-and-down floating motion.")]
    public float floatAmplitude = 0.5f;

    [Tooltip("Frequency of the up-and-down floating motion.")]
    public float floatFrequency = 2f;

    private float randomOffset;

    protected override void Awake()
    {
        base.Awake();

        randomOffset = Random.Range(0f, 10f);
    }

    protected override void Update()
    {
        base.Update();

        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        FloatTowardsPlayer();
        TryAttackPlayer();
    }

    private void FloatTowardsPlayer()
    {
        Vector3 targetPosition =
            playerTransform.position +
            Vector3.up * heightOffset;

        float bobbing =
            Mathf.Sin((Time.time + randomOffset) * floatFrequency)
            * floatAmplitude;

        targetPosition.y += bobbing;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        Vector3 direction =
            playerTransform.position - transform.position;

        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * 5f
            );
        }
    }
}