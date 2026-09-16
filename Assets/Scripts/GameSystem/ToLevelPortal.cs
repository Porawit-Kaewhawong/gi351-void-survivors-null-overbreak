using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToLevelPortal : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    private bool levelStarted = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (levelStarted) return;

        if (other.CompareTag(playerTag) || other.GetComponent<CharacterController>() != null)
        {
            levelStarted = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartSelectedLevel();
            }
        }
    }
}