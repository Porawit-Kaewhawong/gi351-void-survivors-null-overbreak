using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Default Pitch Variance")]
    [Range(0.5f, 1f)] public float minPitch = 0.92f;
    [Range(1f, 1.5f)] public float maxPitch = 1.08f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Plays 2D UI or global sound effects with randomized pitch.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;

        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Plays a 3D sound in world space with pitch variance.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        GameObject tempAudioObj = new GameObject("Temp3DAudio");
        tempAudioObj.transform.position = position;

        AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.volume = volume;
        tempSource.spatialBlend = 1f; // Full 3D positioning
        tempSource.minDistance = 2f;
        tempSource.maxDistance = 25f;
        tempSource.pitch = Random.Range(minPitch, maxPitch);

        tempSource.Play();

        // Self-destruct temporary audio source after clip finishes
        Destroy(tempAudioObj, clip.length / Mathf.Abs(tempSource.pitch) + 0.1f);
    }

    /// <summary>
    /// Dynamically shifts BGM speed/pitch (e.g. during Collapse Overtime).
    /// </summary>
    public void SetBGMPitch(float pitch)
    {
        if (bgmSource != null)
        {
            bgmSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
        }
    }
}