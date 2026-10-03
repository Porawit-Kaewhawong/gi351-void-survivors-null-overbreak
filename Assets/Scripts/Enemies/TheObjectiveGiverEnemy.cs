using UnityEngine;
using System.Collections;

public class TheObjectiveGiverEnemy : EnemyBase
{
    public override bool IsTargetable => false;

    [Header("Skybox Objective Materials (Optional)")]
    [Tooltip("Skybox material displayed when 'DO NOT JUMP' objective triggers. If unassigned, tints existing skybox.")]
    [SerializeField] private Material doNotJumpSkybox;

    [Tooltip("Skybox material displayed when 'DO NOT MOVE' objective triggers. If unassigned, tints existing skybox.")]
    [SerializeField] private Material doNotMoveSkybox;

    [Header("Skybox Fallback Tint Colors")]
    [Tooltip("Tint color applied to current skybox if 'doNotJumpSkybox' is unassigned.")]
    [SerializeField] private Color doNotJumpSkyColor = new Color(1f, 0.85f, 0.2f); // Warning Yellow

    [Tooltip("Tint color applied to current skybox if 'doNotMoveSkybox' is unassigned.")]
    [SerializeField] private Color doNotMoveSkyColor = new Color(1f, 0.2f, 0.2f); // Critical Red

    [Tooltip("Speed at which the skybox color pulses during the active objective.")]
    [SerializeField] private float skyboxPulseSpeed = 8f;

    [Header("Objective Duration Settings")]
    [Tooltip("Duration in seconds that the objective lasts and monitors player actions.")]
    [SerializeField] private float objectiveDuration = 2.5f;

    [Header("Audio Settings")]
    [Tooltip("Audio clip played when an objective appears.")]
    [SerializeField] private AudioClip objectiveSound;

    [Tooltip("Audio clip played if the player breaks the rule and takes damage.")]
    [SerializeField] private AudioClip failSound;

    [Header("Spawn Timing Settings")]
    [Tooltip("Initial delay before the objective giver can first appear after spawn.")]
    [SerializeField] private float initialDelay = 12f;

    [Tooltip("Minimum time interval between random objective appearances.")]
    [SerializeField] private float minSpawnInterval = 18f;

    [Tooltip("Maximum time interval between random objective appearances.")]
    [SerializeField] private float maxSpawnInterval = 35f;

    private AudioSource audioSource;
    private bool isEventActive = false;

    private Material originalSkybox;
    private Material runtimeTempSkybox;

    protected override void Awake()
    {
        base.Awake();

        // Setup AudioSource component for sound effects
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        DisableWorldInteraction();
    }

    private void Start()
    {
        StartCoroutine(RandomObjectiveRoutine());
    }

    private void OnDisable()
    {
        RestoreSkybox();
    }

    protected override void Update()
    {
        // Overridden empty to prevent base enemy movement logic
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
    private IEnumerator RandomObjectiveRoutine()
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
                yield return StartCoroutine(TriggerObjectiveEventRoutine());
            }
        }
    }

    // --- OBJECTIVE EVENT SEQUENCE ---
    private IEnumerator TriggerObjectiveEventRoutine()
    {
        isEventActive = true;

        // Pick objective type: 0 = Do Not Jump, 1 = Do Not Move
        int objectiveType = Random.Range(0, 2);
        bool isJumpObjective = (objectiveType == 0);

        Debug.Log($"[TheObjectiveGiver] Objective event started! Skybox active. Mode: {(isJumpObjective ? "DO NOT JUMP" : "DO NOT MOVE")}");

        // Save active skybox and apply objective skybox/tint
        ApplyObjectiveSkybox(isJumpObjective);

        // Play warning sound effect
        if (objectiveSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(objectiveSound);
        }

        float elapsed = 0f;
        bool ruleBroken = false;

        // Monitor player inputs while pulsing skybox
        while (elapsed < objectiveDuration)
        {
            // Check if player violates the rule
            if (isJumpObjective)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump"))
                {
                    ruleBroken = true;
                }
            }
            else // Rule: Do Not Move
            {
                if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                {
                    ruleBroken = true;
                }
            }

            // Pulse skybox tint/exposure if using temp runtime material
            PulseSkyboxEffect(isJumpObjective, elapsed);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Apply penalty if rule was broken
        if (ruleBroken)
        {
            float penaltyDamage = attackDamage > 0f ? attackDamage : 5f;
            if (playerController != null)
            {
                playerController.TakeDamage(penaltyDamage);
            }

            if (failSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failSound);
            }

            Debug.Log($"[TheObjectiveGiver] Rule broken! Applied {penaltyDamage} damage penalty.");
        }
        else
        {
            Debug.Log("[TheObjectiveGiver] Objective successfully followed!");
        }

        // Smoothly transition back to normal skybox
        yield return StartCoroutine(TransitionBackToNormalSkyboxRoutine(0.5f));

        isEventActive = false;
    }

    // --- SKYBOX MANIPULATION LOGIC ---
    private void ApplyObjectiveSkybox(bool isJumpObjective)
    {
        // Backup the original scene skybox
        if (originalSkybox == null)
        {
            originalSkybox = RenderSettings.skybox;
        }

        Material targetMat = isJumpObjective ? doNotJumpSkybox : doNotMoveSkybox;

        if (targetMat != null)
        {
            RenderSettings.skybox = targetMat;
        }
        else if (originalSkybox != null)
        {
            // Fallback: Clone active skybox and adjust tint dynamically
            if (runtimeTempSkybox != null) Destroy(runtimeTempSkybox);
            runtimeTempSkybox = new Material(originalSkybox);

            Color targetColor = isJumpObjective ? doNotJumpSkyColor : doNotMoveSkyColor;
            SetMaterialColor(runtimeTempSkybox, targetColor);

            RenderSettings.skybox = runtimeTempSkybox;
        }

        DynamicGI.UpdateEnvironment();
    }

    private void PulseSkyboxEffect(bool isJumpObjective, float time)
    {
        if (runtimeTempSkybox == null) return;

        Color baseColor = isJumpObjective ? doNotJumpSkyColor : doNotMoveSkyColor;
        float pulse = (Mathf.Sin(time * skyboxPulseSpeed) + 1f) * 0.5f; // 0 to 1 wave
        Color pulsedColor = Color.Lerp(baseColor * 0.6f, baseColor * 1.4f, pulse);

        SetMaterialColor(runtimeTempSkybox, pulsedColor);
    }

    private void SetMaterialColor(Material mat, Color col)
    {
        if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", col);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
        else if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", col);
    }

    private IEnumerator TransitionBackToNormalSkyboxRoutine(float duration)
    {
        float elapsed = 0f;
        Color startColor = runtimeTempSkybox != null && runtimeTempSkybox.HasProperty("_Tint") ? runtimeTempSkybox.GetColor("_Tint") : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (runtimeTempSkybox != null)
            {
                Color fadedColor = Color.Lerp(startColor, Color.white, t);
                SetMaterialColor(runtimeTempSkybox, fadedColor);
            }

            yield return null;
        }

        RestoreSkybox();
    }

    private void RestoreSkybox()
    {
        if (originalSkybox != null)
        {
            RenderSettings.skybox = originalSkybox;
            DynamicGI.UpdateEnvironment();
        }

        if (runtimeTempSkybox != null)
        {
            Destroy(runtimeTempSkybox);
            runtimeTempSkybox = null;
        }
    }
}