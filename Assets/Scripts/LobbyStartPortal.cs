using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Lobby3DStartPortal : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Tag assigned to your Player GameObject.")]
    public string playerTag = "Player";

    private bool levelStarted = false;

    private void Awake()
    {
        // Ensure the attached collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (levelStarted) return;

        // Verify if the entering collider belongs to the Player
        if (other.CompareTag(playerTag) || other.GetComponent<CharacterController>() != null)
        {
            levelStarted = true;
            EnterLevel();
        }
    }

    private void EnterLevel()
    {
        // Trigger level generation and gameplay sequence
        if (LobbyGameManager.Instance != null)
        {
            LobbyGameManager.Instance.StartSelectedLevel();
        }
        else if (LobbyGameManager.Instance != null)
        {
            LobbyGameManager.Instance.StartSelectedLevel();
        }
    }
}