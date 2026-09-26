using UnityEngine;
using System.Collections;

public class FilterPlacerEnemy : EnemyBase
{
    

    [Tooltip("Height offset for chasing.")]
    [SerializeField] private float heightOffset = 1f;

    [Header("Filter Status Effect Settings")]
    [Tooltip("Duration of the vision-hindering filter status in seconds.")]
    [SerializeField] private float filterDuration = 4f;

    [Tooltip("Color tint of the weird filter overlay that blocks vision.")]
    [SerializeField] private Color filterOverlayColor = new Color(0.2f, 0.8f, 0.3f, 0.65f);

    [Tooltip("Intensity of the screen shake while the filter is active.")]
    [SerializeField] private float shakeIntensity = 0.08f;

    private CharacterController charController;
    private static bool isFilterActive = false;

    protected override void Awake()
    {
        base.Awake();
        SetupCharacterController();
    }

    protected override void Update()
    {
        base.Update();

        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        // Continuously chase the player regardless of whether the player stops moving
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
            charController.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    // --- CHASE PLAYER LOGIC ---
    private void ChasePlayer()
    {
        Vector3 targetPosition = playerTransform.position + Vector3.up * heightOffset;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f; // Keep movement horizontal

        Vector3 moveDir = toTarget.normalized * moveSpeed;

        // Move towards the player continuously
        if (charController != null)
        {
            charController.Move(moveDir * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        // Face towards the player
        if (toTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    // --- TOUCH COLLISION DETECTION ---
    private void OnCollisionEnter(Collision collision)
    {
        TriggerFilterOnTouch(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerFilterOnTouch(other.gameObject);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        TriggerFilterOnTouch(hit.gameObject);
    }

    private void TriggerFilterOnTouch(GameObject target)
    {
        // Check if the touched object is the player and the filter isn't already active
        if (target.CompareTag("Player") && !isFilterActive)
        {
            StartCoroutine(ApplyFilterStatusRoutine());

            // Optional: Deal touch damage if needed via EnemyBase stats
            PlayerController playerCtrl = target.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.TakeDamage(attackDamage);
            }
        }
    }

    // --- FILTER STATUS EFFECT COROUTINE ---
    private IEnumerator ApplyFilterStatusRoutine()
    {
        isFilterActive = true;
        Debug.Log($"[{gameObject.name}] Player touched! Applying vision-hindering filter status.");

        // 1. Dynamically create a full-screen UI Canvas and Panel to block/hinder vision
        GameObject canvasObj = new GameObject("PlayerFilterCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Ensure it renders on top of everything
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panelObj = new GameObject("FilterPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = filterOverlayColor;

        // Stretch panel to cover the entire screen
        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // 2. Camera shake and duration loop
        Camera mainCam = Camera.main;
        Vector3 originalCamPos = mainCam != null ? mainCam.transform.localPosition : Vector3.zero;

        float elapsed = 0f;
        while (elapsed < filterDuration)
        {
            // Shake the camera to make it disorienting and hard to see
            if (mainCam != null)
            {
                Vector3 shakeOffset = Random.insideUnitSphere * shakeIntensity;
                shakeOffset.z = 0f;
                mainCam.transform.localPosition = originalCamPos + shakeOffset;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. Restore camera position and remove the filter overlay
        if (mainCam != null)
        {
            mainCam.transform.localPosition = originalCamPos;
        }

        Destroy(canvasObj);
        isFilterActive = false;
        Debug.Log("[FilterPlacerEnemy] Filter status expired.");
    }
}