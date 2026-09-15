using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    private bool isCollected = false;

    private void Start()
    {
        // Register this item with the manager when spawned
        if (GameManager.Instance != null)
        {
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
        // If the chunk despawns and destroys the item before collection, reduce total count
        if (!isCollected && gameObject.scene.isLoaded && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterItem();
        }
    }
}