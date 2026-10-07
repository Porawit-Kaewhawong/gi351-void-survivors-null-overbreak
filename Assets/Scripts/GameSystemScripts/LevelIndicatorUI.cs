using System.Collections;
using TMPro;
using UnityEngine;

public class LevelIndicator : MonoBehaviour
{
    public static LevelIndicator Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Huge centered TextMeshProUGUI element on your Canvas.")]
    [SerializeField] private TextMeshProUGUI bannerText;

    [Header("Typewriter Timing Settings")]
    [Tooltip("Speed in seconds between each typed letter.")]
    [SerializeField] private float letterDelay = 0.05f;

    [Tooltip("Duration in seconds the full text stays visible before fading out.")]
    [SerializeField] private float displayHoldDuration = 1.2f;

    [Tooltip("Duration in seconds for the smooth fade-out.")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Header("Self-Contained Audio")]
    [Tooltip("Sound clip played for each typed character.")]
    [SerializeField] private AudioClip typeClickSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.4f;

    [Tooltip("Slightly randomizes pitch per letter so the typewriter sound feels organic.")]
    [SerializeField] private bool randomizePitch = true;

    private AudioSource audioSource;
    private Coroutine activeBannerCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-fetch or dynamically add an AudioSource so no manual inspector setup is required
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        if (bannerText != null)
        {
            Color c = bannerText.color;
            c.a = 0f;
            bannerText.color = c;
            bannerText.text = "";
        }
    }

    /// <summary>
    /// Triggers the typewriter reveal effect for the provided message.
    /// </summary>
    public void DisplayText(string message)
    {
        if (bannerText == null) return;

        if (activeBannerCoroutine != null)
        {
            StopCoroutine(activeBannerCoroutine);
        }

        activeBannerCoroutine = StartCoroutine(TypewriterRoutine(message));
    }

    private IEnumerator TypewriterRoutine(string fullMessage)
    {
        bannerText.text = "";
        Color textColor = bannerText.color;
        textColor.a = 1f;
        bannerText.color = textColor;

        // Type out letter by letter
        for (int i = 0; i < fullMessage.Length; i++)
        {
            bannerText.text += fullMessage[i];

            // Play the typewriter sound internally
            PlayTypewriterClick(fullMessage[i]);

            yield return new WaitForSeconds(letterDelay);
        }

        // Hold visible text
        yield return new WaitForSeconds(displayHoldDuration);

        // Smooth fade out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            textColor.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            bannerText.color = textColor;
            yield return null;
        }

        bannerText.text = "";
        activeBannerCoroutine = null;
    }

    private void PlayTypewriterClick(char character)
    {
        if (typeClickSound == null || char.IsWhiteSpace(character)) return;

        if (audioSource != null)
        {
            if (randomizePitch)
            {
                audioSource.pitch = Random.Range(0.88f, 1.12f);
            }
            audioSource.PlayOneShot(typeClickSound, soundVolume);
        }
        else if (Camera.main != null)
        {
            // Fallback if no AudioSource attached
            AudioSource.PlayClipAtPoint(typeClickSound, Camera.main.transform.position, soundVolume);
        }
    }
}