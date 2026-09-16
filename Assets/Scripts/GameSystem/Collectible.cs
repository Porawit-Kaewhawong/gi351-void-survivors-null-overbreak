using UnityEngine;

public class Collectible : MonoBehaviour
{
    private bool isCollected = false;
    private bool isRegistered = false;

    private void Awake()
    {
        // Register immediately during Instantiate() so MapGenerator sees the count on frame 1
        Register();
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