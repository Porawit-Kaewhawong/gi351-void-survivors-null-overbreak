using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum WatcherTextPosition
{
    Center,
    Top,
    Bottom,
    CustomOffset
}

public class TheWatcherEnemy : EnemyBase
{
    public override bool IsTargetable => false;

    [Header("Watcher Fullscreen Sprite Settings")]
    [Tooltip("Image sprite displayed in the center of the screen during the black flash phase.")]
    [SerializeField] private Sprite watcherSprite;

    [Tooltip("Dimensions (Width, Height) of the Watcher sprite during the full-screen flash phase.")]
    [SerializeField] private Vector2 watcherSpriteSize = new Vector2(350f, 350f);

    [Tooltip("Duration in seconds that the full-screen black panel and Watcher sprite flash.")]
    [SerializeField] private float flashDuration = 1.5f;

    [Header("Watcher Text Settings")]
    [Tooltip("Initial message text to display instructing the player about the fog item.")]
    [TextArea(2, 4)]
    [SerializeField] private string watcherMessage = "You have something to retrieve in the fog!";

    [Tooltip("Time remaining (in seconds) when the urgent warning message triggers.")]
    [SerializeField] private float warningTimeThreshold = 5f;

    [Tooltip("Urgent message shown when time is running low.")]
    [TextArea(2, 4)]
    [SerializeField] private string warningMessage = "Hurry! Time is running out!";

    [Tooltip("Message displayed when item is successfully collected in time.")]
    [TextArea(2, 4)]
    [SerializeField] private string successMessage = "Item retrieved successfully!";

    [Tooltip("How many seconds the success message and remaining fog stay on screen before full cleanup.")]
    [SerializeField] private float successDisplayDuration = 3f;

    [Tooltip("Message displayed when timer hits zero before item collection.")]
    [TextArea(2, 4)]
    [SerializeField] private string timeoutMessage = "You were too late...";

    [Tooltip("How many seconds the timeout message stays on screen before fading out.")]
    [SerializeField] private float timeoutDisplayDuration = 2f;

    [Tooltip("Font size for the typewriter text.")]
    [SerializeField] private int textFontSize = 24;

    [Tooltip("Optional custom font for the text.")]
    [SerializeField] private Font customFont;

    [Header("Text Position Settings")]
    [Tooltip("Controls where on screen the typewriter message appears after the flash phase.")]
    [SerializeField] private WatcherTextPosition textPositionMode = WatcherTextPosition.Center;

    [Tooltip("Manual (X, Y) pixel offset when using CustomOffset position mode.")]
    [SerializeField] private Vector2 textCustomOffset = Vector2.zero;

    [Header("Per-Letter Shake Settings")]
    [Tooltip("Delay between characters in typewriter text animation.")]
    [SerializeField] private float typewriterSpeed = 0.04f;

    [Tooltip("Intensity/magnitude of individual letter vibration.")]
    [SerializeField] private float letterShakeMagnitude = 5f;

    [Tooltip("Speed/frequency of individual letter vibration.")]
    [SerializeField] private float letterShakeSpeed = 25f;

    [Header("Fog Object & Map Spawn Settings")]
    [Tooltip("Prefab for the collectible item that spawns on the ground in the fog.")]
    [SerializeField] private GameObject fogItemPrefab;

    [Tooltip("Time limit (in seconds) the player has to collect the item before taking damage.")]
    [SerializeField] private float collectionTimeLimit = 15f;

    [Tooltip("If enabled, attempts to dynamically fetch the map radius from MapGenerator.")]
    [SerializeField] private bool useMapGeneratorRadius = true;

    [Tooltip("Center point for spawning fog items if MapGenerator is not found.")]
    [SerializeField] private Vector3 mapCenterPosition = Vector3.zero;

    [Tooltip("Fallback radius within which fog items spawn if MapGenerator is not found.")]
    [SerializeField] private float mapSpawnRadius = 25f;

    [Tooltip("Fallback Y height if ground raycasting fails.")]
    [SerializeField] private float groundYPosition = 0f;

    [Tooltip("Vertical offset above the detected floor to prevent clipping.")]
    [SerializeField] private float groundYOffset = 0.2f;

    [Tooltip("Layer mask designated for walkable floor/ground.")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Audio Settings")]
    [Tooltip("Sound played when The Watcher full-screen flash appears.")]
    [SerializeField] private AudioClip watcherSound;

    [Tooltip("Sound played per character during typewriter reveal.")]
    [SerializeField] private AudioClip typewriterSound;

    [Tooltip("Sound played when the player successfully collects the item.")]
    [SerializeField] private AudioClip successSound;

    [Tooltip("Sound played when time runs out and player takes damage.")]
    [SerializeField] private AudioClip failSound;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before The Watcher can first appear after spawn.")]
    [SerializeField] private float initialDelay = 15f;

    [Tooltip("Minimum time interval between random appearances.")]
    [SerializeField] private float minSpawnInterval = 20f;

    [Tooltip("Maximum time interval between random appearances.")]
    [SerializeField] private float maxSpawnInterval = 40f;

    [Header("Vision Penalty Settings")]
    [Tooltip("If enabled, restricts player line of sight on failure.")]
    [SerializeField] private bool applyVisionPenalty = true;

    [Tooltip("How long (in seconds) the player's sight remains restricted after failing.")]
    [SerializeField] private float visionPenaltyDuration = 8f;

    [Tooltip("Target Far Clip Plane during penalty (smaller = less distance visible). Normal is usually 100-1000.")]
    [SerializeField] private float penaltyFarClipPlane = 12f;

    [Tooltip("Optional dark vignette texture/sprite with a transparent center hole to restrict field of view.")]
    [SerializeField] private Sprite visionOverlaySprite;

    private GameObject uiCanvasObj;
    private GameObject fullScreenFlashObj;
    private Image watcherImage;

    private GameObject textPanelObj;
    private Text watcherText;
    private RectTransform textRectTransform;
    private CanvasGroup textCanvasGroup;
    private PerCharacterShake perCharShake;

    private AudioSource audioSource;
    private GameObject currentSpawnedFogItem;
    private bool isItemCollected = false;
    private bool isEventActive = false;

    private Coroutine activeTypewriterCoroutine;
    private Coroutine activeVisionPenaltyCoroutine;
    private float originalFarClipPlane = -1f;

    protected override void Awake()
    {
        base.Awake();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        DisableWorldInteraction();
    }

    private void Start()
    {
        StartCoroutine(RandomWatcherRoutine());
    }

    private void OnDisable()
    {
        if (activeVisionPenaltyCoroutine != null)
        {
            StopCoroutine(activeVisionPenaltyCoroutine);
            activeVisionPenaltyCoroutine = null;
        }
        RestoreCameraFarClip();

        DestroyUI();
        DestroyFogItem(false);
    }

    protected override void Update()
    {
        // Overridden empty to prevent base movement logic
    }

    public override void TakeDamage(float amount)
    {
        // Non-targetable hazard manager ignores incoming damage
    }

    private void DisableWorldInteraction()
    {
        Collider col3D = GetComponent<Collider>();
        if (col3D != null) col3D.enabled = false;

        Collider2D col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;

        gameObject.layer = 2; // Ignore Raycast
    }

    // --- RANDOM REPEATING EVENT LOOP ---
    private IEnumerator RandomWatcherRoutine()
    {
        int safeStackCount = Mathf.Max(1, StackCount);
        float effectiveInitialDelay = initialDelay / safeStackCount;
        yield return new WaitForSeconds(effectiveInitialDelay);

        while (true)
        {
            safeStackCount = Mathf.Max(1, StackCount);
            float minTime = minSpawnInterval / safeStackCount;
            float maxTime = maxSpawnInterval / safeStackCount;

            float waitTime = Random.Range(minTime, maxTime);
            yield return new WaitForSeconds(waitTime);

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
        isItemCollected = false;
        Debug.Log("[TheWatcherEnemy] The Watcher event triggered!");

        CreateBaseCanvas();

        // 1. PHASE 1: Full-Screen Black Overlay + Centered Watcher Sprite Flash
        CreateFullScreenFlashUI();

        if (watcherSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(watcherSound);
        }

        yield return StartCoroutine(FlashUIRoutine(flashDuration));

        // 2. Destroy Full-Screen Overlay completely
        DestroyFullScreenFlashUI();

        // 3. PHASE 2: Spawn Typewriter Text UI
        CreateWatcherTextUI();

        SetWatcherMessage(watcherMessage);

        // 4. Spawn Collectible Item on Ground in Fog (Map-wide)
        SpawnFogItem();

        // 5. Active Countdown Phase
        float timer = collectionTimeLimit;
        bool hasTriggeredWarning = false;

        while (timer > 0f && !isItemCollected)
        {
            timer -= Time.deltaTime;

            if (!hasTriggeredWarning && timer <= warningTimeThreshold)
            {
                hasTriggeredWarning = true;
                SetWatcherMessage(warningMessage);
            }

            yield return null;
        }

        // 6. Resolution Phase
        if (isItemCollected)
        {
            SetWatcherMessage(successMessage);

            if (successSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(successSound);
            }

            DestroyFogItem(allowParticlesToFade: true, fadeDuration: successDisplayDuration);
            yield return new WaitForSeconds(successDisplayDuration);
        }
        else
        {
            SetWatcherMessage(timeoutMessage);

            if (failSound != null && audioSource != null) audioSource.PlayOneShot(failSound);

            if (playerController != null)
            {
                playerController.TakeDamage(attackDamage);
            }

            if (applyVisionPenalty)
            {
                if (activeVisionPenaltyCoroutine != null)
                {
                    StopCoroutine(activeVisionPenaltyCoroutine);
                }
                activeVisionPenaltyCoroutine = StartCoroutine(ApplyBlindnessPenaltyRoutine(visionPenaltyDuration));
            }

            DestroyFogItem(allowParticlesToFade: true, fadeDuration: timeoutDisplayDuration);
            yield return new WaitForSeconds(timeoutDisplayDuration);
        }

        // 7. Fade out and cleanup Text UI
        yield return StartCoroutine(FadeOutAndDestroyTextUIRoutine(1.0f));

        isEventActive = false;
    }

    private void SetWatcherMessage(string message)
    {
        if (activeTypewriterCoroutine != null)
        {
            StopCoroutine(activeTypewriterCoroutine);
        }
        activeTypewriterCoroutine = StartCoroutine(TypewriterTextRoutine(message));
    }

    // --- DYNAMIC UI CREATION ---
    private void CreateBaseCanvas()
    {
        DestroyUI();

        uiCanvasObj = new GameObject("TheWatcherCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = uiCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        uiCanvasObj.AddComponent<GraphicRaycaster>();
    }

    private void CreateFullScreenFlashUI()
    {
        if (uiCanvasObj == null) return;

        fullScreenFlashObj = new GameObject("FullScreenFlashPanel", typeof(RectTransform));
        fullScreenFlashObj.transform.SetParent(uiCanvasObj.transform, false);

        Image blackBg = fullScreenFlashObj.AddComponent<Image>();
        blackBg.color = Color.black;

        RectTransform bgRect = fullScreenFlashObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        GameObject imageObj = new GameObject("WatcherCenterImage", typeof(RectTransform));
        imageObj.transform.SetParent(fullScreenFlashObj.transform, false);

        watcherImage = imageObj.AddComponent<Image>();
        if (watcherSprite != null)
        {
            watcherImage.sprite = watcherSprite;
        }

        RectTransform imgRect = imageObj.GetComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.sizeDelta = watcherSpriteSize;
        imgRect.anchoredPosition = Vector2.zero;
    }

    private void DestroyFullScreenFlashUI()
    {
        if (fullScreenFlashObj != null)
        {
            Destroy(fullScreenFlashObj);
            fullScreenFlashObj = null;
            watcherImage = null;
        }
    }

    private void CreateWatcherTextUI()
    {
        if (uiCanvasObj == null) return;

        textPanelObj = new GameObject("WatcherTextPanel", typeof(RectTransform));
        textPanelObj.transform.SetParent(uiCanvasObj.transform, false);

        textCanvasGroup = textPanelObj.AddComponent<CanvasGroup>();

        textRectTransform = textPanelObj.GetComponent<RectTransform>();
        textRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textRectTransform.sizeDelta = new Vector2(800f, 120f);

        textRectTransform.anchoredPosition = CalculateTextPosition();

        GameObject textObj = new GameObject("WatcherText", typeof(RectTransform));
        textObj.transform.SetParent(textPanelObj.transform, false);

        watcherText = textObj.AddComponent<Text>();
        watcherText.text = "";

        if (customFont != null)
        {
            watcherText.font = customFont;
        }
        else
        {
            watcherText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        watcherText.fontSize = textFontSize;
        watcherText.alignment = TextAnchor.MiddleCenter;
        watcherText.color = Color.white;

        RectTransform innerTextRect = textObj.GetComponent<RectTransform>();
        innerTextRect.anchorMin = Vector2.zero;
        innerTextRect.anchorMax = Vector2.one;
        innerTextRect.sizeDelta = Vector2.zero;

        perCharShake = textObj.AddComponent<PerCharacterShake>();
        perCharShake.shakeMagnitude = letterShakeMagnitude;
        perCharShake.shakeSpeed = letterShakeSpeed;
        perCharShake.isShaking = true;
    }

    private Vector2 CalculateTextPosition()
    {
        switch (textPositionMode)
        {
            case WatcherTextPosition.Top:
                return new Vector2(0f, 350f);

            case WatcherTextPosition.Bottom:
                return new Vector2(0f, -350f);

            case WatcherTextPosition.CustomOffset:
                return textCustomOffset;

            case WatcherTextPosition.Center:
            default:
                return Vector2.zero;
        }
    }

    // --- FLASHING EFFECT ---
    private IEnumerator FlashUIRoutine(float duration)
    {
        if (fullScreenFlashObj == null) yield break;

        float elapsed = 0f;
        bool isVisible = true;
        float flashInterval = 0.12f;
        float nextFlashTime = 0f;

        CanvasGroup flashCanvasGroup = fullScreenFlashObj.AddComponent<CanvasGroup>();

        while (elapsed < duration)
        {
            if (Time.time >= nextFlashTime)
            {
                isVisible = !isVisible;
                if (flashCanvasGroup != null)
                {
                    flashCanvasGroup.alpha = isVisible ? 1f : 0.15f;
                }
                nextFlashTime = Time.time + flashInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flashCanvasGroup != null)
        {
            flashCanvasGroup.alpha = 1f;
        }
    }

    // --- TYPEWRITER TEXT ANIMATION ---
    private IEnumerator TypewriterTextRoutine(string fullText)
    {
        if (watcherText == null) yield break;

        watcherText.text = "";
        for (int i = 0; i < fullText.Length; i++)
        {
            watcherText.text += fullText[i];

            if (typewriterSound != null && audioSource != null && fullText[i] != ' ')
            {
                audioSource.PlayOneShot(typewriterSound, 0.4f);
            }

            yield return new WaitForSeconds(typewriterSpeed);
        }
    }

    // --- MAP ITEM SPAWNING ---
    private void SpawnFogItem()
    {
        DestroyFogItem(false);

        float effectiveRadius = mapSpawnRadius;
        Vector3 centerPoint = mapCenterPosition;

        // Fetch dynamic map radius and center point directly from MapGenerator
        MapGenerator mapGen = MapGenerator.Instance != null ? MapGenerator.Instance : FindFirstObjectByType<MapGenerator>();
        if (useMapGeneratorRadius && mapGen != null)
        {
            centerPoint = mapGen.transform.position;
            effectiveRadius = mapGen.mapRadius; // Change variable name if different in MapGenerator
        }

        Vector3 spawnPos = centerPoint;
        bool foundGround = false;
        int maxAttempts = 30;

        // Raycast downwards anywhere within map bounds to snap cleanly onto floor geometry
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * effectiveRadius;
            Vector3 candidatePos = centerPoint + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 rayOrigin = candidatePos + Vector3.up * 50f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
            {
                if (groundLayer.value == 0 || ((1 << hit.collider.gameObject.layer) & groundLayer.value) != 0)
                {
                    spawnPos = hit.point + Vector3.up * groundYOffset;
                    foundGround = true;
                    break;
                }
            }
        }

        // Fallback positioning if raycast misses walkable geometry
        if (!foundGround)
        {
            Vector2 fallbackCircle = Random.insideUnitCircle * effectiveRadius;
            spawnPos = centerPoint + new Vector3(fallbackCircle.x, 0f, fallbackCircle.y);
            spawnPos.y = groundYPosition;
        }

        if (fogItemPrefab != null)
        {
            currentSpawnedFogItem = Instantiate(fogItemPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            currentSpawnedFogItem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            currentSpawnedFogItem.transform.position = spawnPos;
            currentSpawnedFogItem.GetComponent<Collider>().isTrigger = true;

            Renderer ren = currentSpawnedFogItem.GetComponent<Renderer>();
            if (ren != null) ren.material.color = Color.cyan;
        }

        WatcherFogItem fogItem = currentSpawnedFogItem.GetComponent<WatcherFogItem>();
        if (fogItem == null)
        {
            fogItem = currentSpawnedFogItem.AddComponent<WatcherFogItem>();
        }

        fogItem.Initialize(() => {
            isItemCollected = true;
        });
    }

    private void DestroyFogItem(bool allowParticlesToFade = false, float fadeDuration = 3f)
    {
        if (currentSpawnedFogItem != null)
        {
            if (allowParticlesToFade)
            {
                Renderer[] renderers = currentSpawnedFogItem.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (!(r is ParticleSystemRenderer)) r.enabled = false;
                }

                Collider[] colliders = currentSpawnedFogItem.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;

                ParticleSystem[] particleSystems = currentSpawnedFogItem.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                Destroy(currentSpawnedFogItem, fadeDuration);
                currentSpawnedFogItem = null;
            }
            else
            {
                Destroy(currentSpawnedFogItem);
                currentSpawnedFogItem = null;
            }
        }
    }

    // --- FADE OUT & CLEANUP ---
    private IEnumerator FadeOutAndDestroyTextUIRoutine(float fadeDuration)
    {
        if (textCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (textCanvasGroup == null) yield break;
            textCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        DestroyUI();
    }

    private void DestroyUI()
    {
        if (activeTypewriterCoroutine != null)
        {
            StopCoroutine(activeTypewriterCoroutine);
            activeTypewriterCoroutine = null;
        }

        DestroyFullScreenFlashUI();

        if (textPanelObj != null)
        {
            Destroy(textPanelObj);
            textPanelObj = null;
            watcherText = null;
            textRectTransform = null;
            textCanvasGroup = null;
            perCharShake = null;
        }

        if (uiCanvasObj != null)
        {
            Destroy(uiCanvasObj);
            uiCanvasObj = null;
        }
    }

    private void RestoreCameraFarClip()
    {
        if (originalFarClipPlane > 0f && Camera.main != null)
        {
            Camera.main.farClipPlane = originalFarClipPlane;
            originalFarClipPlane = -1f;
        }
    }

    private IEnumerator ApplyBlindnessPenaltyRoutine(float duration)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null && originalFarClipPlane < 0f)
        {
            originalFarClipPlane = mainCam.farClipPlane;
        }

        GameObject visionOverlayObj = null;
        CanvasGroup overlayCanvasGroup = null;

        if (uiCanvasObj != null && visionOverlaySprite != null)
        {
            visionOverlayObj = new GameObject("VisionPenaltyOverlay", typeof(RectTransform));
            visionOverlayObj.transform.SetParent(uiCanvasObj.transform, false);

            Image overlayImg = visionOverlayObj.AddComponent<Image>();
            overlayImg.sprite = visionOverlaySprite;
            overlayImg.color = Color.white;

            RectTransform overlayRect = visionOverlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            overlayCanvasGroup = visionOverlayObj.AddComponent<CanvasGroup>();
            overlayCanvasGroup.alpha = 0f;
        }

        float fadeTime = 0.5f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            if (mainCam != null && originalFarClipPlane > 0f)
                mainCam.farClipPlane = Mathf.Lerp(originalFarClipPlane, penaltyFarClipPlane, t);

            yield return null;
        }

        yield return new WaitForSeconds(duration);

        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            if (mainCam != null && originalFarClipPlane > 0f)
                mainCam.farClipPlane = Mathf.Lerp(penaltyFarClipPlane, originalFarClipPlane, t);

            yield return null;
        }

        RestoreCameraFarClip();
        if (visionOverlayObj != null) Destroy(visionOverlayObj);
        activeVisionPenaltyCoroutine = null;
    }
}