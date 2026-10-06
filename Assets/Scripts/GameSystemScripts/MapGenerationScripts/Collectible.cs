using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Sound clip played when the player collects this item.")]
    [SerializeField] private AudioClip collectSound;

    [Tooltip("Volume level for the collect sound (0.0 to 1.0).")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("Pickup Magnet Settings")]
    [Tooltip("Base flight speed when the item starts moving toward the player.")]
    [SerializeField] private float baseFlySpeed = 12f;

    [Tooltip("Acceleration rate (units/sec²) applied while traveling.")]
    [SerializeField] private float acceleration = 35f;

    [Tooltip("Multiplier applied to distance. Higher = faster pull from extreme distances.")]
    [SerializeField] private float distanceMultiplier = 6f;

    [Tooltip("Vertical height offset targeting the player's upper body.")]
    [SerializeField] private float targetYOffset = 0.8f;

    private bool isCollected = false;
    private bool isRegistered = false;
    private bool isMagnetized = false;

    private float currentFlySpeed;
    private Transform playerTransform;
    private PlayerController playerController;

    private void Awake()
    {
        Register(); // Fires IMMEDIATELY on Instantiate()
    }

    private void Start()
    {
        currentFlySpeed = baseFlySpeed;
        FindPlayerReference();
    }

    private void FindPlayerReference()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (playerTransform == null)
        {
            FindPlayerReference();
            if (playerTransform == null) return;
        }

        Vector3 targetPosition = playerTransform.position + Vector3.up * targetYOffset;
        float distance = Vector3.Distance(transform.position, targetPosition);
        float pickupRadius = playerController != null ? playerController.CurrentPickupRadius : 2f;

        // 1. Trigger magnet pull when item enters player's dynamic pickup radius
        if (!isMagnetized && distance <= pickupRadius)
        {
            isMagnetized = true;
        }

        // 2. Fly towards player with dynamic distance scaling
        if (isMagnetized)
        {
            // Ramp speed over time
            currentFlySpeed += acceleration * Time.deltaTime;

            // Pick whichever is faster: accelerated speed OR distance-proportional speed
            float dynamicSpeed = Mathf.Max(currentFlySpeed, distance * distanceMultiplier);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                dynamicSpeed * Time.deltaTime
            );
        }
    }

    private void Register()
    {
        if (!isRegistered && GameManager.Instance != null)
        {
            isRegistered = true;
            GameManager.Instance.RegisterItem();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isCollected && other.CompareTag("Player"))
        {
            isCollected = true;

            // Play pickup sound at the collectible's position before destroying
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, soundVolume);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnItemCollected();
            }

            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (isRegistered && !isCollected && gameObject.scene.isLoaded && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterItem();
        }
    }
}