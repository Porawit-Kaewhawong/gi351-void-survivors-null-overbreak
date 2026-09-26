using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class StopSignEnemy : EnemyBase
{
    [Header("Stop Sign UI Sprites")]
    [Tooltip("Sprite for the Yellow Warning sign (Stage 1).")]
    [SerializeField] private Sprite yellowSignSprite;

    [Tooltip("Sprite for the Red Stop sign (Stage 2 - Player kept moving).")]
    [SerializeField] private Sprite redSignSprite;

    [Tooltip("Sprite for the Green Success sign (Stage 3 - Player stopped).")]
    [SerializeField] private Sprite greenSignSprite;

    [Header("Audio Settings for 3 Signs")]
    [Tooltip("Sound played when the yellow warning sign appears.")]
    [SerializeField] private AudioClip yellowSound;
    [Tooltip("Sound played when the red sign triggers (penalty).")]
    [SerializeField] private AudioClip redSound;
    [Tooltip("Sound played when the green success sign appears.")]
    [SerializeField] private AudioClip greenSound;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before the stop sign can first appear after spawn.")]
    [SerializeField] private float initialDelay = 12f;

    [Tooltip("Minimum time interval between random stop sign appearances.")]
    [SerializeField] private float minSpawnInterval = 15f;

    [Tooltip("Maximum time interval between random stop sign appearances.")]
    [SerializeField] private float maxSpawnInterval = 30f;

    private GameObject uiCanvasObj;
    private Image signImage;
    private AudioSource audioSource;
    private bool isEventActive = false;

    protected override void Awake()
    {
        base.Awake();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        StartCoroutine(RandomStopSignRoutine());
    }

    // --- RANDOM REPEATING EVENT LOOP ---
    private IEnumerator RandomStopSignRoutine()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            if (!isEventActive)
            {
                
                yield return StartCoroutine(TriggerStopSignEventRoutine());
            }
        }
    }

    // --- STOP SIGN EVENT SEQUENCE ---
    private IEnumerator TriggerStopSignEventRoutine()
    {
        isEventActive = true;
        Debug.Log("[StopSignEnemy] Stop Sign event appeared!");

        CreateStopSignUI();

        // Stage 1: Yellow Warning Sign
        if (yellowSignSprite != null && signImage != null)
        {
            signImage.sprite = yellowSignSprite;
        }
        PlaySignSound(yellowSound);

        float warningDuration = 1.5f;
        float elapsed = 0f;
        bool playerMovedDuringWarning = false;

        while (elapsed < warningDuration)
        {
            if (IsPlayerMoving())
            {
                playerMovedDuringWarning = true;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Stage 2: Red Sign (Player chose to keep moving)
        if (playerMovedDuringWarning || IsPlayerMoving())
        {
            if (redSignSprite != null && signImage != null)
            {
                signImage.sprite = redSignSprite;
            }
            PlaySignSound(redSound);

            if (playerController != null)
            {
                playerController.TakeDamage(attackDamage);
            }
            Debug.Log("[StopSignEnemy] Player kept moving! Red sign penalty applied.");

            while (IsPlayerMoving())
            {
                yield return null;
            }
        }

        // Stage 3: Green Sign (Player has successfully stopped)
        if (greenSignSprite != null && signImage != null)
        {
            signImage.sprite = greenSignSprite;
        }
        PlaySignSound(greenSound);
        Debug.Log("[StopSignEnemy] Player stopped! Showing green sign and fading out.");

        yield return StartCoroutine(FadeOutAndDestroyUIRoutine());

        isEventActive = false;
    }

    // --- PLAYER MOVEMENT DETECTION ---
    private bool IsPlayerMoving()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        return (Mathf.Abs(moveX) > 0.1f || Mathf.Abs(moveZ) > 0.1f);
    }

    // --- DYNAMIC UI CREATION ---
    private void CreateStopSignUI()
    {
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        uiCanvasObj = new GameObject("StopSignCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        uiCanvasObj.AddComponent<CanvasScaler>();
        uiCanvasObj.AddComponent<GraphicRaycaster>();

        GameObject panelObj = new GameObject("StopSignImage");
        panelObj.transform.SetParent(uiCanvasObj.transform, false);

        signImage = panelObj.AddComponent<Image>();

        RectTransform rect = panelObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(250f, 250f);
        rect.anchoredPosition = Vector2.zero;
    }

    // --- FADE OUT EFFECT ---
    private IEnumerator FadeOutAndDestroyUIRoutine()
    {
        if (signImage == null) yield break;

        float fadeDuration = 1.0f;
        float elapsed = 0f;
        Color startColor = signImage.color;

        while (elapsed < fadeDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            signImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (uiCanvasObj != null)
        {
            Destroy(uiCanvasObj);
        }
    }

    // --- AUDIO HELPER ---
    private void PlaySignSound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}