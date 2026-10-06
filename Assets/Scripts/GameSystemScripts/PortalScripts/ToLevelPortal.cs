using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToLevelPortal : MonoBehaviour
{
    [Header("Player Detection")]
    public string playerTag = "Player";

    [Header("Audio Settings")]
    [Tooltip("Sound clip played when stepping into the portal to start the level.")]
    [SerializeField] private AudioClip portalSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

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

            if (portalSound != null)
            {
                AudioSource.PlayClipAtPoint(portalSound, transform.position, soundVolume);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartSelectedLevel();
            }
        }
    }
}