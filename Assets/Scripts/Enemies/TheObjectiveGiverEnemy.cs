using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TheObjectiveGiverEnemy : EnemyBase
{
    public override bool IsTargetable => false;

    public enum ObjectiveType
    {
        MaxJumpsAllowed,     // Allow up to N jumps (e.g., max 3)
        HoldCameraStill,     // Must keep camera static for N continuous seconds (e.g., 1.0s)
        MustJump             // Airborne state required before window expires
    }

    [Header("Retro OS Window Visuals")]
    [Tooltip("Dimensions (Width, Height) of the error pop-up window.")]
    [SerializeField] private Vector2 windowSize = new Vector2(560f, 260f);

    [Tooltip("Padding in UI pixels from the screen edges so the window never touches the border.")]
    [SerializeField] private float screenEdgePadding = 40f;

    [Tooltip("Window background color (classic Win95 Gray is #C0C0C0).")]
    [SerializeField] private Color windowBackgroundColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Tooltip("Title bar background color (classic Win95 Dark Blue is #000080).")]
    [SerializeField] private Color titleBarColor = new Color(0f, 0f, 0.5f, 1f);

    [Tooltip("Font to use for retro window text. Drag any TTF/OTF font here if auto-detect fails.")]
    [SerializeField] private Font customFont;

    [Header("Objective 1: JUMP BUDGET (MAX 3 JUMPS)")]
    [Tooltip("Maximum allowed jumps before breaking the objective rule.")]
    [SerializeField] private int maxAllowedJumps = 3;
    [SerializeField] private string maxJumpsHeader = "system_error.exe - [JUMP BUDGET EXCEEDED]";
    [SerializeField]
    private string[] maxJumpsMessages = new string[]
    {
        "DO NOT JUMP MORE THAN 3 TIMES.",
        "JUMP ALLOTMENT: 3 MAXIMUM.",
        "EXCEEDING 3 JUMPS WILL CAUSE DESYNC."
    };

    [Header("Objective 2: CAMERA CALIBRATION (1 SECOND STILL)")]
    [Tooltip("Continuous seconds the camera must remain still during the event window.")]
    [SerializeField] private float requiredStaticDuration = 1.0f;
    [SerializeField] private string stillnessHeader = "system_warning.sys - [VISION CALIBRATION]";
    [SerializeField]
    private string[] stillnessMessages = new string[]
    {
        "HOLD CAMERA STILL FOR 1 SECOND.",
        "FREEZE VISION FOR 1.0s TO RECALIBRATE.",
        "PAUSE LOOK INPUT FOR 1 CONTINUOUS SECOND."
    };

    [Header("Objective 3: AIRBORNE (MUST JUMP)")]
    [SerializeField] private string airborneHeader = "system_error.exe - [GRAVITY DESYNC]";
    [SerializeField]
    private string[] airborneMessages = new string[]
    {
        "THE FLOOR IS NO LONGER SAFE. ELEVATE.",
        "AIRBORNE STATE REQUIRED IMMEDIATELY.",
        "LEAVE THE GROUND AT LEAST ONCE BEFORE TIME EXPIRES."
    };

    [Header("Duration & Sensitivity Settings")]
    [Tooltip("Duration in seconds that the objective window remains active and monitors player input.")]
    [SerializeField] private float objectiveDuration = 2.5f;

    [Tooltip("Sensitivity threshold for detecting camera/mouse movement during 'Hold Camera Still'.")]
    [SerializeField] private float mouseSensitivityThreshold = 0.15f;

    [Tooltip("Intensity of retro digital jitter/shake applied to the pop-up window.")]
    [SerializeField] private float windowShakeMagnitude = 6f;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before the objective giver can first appear after spawn.")]
    [SerializeField] private float initialDelay = 12f;

    [Tooltip("Minimum time interval between random objective appearances.")]
    [SerializeField] private float minSpawnInterval = 18f;

    [Tooltip("Maximum time interval between random objective appearances.")]
    [SerializeField] private float maxSpawnInterval = 35f;

    [Header("Audio Settings")]
    [Tooltip("16-bit chime or error sound played when the pop-up appears.")]
    [SerializeField] private AudioClip popUpSound;

    [Tooltip("Error sound played if the player violates the objective rule.")]
    [SerializeField] private AudioClip failSound;

    [Tooltip("Click/Resolve sound played if the player successfully follows the rule.")]
    [SerializeField] private AudioClip successSound;

    private GameObject uiCanvasObj;
    private RectTransform windowRect;
    private CanvasGroup canvasGroup;
    private AudioSource audioSource;
    private bool isEventActive = false;
    private Coroutine shakeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        DisableWorldInteraction();
    }

    private void Start()
    {
        StartCoroutine(RandomObjectiveRoutine());
    }

    private void OnDisable()
    {
        DestroyUI();
    }

    protected override void Update() { }
    public override void TakeDamage(float amount) { }

    private void DisableWorldInteraction()
    {
        Collider col3D = GetComponent<Collider>(); if (col3D != null) col3D.enabled = false;
        Collider2D col2D = GetComponent<Collider2D>(); if (col2D != null) col2D.enabled = false;
        gameObject.layer = 2; // Ignore Raycast
    }

    // --- FONT FALLBACK HELPER ---
    private Font GetValidFont()
    {
        if (customFont != null) return customFont;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) return font;

        return Font.CreateDynamicFontFromOSFont("Arial", 16);
    }

    // --- RANDOM REPEATING EVENT LOOP ---
    private IEnumerator RandomObjectiveRoutine()
    {
        int safeStackCount = Mathf.Max(1, StackCount);
        yield return new WaitForSeconds(initialDelay / safeStackCount);

        while (true)
        {
            safeStackCount = Mathf.Max(1, StackCount);
            float waitTime = Random.Range(minSpawnInterval / safeStackCount, maxSpawnInterval / safeStackCount);
            yield return new WaitForSeconds(waitTime);

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

        ObjectiveType objective = (ObjectiveType)Random.Range(0, 3);

        string headerText = "";
        string[] msgPool = null;

        switch (objective)
        {
            case ObjectiveType.MaxJumpsAllowed:
                headerText = maxJumpsHeader;
                msgPool = maxJumpsMessages;
                break;
            case ObjectiveType.HoldCameraStill:
                headerText = stillnessHeader;
                msgPool = stillnessMessages;
                break;
            case ObjectiveType.MustJump:
                headerText = airborneHeader;
                msgPool = airborneMessages;
                break;
        }

        string bodyText = (msgPool != null && msgPool.Length > 0) ? msgPool[Random.Range(0, msgPool.Length)] : "SYSTEM ERROR DETECTED.";

        CreateRetroWindowUI(headerText, bodyText);

        if (popUpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(popUpSound);
        }

        StartCoroutine(FadeCanvasGroupRoutine(canvasGroup, 0f, 1f, 0.12f));
        TriggerShake(objectiveDuration, windowShakeMagnitude);

        float elapsed = 0f;
        bool ruleBroken = false;

        // Tracking variables
        int currentJumpsPerformed = 0;
        float currentStaticTime = 0f;
        bool achievedStaticHold = false;
        bool satisfiedJumpRequirement = false;

        while (elapsed < objectiveDuration)
        {
            switch (objective)
            {
                case ObjectiveType.MaxJumpsAllowed:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump"))
                    {
                        currentJumpsPerformed++;
                        if (currentJumpsPerformed > maxAllowedJumps)
                        {
                            ruleBroken = true;
                        }
                    }
                    break;

                case ObjectiveType.HoldCameraStill:
                    float mouseX = Mathf.Abs(Input.GetAxisRaw("Mouse X"));
                    float mouseY = Mathf.Abs(Input.GetAxisRaw("Mouse Y"));

                    if (mouseX <= mouseSensitivityThreshold && mouseY <= mouseSensitivityThreshold)
                    {
                        currentStaticTime += Time.deltaTime;
                        if (currentStaticTime >= requiredStaticDuration)
                        {
                            achievedStaticHold = true;
                        }
                    }
                    else
                    {
                        currentStaticTime = 0f;
                    }
                    break;

                case ObjectiveType.MustJump:
                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump"))
                    {
                        satisfiedJumpRequirement = true;
                    }
                    break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (objective == ObjectiveType.HoldCameraStill && !achievedStaticHold)
        {
            ruleBroken = true;
        }
        else if (objective == ObjectiveType.MustJump && !satisfiedJumpRequirement)
        {
            ruleBroken = true;
        }

        if (ruleBroken)
        {
            float penaltyDamage = attackDamage > 0f ? attackDamage : 5f;
            if (playerController != null) playerController.TakeDamage(penaltyDamage);
            if (failSound != null && audioSource != null) audioSource.PlayOneShot(failSound);
            Debug.Log($"[TheObjectiveGiver] Rule broken! Objective: {objective}. Player took {penaltyDamage} damage.");
        }
        else
        {
            if (successSound != null && audioSource != null) audioSource.PlayOneShot(successSound);
            Debug.Log($"[TheObjectiveGiver] Objective {objective} successfully followed!");
        }

        yield return StartCoroutine(FadeCanvasGroupRoutine(canvasGroup, 1f, 0f, 0.2f));
        DestroyUI();

        isEventActive = false;
    }

    // --- DYNAMIC RETRO OS UI CREATION ---
    private void CreateRetroWindowUI(string header, string message)
    {
        DestroyUI();

        uiCanvasObj = new GameObject("RetroOSCanvas");
        Canvas canvas = uiCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998;

        CanvasScaler scaler = uiCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        uiCanvasObj.AddComponent<GraphicRaycaster>();
        canvasGroup = uiCanvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        Font selectedFont = GetValidFont();

        // Main Window Panel
        GameObject windowObj = new GameObject("WindowPanel", typeof(RectTransform));
        windowObj.transform.SetParent(uiCanvasObj.transform, false);

        Image windowBg = windowObj.AddComponent<Image>();
        windowBg.color = windowBackgroundColor;

        Outline border = windowObj.AddComponent<Outline>();
        border.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        border.effectDistance = new Vector2(3, -3);

        windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.sizeDelta = windowSize;

        // --- SAFE RANDOM POSITION CALCULATION ---
        // Reference Canvas size is 1920 x 1080 centered at (0,0)
        // Ensure total margin factors in both UI edge padding and window shake magnitude
        float totalMargin = screenEdgePadding + windowShakeMagnitude;

        float maxAnchoredX = Mathf.Max(0f, (1920f * 0.5f) - (windowSize.x * 0.5f) - totalMargin);
        float maxAnchoredY = Mathf.Max(0f, (1080f * 0.5f) - (windowSize.y * 0.5f) - totalMargin);

        float randomX = Random.Range(-maxAnchoredX, maxAnchoredX);
        float randomY = Random.Range(-maxAnchoredY, maxAnchoredY);

        windowRect.anchoredPosition = new Vector2(randomX, randomY);

        // Title Bar Header
        GameObject titleBarObj = new GameObject("TitleBar", typeof(RectTransform));
        titleBarObj.transform.SetParent(windowObj.transform, false);

        Image titleBg = titleBarObj.AddComponent<Image>();
        titleBg.color = titleBarColor;

        RectTransform titleRect = titleBarObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 32f);

        // Title Text
        GameObject titleTextObj = new GameObject("TitleText", typeof(RectTransform));
        titleTextObj.transform.SetParent(titleBarObj.transform, false);

        Text titleTxt = titleTextObj.AddComponent<Text>();
        titleTxt.text = " " + header;
        titleTxt.font = selectedFont;
        titleTxt.fontSize = 15;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.color = Color.white;
        titleTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleTxt.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform titleTxtRect = titleTextObj.GetComponent<RectTransform>();
        titleTxtRect.anchorMin = Vector2.zero;
        titleTxtRect.anchorMax = Vector2.one;
        titleTxtRect.offsetMin = new Vector2(8f, 0f);
        titleTxtRect.offsetMax = new Vector2(-35f, 0f);

        // Close Button [X]
        GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform));
        closeBtnObj.transform.SetParent(titleBarObj.transform, false);

        Image closeBg = closeBtnObj.AddComponent<Image>();
        closeBg.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(22f, 22f);
        closeRect.anchoredPosition = new Vector2(-5f, 0f);

        GameObject closeTxtObj = new GameObject("XText", typeof(RectTransform));
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        Text closeTxt = closeTxtObj.AddComponent<Text>();
        closeTxt.text = "X";
        closeTxt.font = selectedFont;
        closeTxt.fontSize = 14;
        closeTxt.fontStyle = FontStyle.Bold;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.color = Color.black;
        closeTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        closeTxt.verticalOverflow = VerticalWrapMode.Overflow;
        closeTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);

        // Error Icon [!]
        GameObject iconObj = new GameObject("ErrorIcon", typeof(RectTransform));
        iconObj.transform.SetParent(windowObj.transform, false);

        Image iconBg = iconObj.AddComponent<Image>();
        iconBg.color = new Color(0.85f, 0.1f, 0.1f, 1f);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(48f, 48f);
        iconRect.anchoredPosition = new Vector2(24f, 10f);

        GameObject iconTxtObj = new GameObject("Exclamation", typeof(RectTransform));
        iconTxtObj.transform.SetParent(iconObj.transform, false);
        Text iconTxt = iconTxtObj.AddComponent<Text>();
        iconTxt.text = "!";
        iconTxt.font = selectedFont;
        iconTxt.fontSize = 32;
        iconTxt.fontStyle = FontStyle.Bold;
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.color = Color.white;
        iconTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        iconTxt.verticalOverflow = VerticalWrapMode.Overflow;
        iconTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);

        // Body Warning Text
        GameObject bodyTextObj = new GameObject("BodyText", typeof(RectTransform));
        bodyTextObj.transform.SetParent(windowObj.transform, false);

        Text bodyTxt = bodyTextObj.AddComponent<Text>();
        bodyTxt.text = message;
        bodyTxt.font = selectedFont;
        bodyTxt.fontSize = 18;
        bodyTxt.fontStyle = FontStyle.Bold;
        bodyTxt.alignment = TextAnchor.MiddleLeft;
        bodyTxt.color = Color.black;
        bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyTxt.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform bodyRect = bodyTextObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.pivot = new Vector2(0f, 0.5f);
        bodyRect.sizeDelta = new Vector2(-110f, 90f);
        bodyRect.anchoredPosition = new Vector2(88f, 10f);

        // OK Button
        GameObject okBtnObj = new GameObject("OKButton", typeof(RectTransform));
        okBtnObj.transform.SetParent(windowObj.transform, false);

        Image okBg = okBtnObj.AddComponent<Image>();
        okBg.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        Outline okOutline = okBtnObj.AddComponent<Outline>();
        okOutline.effectColor = Color.black;
        okOutline.effectDistance = new Vector2(1, -1);

        RectTransform okRect = okBtnObj.GetComponent<RectTransform>();
        okRect.anchorMin = new Vector2(0.5f, 0f);
        okRect.anchorMax = new Vector2(0.5f, 0f);
        okRect.pivot = new Vector2(0.5f, 0f);
        okRect.sizeDelta = new Vector2(90f, 30f);
        okRect.anchoredPosition = new Vector2(0f, 18f);

        GameObject okTxtObj = new GameObject("OKText", typeof(RectTransform));
        okTxtObj.transform.SetParent(okBtnObj.transform, false);
        Text okTxt = okTxtObj.AddComponent<Text>();
        okTxt.text = "OK";
        okTxt.font = selectedFont;
        okTxt.fontSize = 14;
        okTxt.alignment = TextAnchor.MiddleCenter;
        okTxt.color = Color.black;
        okTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        okTxt.verticalOverflow = VerticalWrapMode.Overflow;
        okTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 30f);
    }

    // --- ANIMATION & SHAKE ROUTINES ---
    private void TriggerShake(float duration, float magnitude)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        if (windowRect != null) shakeCoroutine = StartCoroutine(ShakeUIRoutine(duration, magnitude));
    }

    private IEnumerator ShakeUIRoutine(float duration, float magnitude)
    {
        if (windowRect == null) yield break;
        Vector2 originalPos = windowRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (windowRect == null) yield break;
            Vector2 offset = Random.insideUnitCircle * magnitude;
            windowRect.anchoredPosition = originalPos + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (windowRect != null) windowRect.anchoredPosition = originalPos;
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

        if (cg != null) cg.alpha = endAlpha;
    }

    private void DestroyUI()
    {
        if (shakeCoroutine != null) { StopCoroutine(shakeCoroutine); shakeCoroutine = null; }
        if (uiCanvasObj != null) { Destroy(uiCanvasObj); uiCanvasObj = null; windowRect = null; canvasGroup = null; }
    }
}