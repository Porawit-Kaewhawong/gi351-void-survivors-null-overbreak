using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Forces")]
    public float jumpForce = 18f;

    [Header("Audio & FX")]
    [Tooltip("Sound clip played when the player or an object lands on the jump pad.")]
    public AudioClip launchSound;

    [Range(0f, 1f)]
    [Tooltip("Volume of the launch sound.")]
    public float soundVolume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        // Try to get an attached AudioSource component on startup
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if the object entering is your Player
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            player.Bounce(jumpForce);
            PlayLaunchEffect();
            return;
        }

        // 2. Fallback for non-player physics objects (crates, props, etc.)
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && !rb.isKinematic)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 vel = rb.linearVelocity;
            vel.y = jumpForce;
            rb.linearVelocity = vel;
#else
            Vector3 vel = rb.velocity;
            vel.y = jumpForce;
            rb.velocity = vel;
#endif
            PlayLaunchEffect();
        }
    }

    private void PlayLaunchEffect()
    {
        if (launchSound == null) return;

        // Method A: Use attached AudioSource if available
        if (audioSource != null)
        {
            audioSource.PlayOneShot(launchSound, soundVolume);
        }
        // Method B: Fallback spatial 3D audio playback at the JumpPad position
        else
        {
            AudioSource.PlayClipAtPoint(launchSound, transform.position, soundVolume);
        }
    }
}