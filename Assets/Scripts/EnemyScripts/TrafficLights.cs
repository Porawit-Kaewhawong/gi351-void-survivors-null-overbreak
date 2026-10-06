using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum SignScreenPosition
{
    Center,
    CustomOffset,
    RandomScreen
}

public class StopSignEnemy : EnemyBase
{
    public override bool IsTargetable => false;

    [Header("Stop Sign UI Sprites")]
    [Tooltip("Sprite for the Green sign (Stage 1 - Initial spawn / safe to stop).")]
    [SerializeField] private Sprite greenSignSprite;

    [Tooltip("Sprite for the Orange sign (Stage 2 - Warning / time running out).")]
    [SerializeField] private Sprite orangeSignSprite;

    [Tooltip("Sprite for the Red sign (Stage 3 - Critical warning / final chance to stop).")]
    [SerializeField] private Sprite redSignSprite;

    [Header("Audio Settings")]
    [Tooltip("Sound played when the green sign appears on screen.")]
    [SerializeField] private AudioClip greenSound;

    [Tooltip("Sound played when the warning escalates to orange.")]
    [SerializeField] private AudioClip orangeSound;

    [Tooltip("Sound played when the red sign triggers (critical warning window).")]
    [SerializeField] private AudioClip redSound;

    [Tooltip("Optional sound played when damage penalty is applied at end of reaction window.")]
    [SerializeField] private AudioClip damageSound;

    [Tooltip("Sound played when the stop sign fades out and disappears.")]
    [SerializeField] private AudioClip fadeOutSound;

    [Tooltip("Looping ambient sound played continuously while the stop sign event is active (e.g. warning hum/siren).")]
    [SerializeField] private AudioClip ambientSound;

    [Range(0f, 1f)]
    [Tooltip("Maximum volume for ambient SFX.")]
    [SerializeField] private float ambientVolume = 0.75f;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before the stop sign can first appear after spawn.")]
    [SerializeField] private float initialDelay = 12f;

    [Tooltip("Minimum time interval between random stop sign appearances.")]
    [SerializeField] private float minSpawnInterval = 15f;

    [Tooltip("Maximum time interval between random stop sign appearances.")]
    [SerializeField] private float maxSpawnInterval = 30f;

    [Header("Operator Reaction Settings")]
    [Tooltip("Total time limit (in seconds) the player has to stop moving before taking damage. Automatically divided into Green, Orange, and Red phases.")]
    [SerializeField] private float reactionWindow = 3.0f;

    [Header("UI Position & Size Settings")]
    [Tooltip("Dimensions (Width, Height) of the Stop Sign sprite on screen relative to 1920x1080.")]
    [SerializeField] private Vector2 signSize = new Vector2(600f, 600f);

    [Tooltip("Controls where on the screen the stop sign UI appears.")]
    [SerializeField] private SignScreenPosition positionMode = SignScreenPosition.Center;

    [Tooltip("Offset relative to screen center (X, Y) when using CustomOffset mode.")]
    [SerializeField] private Vector2 customPositionOffset = Vector2.zero;

    [Tooltip("Margin padding (in pixels) from screen edges when using RandomScreen position mode.")]
    [SerializeField] private Vector2 randomScreenPadding = new Vector2(150f, 150f);

    [Header("Visual Animation & Fade Settings")]
    [Tooltip("Duration in seconds for the stop sign to fade in when appearing.")]
    [SerializeField] private float fadeInDuration = 0.2f;

    [Tooltip("Duration in seconds for the stop sign to fade out when vanishing.")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Tooltip("How long the green success sign stays visible before fading out when player is standing still.")]
    [SerializeField] private float minSuccessDisplayTime = 0.5f;

    [Header("Shake Settings")]
    [Tooltip("Duration (in seconds) for the shake effect during color changes and transitions.")]
    [SerializeField] private float shakeDuration = 0.25f;

    [Tooltip("Intensity/magnitude of the UI shake (in pixels).")]
    [SerializeField] private float shakeMagnitude = 15f;

    private GameObject uiCanvasObj;
    private Image signImage;
    private RectTransform signRectTransform;
    private CanvasGroup canvasGroup;

    private AudioSource sfxAudioSource;
    private AudioSource ambientAudioSource;
    private Coroutine shakeCoroutine;
    private Coroutine ambientFadeCoroutine;
    private bool isEventActive = false;

    protected override void Awake()
    {
        base.Awake();

        // Setup dual AudioSources for independent SFX and Ambient handling
        AudioSource[] existingSources = GetComponents<AudioSource>();
        sfxAudioSource = existingSources.Length > 0 ? existingSources[0] : gameObject.AddComponent<AudioSource>();
        ambientAudioSource = existingSources.Length > 1 ? existingSources[1] : gameObject.AddComponent<AudioSource>();

        ambientAudioSource.loop = true;
        ambientAudioSource.playOnAwake = false;

        DisableWorldInteraction();
    }

    private void Start()
    {
        StartCoroutine(RandomStopSignRoutine());
    }

    private void OnDisable()
    {
        DestroyUI();
        StopAmbientSound(0f);
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
    private IEnumerator RandomStopSignRoutine()
    {
        float effectiveInitialDelay = initialDelay / StackCount;
        yield return new WaitForSeconds(effectiveInitialDelay);

        while (true)
        {
            float minTime = minSpawnInterval / StackCount;
            float maxTime = maxSpawnInterval / StackCount;

            float waitTime = Random.Range(minTime, maxTime);
            yield return new WaitForSeconds(waitTime);

            if (!isEventActive)
            {
                yield return StartCoroutine(TriggerStopSignEventRoutine());
            }
        }
    }

    // --- OPERATOR STOP SIGN EVENT SEQUENCE ---
    private IEnumerator TriggerStopSignEventRoutine()
    {
        isEventActive = true;
        Debug.Log("[StopSignEnemy] Stop Sign appeared! Stand still to survive.");

        CreateStopSignUI();

        // Start Ambient Sound Fade-In
        StartAmbientSound(fadeInDuration);

        // Stage 1: Green Sign & Start Fade In + Shake
        SetSignSprite(greenSignSprite);
        PlaySignSound(greenSound);
        TriggerShake(shakeDuration, shakeMagnitude);

        StartCoroutine(FadeCanvasGroupRoutine(canvasGroup, 0f, 1f, fadeInDuration));

        float timer = reactionWindow;

        float orangeTimeTrigger = reactionWindow * (2f / 3f);
        float redTimeTrigger = reactionWindow * (1f / 3f);

        bool switchedToOrange = false;
        bool switchedToRed = false;
        bool playerSuccessfullyStopped = false;

        while (timer > 0f)
        {
            if (!IsPlayerMoving())
            {
                playerSuccessfullyStopped = true;
                break;
            }

            // Phase 2: Switch to Orange + Shake
            if (!switchedToOrange && timer <= orangeTimeTrigger)
            {
                switchedToOrange = true;
                SetSignSprite(orangeSignSprite);
                PlaySignSound(orangeSound);
                TriggerShake(shakeDuration, shakeMagnitude);
                Debug.Log("[StopSignEnemy] Warning escalated to ORANGE!");
            }

            // Phase 3: Switch to Red + Shake
            if (!switchedToRed && timer <= redTimeTrigger)
            {
                switchedToRed = true;
                SetSignSprite(redSignSprite);
                PlaySignSound(redSound);
                TriggerShake(shakeDuration, shakeMagnitude);
                Debug.Log("[StopSignEnemy] Warning escalated to RED! Final chance to stop!");
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        // Resolution Phase
        if (playerSuccessfullyStopped)
        {
            SetSignSprite(greenSignSprite);
            TriggerShake(shakeDuration, shakeMagnitude);

            yield return new WaitForSeconds(minSuccessDisplayTime);

            // Play Fade Out SFX, stop ambient, and fade UI canvas
            PlaySignSound(fadeOutSound);
            StopAmbientSound(fadeOutDuration);
            TriggerShake(fadeOutDuration, shakeMagnitude);
            yield return StartCoroutine(FadeCanvasGroupRoutine(canvasGroup, canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutDuration));
            DestroyUI();
        }
        else
        {
            Debug.Log("[StopSignEnemy] Player failed to stop within reaction window! Damage penalty applied.");

            if (damageSound != null)
            {
                PlaySignSound(damageSound);
            }

            if (playerController != null)
            {
                playerController.TakeDamage(attackDamage);
            }

            TriggerShake(shakeDuration * 1.5f, shakeMagnitude * 1.5f);

            yield return new WaitForSeconds(0.3f);

            // Play Fade Out SFX, stop ambient, and fade UI canvas
            PlaySignSound(fadeOutSound);
            StopAmbientSound(fadeOutDuration);
            TriggerShake(fadeOutDuration, shakeMagnitude);
            yield return StartCoroutine(FadeCanvasGroupRoutine(canvasGroup, canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutDuration));
            DestroyUI();
        }

        isEventActive = false;
    }

    // --- PLAYER MOVEMENT DETECTION ---
    private bool IsPlayerMoving()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        return (Mathf.Abs(moveX) > 0.05f || Mathf.Abs(moveZ) > 0.05f);
    }

    // --- UI CREATION, POSITIONING, SHAKE & ANIMATION ---
    private void CreateStopSignUI()
    {
        DestroyUI();

        uiCanvasObj = new GameObject("StopSignCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        // Configure CanvasScaler for 1920x1080 resolution scaling
        CanvasScaler scaler = uiCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        uiCanvasObj.AddComponent<GraphicRaycaster>();

        canvasGroup = uiCanvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        GameObject panelObj = new GameObject("StopSignImage");
        panelObj.transform.SetParent(uiCanvasObj.transform, false);

        signImage = panelObj.AddComponent<Image>();

        signRectTransform = panelObj.GetComponent<RectTransform>();
        signRectTransform.sizeDelta = signSize; // Driven by signSize field

        signRectTransform.anchoredPosition = CalculateSignPosition();
    }

    private Vector2 CalculateSignPosition()
    {
        switch (positionMode)
        {
            case SignScreenPosition.CustomOffset:
                return customPositionOffset;

            case SignScreenPosition.RandomScreen:
                float halfWidth = Mathf.Max(0f, (Screen.width / 2f) - randomScreenPadding.x);
                float halfHeight = Mathf.Max(0f, (Screen.height / 2f) - randomScreenPadding.y);

                float randomX = Random.Range(-halfWidth, halfWidth);
                float randomY = Random.Range(-halfHeight, halfHeight);
                return new Vector2(randomX, randomY);

            case SignScreenPosition.Center:
            default:
                return Vector2.zero;
        }
    }

    private void TriggerShake(float duration, float magnitude)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }
        if (signRectTransform != null)
        {
            shakeCoroutine = StartCoroutine(ShakeUIRoutine(duration, magnitude));
        }
    }

    private IEnumerator ShakeUIRoutine(float duration, float magnitude)
    {
        if (signRectTransform == null) yield break;

        Vector2 originalPos = signRectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (signRectTransform == null) yield break;

            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            signRectTransform.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (signRectTransform != null)
        {
            signRectTransform.anchoredPosition = originalPos;
        }
    }

    private IEnumerator FadeCanvasGroupRoutine(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        if (cg == null || duration <= 0f) yield break;

        float elapsed = 0f;
        cg.alpha = startAlpha;

        while (elapsed < duration)
        {
            if (cg == null) yield break;
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }

        if (cg != null)
        {
            cg.alpha = endAlpha;
        }
    }

    // --- AMBIENT AUDIO FADE LOGIC ---
    private void StartAmbientSound(float fadeDuration)
    {
        if (ambientSound == null || ambientAudioSource == null) return;

        if (ambientFadeCoroutine != null)
        {
            StopCoroutine(ambientFadeCoroutine);
        }

        ambientAudioSource.clip = ambientSound;
        if (!ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Play();
        }

        ambientFadeCoroutine = StartCoroutine(FadeAudioRoutine(ambientAudioSource, ambientAudioSource.volume, ambientVolume, fadeDuration, false));
    }

    private void StopAmbientSound(float fadeDuration)
    {
        if (ambientAudioSource == null || !ambientAudioSource.isPlaying) return;

        if (ambientFadeCoroutine != null)
        {
            StopCoroutine(ambientFadeCoroutine);
        }

        ambientFadeCoroutine = StartCoroutine(FadeAudioRoutine(ambientAudioSource, ambientAudioSource.volume, 0f, fadeDuration, true));
    }

    private IEnumerator FadeAudioRoutine(AudioSource src, float startVol, float endVol, float duration, bool stopOnComplete)
    {
        if (src == null) yield break;

        if (duration <= 0f)
        {
            src.volume = endVol;
            if (stopOnComplete) src.Stop();
            yield break;
        }

        float elapsed = 0f;
        src.volume = startVol;

        while (elapsed < duration)
        {
            if (src == null) yield break;
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, endVol, elapsed / duration);
            yield return null;
        }

        if (src != null)
        {
            src.volume = endVol;
            if (stopOnComplete) src.Stop();
        }
    }

    private void SetSignSprite(Sprite sprite)
    {
        if (signImage != null && sprite != null)
        {
            signImage.sprite = sprite;
        }
    }

    private void DestroyUI()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (uiCanvasObj != null)
        {
            Destroy(uiCanvasObj);
            uiCanvasObj = null;
            signImage = null;
            signRectTransform = null;
            canvasGroup = null;
        }
    }

    private void PlaySignSound(AudioClip clip)
    {
        if (clip != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(clip);
        }
    }
}