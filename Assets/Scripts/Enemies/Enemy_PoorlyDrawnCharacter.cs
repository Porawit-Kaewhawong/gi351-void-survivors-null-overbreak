using System.Collections;
using UnityEngine;

public class PoorlyDrawnCharacterEnemy : EnemyBase
{
    [Header("Movement Settings")]
    [Tooltip("Height offset to keep floating above the ground/player.")]
    [SerializeField] private float heightOffset = 2f;

    [Header("Detection & Range Settings")]
    [Tooltip("Maximum distance from the player to trigger telegraphing and throwing.")]
    [SerializeField] private float attackRange = 15f;

    [Header("Throwing & Telegraph Settings")]
    [Tooltip("Time interval between each throw attack.")]
    [SerializeField] private float throwInterval = 3f;

    [Tooltip("Duration of the single red warning line before throwing.")]
    [SerializeField] private float telegraphDuration = 1f;

    [Tooltip("Medium throw force/speed for the projectile.")]
    [SerializeField] private float throwForce = 10f;

    [Tooltip("Optional projectile prefab to throw. If left empty, a default sphere will be created.")]
    [SerializeField] private GameObject projectilePrefab;

    private LineRenderer lineRenderer;
    private float throwTimer;
    private bool isTelegraphing = false;

    protected override void Awake()
    {
        base.Awake();
        SetupLineRenderer();
        throwTimer = throwInterval;
    }

    protected override void Update()
    {
        base.Update();

        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Float and follow the player smoothly (only if not currently telegraphing)
        if (!isTelegraphing)
        {
            FloatTowardsPlayer();
        }

        // Calculate distance to the player
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool isInRange = distanceToPlayer <= attackRange;

        // Handle throw cooldown and trigger attack sequence ONLY when player is in range
        throwTimer -= Time.deltaTime;
        if (throwTimer <= 0f && !isTelegraphing && isInRange)
        {
            StartCoroutine(TelegraphAndThrowRoutine());
            throwTimer = throwInterval;
        }
    }

    // --- FLOATING MOVEMENT ---
    private void FloatTowardsPlayer()
    {
        Vector3 targetPosition = playerTransform.position + Vector3.up * heightOffset;

        // Add a slight floating bobbing effect
        float bobbing = Mathf.Sin(Time.time * 3f) * 0.3f;
        targetPosition.y += bobbing;

        // Move directly towards the player
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    // --- SETUP SINGLE RED LINE RENDERER ---
    private void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.positionCount = 2;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
        lineRenderer.enabled = false;
    }

    // --- TELEGRAPH & THROW COROUTINE ---
    private IEnumerator TelegraphAndThrowRoutine()
    {
        isTelegraphing = true;

        if (lineRenderer != null) lineRenderer.enabled = true;

        float elapsed = 0f;

        while (elapsed < telegraphDuration)
        {
            if (playerTransform == null) break;

            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, transform.position + Vector3.up * 0.5f);
                lineRenderer.SetPosition(1, playerTransform.position + Vector3.up * 0.5f);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (lineRenderer != null) lineRenderer.enabled = false;

        ThrowProjectile();

        isTelegraphing = false;
    }

    // --- THROW PROJECTILE LOGIC ---
    private void ThrowProjectile()
    {
        if (playerTransform == null) return;

        // Calculate direct offset towards player, ignoring FaceCamera rotation
        Vector3 dirToPlayer = (playerTransform.position - transform.position);
        dirToPlayer.y = 0f;
        dirToPlayer.Normalize();

        Vector3 spawnPos = transform.position + dirToPlayer * 0.6f + Vector3.up * 0.5f;
        GameObject proj;

        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proj.transform.position = spawnPos;
            proj.transform.localScale = Vector3.one * 0.4f;
        }

        // Prevent projectile from colliding with the enemy itself
        Collider enemyCollider = GetComponent<Collider>();
        Collider projCollider = proj.GetComponent<Collider>();
        if (enemyCollider != null && projCollider != null)
        {
            Physics.IgnoreCollision(enemyCollider, projCollider);
        }

        Rigidbody rb = proj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = proj.AddComponent<Rigidbody>();
        }
        rb.useGravity = true;

        EnemyProjectile projScript = proj.GetComponent<EnemyProjectile>();
        if (projScript == null)
        {
            projScript = proj.AddComponent<EnemyProjectile>();
        }
        projScript.Initialize(attackDamage);

        Vector3 throwDir = (playerTransform.position + Vector3.up * 0.5f - spawnPos).normalized;
        rb.AddForce(throwDir * throwForce + Vector3.up * 1.5f, ForceMode.Impulse);

        Debug.Log($"[{gameObject.name}] Threw a projectile towards the player!");
    }

    // --- EDITOR GIZMO FOR ATTACK RANGE ---
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}