using UnityEngine;

public class Collectible : MonoBehaviour
{
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