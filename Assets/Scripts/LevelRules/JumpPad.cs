using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Pad Forces")]
    public float jumpForce = 18f;

    [Header("Audio & FX (Optional)")]
    public AudioClip launchSound;
    private AudioSource audioSource;

    private void Awake()
    {
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
        if (audioSource != null && launchSound != null)
        {
            audioSource.PlayOneShot(launchSound);
        }
    }
}