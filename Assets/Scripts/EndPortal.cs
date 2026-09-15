using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InGameFinishPortal : MonoBehaviour
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

            // Trigger level completion in Lobby Manager
            if (LobbyGameManager.Instance != null)
            {
                LobbyGameManager.Instance.OnLevelCompleted();
            }
            else if (LobbyGameManager.Instance != null)
            {
                LobbyGameManager.Instance.OnLevelCompleted();
            }

            // Destroy this portal instance immediately
            Destroy(gameObject);
        }
    }
}