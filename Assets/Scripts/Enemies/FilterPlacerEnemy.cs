using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FilterPlacer : EnemyBase
{
    [Header("Movement Settings")]
    [Tooltip("Speed at which the Filter Placer chases the player.")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Filter Effect Settings")]
    [Tooltip("Duration of the vision-obscuring filter effect in seconds.")]
    [SerializeField] private float filterDuration = 4f;

    [Tooltip("Color tint of the vision filter (e.g., dark fog or strange color tint).")]
    [SerializeField] private Color filterColor = new Color(0.1f, 0.4f, 0.1f, 0.75f);

    [Tooltip("Cooldown time between applying the filter effect again.")]
    [SerializeField] private float touchCooldown = 2f;

    private CharacterController charController;
    private float cooldownTimer;
    private static GameObject activeFilterCanvas;

    protected override void Awake()
    {
        base.Awake();
        SetupCharacterController();
    }

    protected override void Update()
    {
        base.Update();

        // Handle cooldown timer over time
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Chase the player continuously (does not stop even if the player stands still)
        ChasePlayer();
    }

    // --- SETUP CHARACTER CONTROLLER ---
    private void SetupCharacterController()
    {
        charController = GetComponent<CharacterController>();
        if (charController == null)
        {
            charController = gameObject.AddComponent<CharacterController>();
            charController.radius = 0.5f;
            charController.height = 1.8f;
        }
    }

    // --- CHASE LOGIC ---
    private void ChasePlayer()
    {
        Vector3 targetPos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        Vector3 direction = (targetPos - transform.position).normalized;

        // Move towards the player every frame regardless of whether the player moves or stops
        if (charController != null)
        {
            charController.Move(direction * moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        }

        // Rotate smoothly towards the player
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    // --- COLLISION & TRIGGER DETECTION ---
    private void OnCollisionEnter(Collision collision)
    {
        TryApplyFilter(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyFilter(other.gameObject);
    }

    private void TryApplyFilter(GameObject targetObj)
    {
        if (cooldownTimer > 0f || targetObj == null) return;

        // Check if the touched object is the player
        if (targetObj.CompareTag("Player"))
        {
            ApplyVisionFilter();
            cooldownTimer = touchCooldown;
            Debug.Log($"[{gameObject.name}] Applied vision filter to the player!");
        }
    }

    // --- VISION FILTER EFFECT LOGIC ---
    private void ApplyVisionFilter()
    {
        StartCoroutine(FilterEffectRoutine());
    }

    private IEnumerator FilterEffectRoutine()
    {
        // If a filter canvas already exists, destroy it to reset the timer
        if (activeFilterCanvas != null)
        {
            Destroy(activeFilterCanvas);
        }

        // Dynamically create a UI Canvas over the screen so it's all-in-one self-contained
        GameObject canvasObj = new GameObject("PlayerVisionFilterCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Render on top of everything
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        activeFilterCanvas = canvasObj;

        // Create a full-screen Panel/Image for the filter effect
        GameObject panelObj = new GameObject("FilterPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image img = panelObj.AddComponent<Image>();
        img.color = filterColor;

        // Stretch the panel to fill the entire screen
        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // Keep the filter active for the specified duration
        float elapsed = 0f;
        while (elapsed < filterDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Cleanup the canvas after the duration ends
        if (activeFilterCanvas != null)
        {
            Destroy(activeFilterCanvas);
            activeFilterCanvas = null;
        }
    }
}