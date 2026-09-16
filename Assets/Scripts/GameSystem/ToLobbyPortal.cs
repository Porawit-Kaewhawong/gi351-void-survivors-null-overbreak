using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToLobbyPortal : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    private bool levelFinished = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (levelFinished) return;

        if (other.CompareTag(playerTag) || other.GetComponent<CharacterController>() != null)
        {
            levelFinished = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCompleted();
            }

            Destroy(gameObject);
        }
    }
}