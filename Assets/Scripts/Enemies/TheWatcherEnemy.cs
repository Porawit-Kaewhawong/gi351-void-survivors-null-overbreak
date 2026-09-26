using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TheWatcherEnemy : EnemyBase
{
    [Header("Watcher UI Settings")]
    [Tooltip("Image sprite to display on the left side of the screen.")]
    [SerializeField] private Sprite watcherSprite;

    [Tooltip("Message text to display instructing the player about the fog item.")]
    [SerializeField] private string watcherMessage = "You have something to retrieve in the fog!";

    [Tooltip("Optional custom font for the text. If left empty, it will use Unity's default runtime font.")]
    [SerializeField] private Font customFont;

    [Header("Audio Settings")]
    [Tooltip("Sound played when The Watcher appears.")]
    [SerializeField] private AudioClip watcherSound;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before The Watcher can first appear after spawn.")]
    [SerializeField] private float initialDelay = 15f;

    [Tooltip("Minimum time interval between random appearances.")]
    [SerializeField] private float minSpawnInterval = 20f;

    [Tooltip("Maximum time interval between random appearances.")]
    [SerializeField] private float maxSpawnInterval = 40f;

    private GameObject uiCanvasObj;
    private Image watcherImage;
    private Text watcherText;
    private AudioSource audioSource;
    private bool isEventActive = false;

    protected override void Awake()
    {
        base.Awake();

        // Setup AudioSource component for watcher sound effect
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // Start the random repeating event loop
        StartCoroutine(RandomWatcherRoutine());
    }

    // --- RANDOM REPEATING EVENT LOOP ---
    private IEnumerator RandomWatcherRoutine()
    {
        // Wait for initial delay so it doesn't appear right when the player spawns
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            // Wait for a random interval (not too fast, not too slow)
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            // Trigger The Watcher event multiple times during gameplay
            if (!isEventActive)
            {
                yield return StartCoroutine(TriggerWatcherEventRoutine());
            }
        }
    }

    // --- WATCHER EVENT SEQUENCE ---
    private IEnumerator TriggerWatcherEventRoutine()
    {
        isEventActive = true;
        Debug.Log("[TheWatcherEnemy] The Watcher appeared on screen!");

        // Create UI panel on the left side of the screen
        CreateWatcherUI();

        // Play appearance sound effect
        if (watcherSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(watcherSound);
        }

        // Phase 1: Flashing effect when it first appears
        yield return StartCoroutine(FlashUIRoutine(1.5f));

        // Phase 2: Stay on screen for about 4 seconds so the player remembers
        yield return new WaitForSeconds(4f);

        // Phase 3: Gradually fade out and destroy the UI
        yield return StartCoroutine(FadeOutAndDestroyUIRoutine());

        isEventActive = false;
    }

    // --- DYNAMIC UI CREATION (LEFT SIDE OF SCREEN) ---
    private void CreateWatcherUI()
    {
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        // Create Canvas in screen space overlay
        uiCanvasObj = new GameObject("TheWatcherCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        uiCanvasObj.AddComponent<CanvasScaler>();
        uiCanvasObj.AddComponent<GraphicRaycaster>();

        // Create Main Container Panel positioned on the left side of the screen
        GameObject panelObj = new GameObject("WatcherPanel");
        panelObj.transform.SetParent(uiCanvasObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f); // Semi-transparent background box

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.3f); // Left side anchoring
        panelRect.anchorMax = new Vector2(0.28f, 0.7f);
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        // Create Image element inside the panel
        GameObject imageObj = new GameObject("WatcherImage");
        imageObj.transform.SetParent(panelObj.transform, false);
        watcherImage = imageObj.AddComponent<Image>();
        if (watcherSprite != null)
        {
            watcherImage.sprite = watcherSprite;
        }

        RectTransform imgRect = imageObj.GetComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.05f, 0.3f);
        imgRect.anchorMax = new Vector2(0.95f, 0.95f);
        imgRect.sizeDelta = Vector2.zero;
        imgRect.anchoredPosition = Vector2.zero;

        // Create Text element below the image inside the panel
        GameObject textObj = new GameObject("WatcherText");
        textObj.transform.SetParent(panelObj.transform, false);
        watcherText = textObj.AddComponent<Text>();
        watcherText.text = watcherMessage;

        // Fixed: Use LegacyRuntime.ttf instead of Arial.ttf to avoid built-in font exception
        if (customFont != null)
        {
            watcherText.font = customFont;
        }
        else
        {
            watcherText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        watcherText.fontSize = 16;
        watcherText.alignment = TextAnchor.MiddleCenter;
        watcherText.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.05f, 0.02f);
        textRect.anchorMax = new Vector2(0.95f, 0.28f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
    }

    // --- FLASHING EFFECT ROUTINE ---
    private IEnumerator FlashUIRoutine(float duration)
    {
        if (watcherImage == null) yield break;

        float elapsed = 0f;
        bool isVisible = true;
        float flashInterval = 0.2f;
        float nextFlashTime = 0f;

        while (elapsed < duration)
        {
            if (Time.time >= nextFlashTime)
            {
                isVisible = !isVisible;
                if (watcherImage != null)
                {
                    Color c = watcherImage.color;
                    c.a = isVisible ? 1f : 0.2f;
                    watcherImage.color = c;
                }
                nextFlashTime = Time.time + flashInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure full visibility after flashing ends
        if (watcherImage != null)
        {
            Color c = watcherImage.color;
            c.a = 1f;
            watcherImage.color = c;
        }
    }

    // --- FADE OUT EFFECT ROUTINE ---
    private IEnumerator FadeOutAndDestroyUIRoutine()
    {
        if (uiCanvasObj == null) yield break;

        float fadeDuration = 1.0f;
        float elapsed = 0f;

        // Use CanvasGroup to fade out all child UI elements smoothly
        CanvasGroup canvasGroup = uiCanvasObj.AddComponent<CanvasGroup>();

        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            canvasGroup.alpha = alpha;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(uiCanvasObj);
    }
}