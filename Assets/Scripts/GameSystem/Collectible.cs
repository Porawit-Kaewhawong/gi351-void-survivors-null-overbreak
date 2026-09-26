using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Audio Settings")]
    [Tooltip("Sound clip played when the player collects this item.")]
    [SerializeField] private AudioClip collectSound;

    [Tooltip("Volume level for the collect sound (0.0 to 1.0).")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    private bool isCollected = false;
    private bool isRegistered = false;
    private Transform playerTransform;
    private PlayerController playerController;

    private void Awake()
    {
        Register(); // Fires IMMEDIATELY on Instantiate()
    }

    private void Start()
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
        if (isCollected || playerTransform == null) return;

        float range = playerController != null ? playerController.CurrentPickupRadius : 2f;
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance <= range)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, 10f * Time.deltaTime);
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