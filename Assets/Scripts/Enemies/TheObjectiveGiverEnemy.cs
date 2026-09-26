using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TheObjectiveGiverEnemy : EnemyBase
{
    [Header("Objective Settings")]
    [Tooltip("Audio clip played when an objective appears.")]
    [SerializeField] private AudioClip objectiveSound;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before the objective giver can first appear after spawn.")]
    [SerializeField] private float initialDelay = 12f;

    [Tooltip("Minimum time interval between random objective appearances.")]
    [SerializeField] private float minSpawnInterval = 18f;

    [Tooltip("Maximum time interval between random objective appearances.")]
    [SerializeField] private float maxSpawnInterval = 35f;

    private GameObject uiCanvasObj;
    private Text objectiveText;
    private AudioSource audioSource;
    private bool isEventActive = false;

    protected override void Awake()
    {
        base.Awake();

        // Setup AudioSource component for sound effects
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // Start the random repeating event loop
        StartCoroutine(RandomObjectiveRoutine());
    }

    // --- RANDOM REPEATING EVENT LOOP ---
    private IEnumerator RandomObjectiveRoutine()
    {
        // Wait for initial delay so it doesn't appear right when the player spawns
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            // Wait for a random interval (not too fast, not too slow)
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            // Trigger objective event multiple times during gameplay
            if (!isEventActive)
            {
                yield return StartCoroutine(TriggerObjectiveEventRoutine());
            }
        }
    }

    // --- OBJECTIVE EVENT SEQUENCE ---
    private IEnumerator TriggerObjectiveEventRoutine()
    {
        isEventActive = true;
        Debug.Log("[TheObjectiveGiver] Objective event started!");

        // Randomly pick an objective type: 0 = Do Not Jump, 1 = Do Not Move
        int objectiveType = Random.Range(0, 2);
        string instruction = objectiveType == 0 ? "DO NOT JUMP!" : "DO NOT MOVE!";

        // Create large UI in the center of the screen (No black background)
        CreateObjectiveUI(instruction);

        // Play warning sound effect
        if (objectiveSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(objectiveSound);
        }

        // Duration to show the objective and track player actions (approx 2.5 seconds)
        float duration = 2.5f;
        float elapsed = 0f;
        bool ruleBroken = false;

        RectTransform textRect = objectiveText != null ? objectiveText.GetComponent<RectTransform>() : null;
        Vector3 originalPos = textRect != null ? textRect.anchoredPosition : Vector3.zero;

        // Monitor player inputs and shake UI
        while (elapsed < duration)
        {
            // Check if player violates the rule
            if (objectiveType == 0) // Rule: Do Not Jump
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    ruleBroken = true;
                }
            }
            else if (objectiveType == 1) // Rule: Do Not Move
            {
                if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                {
                    ruleBroken = true;
                }
            }

            // Shake the UI text vigorously in the center of the screen
            if (textRect != null)
            {
                Vector2 shakeOffset = Random.insideUnitCircle * 18f; // Large shake intensity
                textRect.anchoredPosition = originalPos + (Vector3)shakeOffset;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset text position
        if (textRect != null) textRect.anchoredPosition = originalPos;

        // Apply penalty if the player broke the rule
        if (ruleBroken)
        {
            if (playerController != null)
            {
                playerController.TakeDamage(5f); // Reduce player HP by 5
            }
            Debug.Log("[TheObjectiveGiver] Rule broken! Player took 5 damage penalty.");
        }
        else
        {
            Debug.Log("[TheObjectiveGiver] Objective successfully followed!");
        }

        // Gradually fade out and destroy the UI
        yield return StartCoroutine(FadeOutAndDestroyUIRoutine());

        isEventActive = false;
    }

    // --- DYNAMIC UI CREATION (LARGE CENTER TEXT, NO BLACK BACKGROUND) ---
    private void CreateObjectiveUI(string message)
    {
        if (uiCanvasObj != null) Destroy(uiCanvasObj);

        // Create Canvas in screen space overlay
        uiCanvasObj = new GameObject("ObjectiveCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        uiCanvasObj.AddComponent<CanvasScaler>();
        uiCanvasObj.AddComponent<GraphicRaycaster>();

        // Create Text element directly in the center (No background panel)
        GameObject textObj = new GameObject("ObjectiveText");
        textObj.transform.SetParent(uiCanvasObj.transform, false);

        objectiveText = textObj.AddComponent<Text>();
        objectiveText.text = message;
        objectiveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        objectiveText.fontSize = 42; // Large text size
        objectiveText.alignment = TextAnchor.MiddleCenter;
        objectiveText.color = Color.yellow; // High-visibility color

        // Add an Outline component to make the text pop clearly without a background
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3, -3);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.2f, 0.4f);
        textRect.anchorMax = new Vector2(0.8f, 0.6f);
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
    }

    // --- FADE OUT EFFECT ROUTINE ---
    private IEnumerator FadeOutAndDestroyUIRoutine()
    {
        if (uiCanvasObj == null) yield break;

        float fadeDuration = 0.8f;
        float elapsed = 0f;

        // Use CanvasGroup to fade out smoothly
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