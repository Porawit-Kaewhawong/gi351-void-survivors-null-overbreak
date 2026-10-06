using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FilterPlacerEnemy : EnemyBase
{
    [Tooltip("Height offset for chasing.")]
    [SerializeField] private float heightOffset = 1f;

    [Header("Filter Status Effect Settings")]
    [Tooltip("Duration of the vision-hindering filter status in seconds.")]
    [SerializeField] private float filterDuration = 4f;

    [Tooltip("List of full-screen filter sprites to randomly display when hitting the player.")]
    [SerializeField] private List<Sprite> filterSprites = new List<Sprite>();

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

        // Continuously chase the player
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

        // Uses GetEffectiveMoveSpeed() for catch-up/rubberbanding support
        float currentSpeed = GetEffectiveMoveSpeed();
        Vector3 moveDir = toTarget.normalized * currentSpeed;

        if (charController != null)
        {
            charController.Move(moveDir * Time.deltaTime);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);
        }

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
        if (target.CompareTag("Player") && !isFilterActive)
        {
            ApplyFilterStatus();

            PlayerController playerCtrl = target.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.TakeDamage(attackDamage);
            }
        }
    }

    // --- FILTER STATUS EFFECT SPANWER ---
    private void ApplyFilterStatus()
    {
        // Safety check to ensure sprites have been populated in Inspector
        if (filterSprites == null || filterSprites.Count == 0)
        {
            Debug.LogWarning($"[{name}] No filter sprites assigned in the Inspector list!");
            return;
        }

        isFilterActive = true;

        // Select a random sprite from the list
        Sprite chosenSprite = filterSprites[Random.Range(0, filterSprites.Count)];

        // 1. Create full-screen UI Canvas
        GameObject canvasObj = new GameObject("PlayerFilterCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject panelObj = new GameObject("FilterPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();

        // Display selected sprite at full color opacity
        panelImage.sprite = chosenSprite;
        panelImage.color = Color.white;

        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // 2. Attach independent runner to Canvas so status lifecycle persists past enemy death
        FilterEffectRunner runner = canvasObj.AddComponent<FilterEffectRunner>();
        runner.StartEffect(filterDuration, () => isFilterActive = false);
    }
}

// --- INDEPENDENT STATUS RUNNER ---
public class FilterEffectRunner : MonoBehaviour
{
    public void StartEffect(float duration, System.Action onComplete)
    {
        StartCoroutine(FilterRoutine(duration, onComplete));
    }

    private IEnumerator FilterRoutine(float duration, System.Action onComplete)
    {
        // Maintains the visual UI filter for the full duration without touching the camera transform
        yield return new WaitForSeconds(duration);

        onComplete?.Invoke();
        Destroy(gameObject);
    }
}