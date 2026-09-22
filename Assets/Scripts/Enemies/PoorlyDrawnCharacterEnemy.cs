using System.Collections;
using UnityEngine;

public class PoorlyDrawnCharacterEnemy : EnemyBase
{
    [Header("Movement Settings")]
    [Tooltip("Height offset to keep floating above the ground/player.")]
    [SerializeField] private float heightOffset = 2f;

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

        // Float and follow the player smoothly through walls (only if not currently telegraphing)
        if (!isTelegraphing)
        {
            FloatTowardsPlayer();
        }

        // Handle throw cooldown and trigger attack sequence
        throwTimer -= Time.deltaTime;
        if (throwTimer <= 0f && !isTelegraphing)
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

        // Move directly through walls towards the player
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // Face towards the player horizontally
        Vector3 direction = playerTransform.position - transform.position;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
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

        Vector3 spawnPos = transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;
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
}