using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToLobbyPortal : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when entering the portal to finish a level.")]
    [SerializeField] private AudioClip portalSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    private bool levelFinished = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (levelFinished) return;

        // Check strictly for the Player tag or PlayerController script
        if (other.CompareTag(playerTag) || other.GetComponent<PlayerController>() != null)
        {
            levelFinished = true;

            if (portalSound != null)
            {
                AudioSource.PlayClipAtPoint(portalSound, transform.position, soundVolume);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCompleted();
            }

            Destroy(gameObject);
        }
    }
}