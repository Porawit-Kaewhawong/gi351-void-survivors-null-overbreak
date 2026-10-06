using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SpriteDirectionData
{
    public Sprite idleSprite;
    public List<Sprite> walkFrames = new List<Sprite>();
    public List<Sprite> jumpFrames = new List<Sprite>();
}

public class PlayerController : MonoBehaviour
{
    [Header("Base Movement Settings")]
    public float baseMoveSpeed = 5f;

    [Header("Base Jump Settings")]
    public float baseJumpHeight = 2f;
    public float gravity = -20f;

    [Header("Fall Death Settings")]
    [Tooltip("If the player drops below this Y height in the world, Die() is automatically triggered.")]
    public float killYThreshold = -20f;

    [Header("Base Pickup Radius Settings")]
    public float basePickupRadius = 2f;

    [Header("Health & Shield Settings")]
    public float baseMaxHealth = 100f;
    [Tooltip("Base shield value. Shield buff options add flat numbers directly to this amount.")]
    public float baseMaxShield = 0f;

    [Header("Invincibility / i-Frames Settings")]
    [Tooltip("Duration of immunity (in seconds) after taking a hit.")]
    public float iFrameDuration = 1.0f;
    [Tooltip("Flicker/blink sprite transparency while invincible.")]
    public bool flashSpriteDuringIFrames = true;
    [Tooltip("Speed of the sprite blinking animation during immunity.")]
    public float iFrameBlinkInterval = 0.08f;

    [Header("Shield Recharge Settings")]
    [Tooltip("Minimum time interval in seconds between automatic shield recharges.")]
    public float minShieldRechargeInterval = 10f;
    [Tooltip("Maximum time interval in seconds between automatic shield recharges.")]
    public float maxShieldRechargeInterval = 15f;
    [Tooltip("Amount of shield restored per trigger. Set to 0 to fully restore current max shield.")]
    public float shieldRechargeAmount = 0f;

    [Header("Transparent Sprite Shield Settings")]
    [Tooltip("Optional custom 2D circle sprite. If left unassigned, a smooth glowing circle sprite is procedurally generated.")]
    public Sprite customShieldSprite;
    [Tooltip("Color tint of the transparent shield bubble.")]
    public Color shieldColor = new Color(0f, 0.7f, 1f, 0.5f);
    [Tooltip("Local position offset relative to player pivot to center the shield over the full character visual.")]
    public Vector3 shieldOffset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("Overall scale size of the shield circle (Increase if sprite doesn't fully encapsulate head/feet).")]
    public float shieldSize = 3.2f;
    [Tooltip("Speed of the breathing/pulsing visual effect.")]
    public float pulseSpeed = 3f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public float groundCheckOffset = 0.05f;
    public LayerMask groundLayer;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("2D Sprite & 8-Direction Animations")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Frame rate for walking and jumping animation cycles.")]
    public float animationFPS = 8f;
    [Tooltip("If checked, rotates the SpriteRenderer to face the camera in 3D space.")]
    public bool billboardToCamera = true;

    [Space(10)]
    public SpriteDirectionData north;
    public SpriteDirectionData northEast;
    public SpriteDirectionData east;
    public SpriteDirectionData southEast;
    public SpriteDirectionData south;
    public SpriteDirectionData southWest;
    public SpriteDirectionData west;
    public SpriteDirectionData northWest;

    [Header("Collectible Arrow Tracker")]
    [Tooltip("Transform of the 3D or 2D Arrow object that points towards the item.")]
    public Transform arrowTransform;
    [Tooltip("Tag assigned to all collectible objects in the scene.")]
    public string collectibleTag = "Collectible";
    [Tooltip("How often (in seconds) to re-scan for the closest collectible.")]
    public float trackerScanInterval = 0.2f;
    [Tooltip("Hide the arrow when no collectibles remain in the scene.")]
    public bool hideArrowWhenNoneFound = true;

    [Header("UI Visual Feedback")]
    [Tooltip("UI Image covering the screen used for hit color flashes.")]
    public Image damageOverlayImage;
    public Color shieldHitColor = new Color(0f, 0.5f, 1f, 0.4f);
    public Color healthHitColor = new Color(1f, 0f, 0f, 0.4f);
    public float flashDuration = 0.25f;

    [Header("Audio & SFX Slots")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public AudioClip shieldHitSound;
    public AudioClip shieldBreakSound;
    public AudioClip healthHitSound;
    public AudioClip playerDeathSound;

    [Header("Heartbeat Settings")]
    public AudioClip heartbeatSound;
    [Range(0f, 1f)] public float heartbeatVolume = 0.8f;
    public float maxHeartbeatInterval = 1.2f;
    public float minHeartbeatInterval = 0.25f;

    // Current Dynamic State
    public float CurrentHealth { get; private set; }
    public float CurrentShield { get; private set; }

    // Invincibility State
    private float iFrameTimer = 0f;
    public bool IsInvincible => iFrameTimer > 0f;

    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private float coyoteTime = 0.15f;
    private float coyoteTimer;
    private int extraJumpsRemaining;
    private bool isDead = false;

    // Transparent Sprite Shield Handles
    private GameObject shieldSpriteObj;
    private SpriteRenderer shieldSpriteRenderer;

    // Shield Charging Internal Handle
    private float shieldRechargeTimer;

    // Tracker Internal Handles
    private Transform closestCollectible;
    private float trackerScanTimer;

    // Audio & Visual internal handles
    private AudioSource heartbeatAudioSource;
    private float heartbeatTimer;
    private Coroutine flashCoroutine;

    // Animation internal handles
    private int lastDirectionIndex = 4;
    private float animTimer = 0f;
    private int currentFrameIndex = 0;
    private SpriteFlash spriteFlasher;

    // --- STAT CALCULATIONS ---

    public float CurrentMoveSpeed
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MoveSpeed) : 0f;
            return baseMoveSpeed * (1f + (bonus / 100f));
        }
    }

    public float CurrentJumpHeight
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.JumpHeight) : 0f;
            return baseJumpHeight * (1f + (bonus / 100f));
        }
    }

    public float CurrentPickupRadius
    {
        get
        {
            float bonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.PickupRadius) : 0f;
            return basePickupRadius * (1f + (bonus / 100f));
        }
    }

    public int MaxExtraJumps
    {
        get
        {
            return GameManager.Instance != null
                ? Mathf.FloorToInt(GameManager.Instance.GetTotalBuffValue(StatType.ExtraJumps))
                : 0;
        }
    }

    public float CurrentMaxHealth => baseMaxHealth;

    public float CurrentMaxShield
    {
        get
        {
            float flatShieldBonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.MaxShield) : 0f;
            return baseMaxShield + flatShieldBonus;
        }
    }

    private void Awake()
    {
        ApplyMainMenuUpgrades();
        spriteFlasher = GetComponentInChildren<SpriteFlash>();

        heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.loop = false;
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            spriteRenderer.receiveShadows = true;

            if (spriteRenderer.sprite != null && spriteRenderer.material != null)
            {
                spriteRenderer.material.SetTexture("_BaseMap", spriteRenderer.sprite.texture);
            }
        }

        GenerateSpriteShield();
        ResetHealthAndShield();

        if (damageOverlayImage != null)
        {
            damageOverlayImage.color = Color.clear;
            damageOverlayImage.raycastTarget = false;
        }
    }

    private void ApplyMainMenuUpgrades()
    {
        int hpLvl = PlayerPrefs.GetInt("Upgrade_MaxHP_Level", 0);
        int speedLvl = PlayerPrefs.GetInt("Upgrade_MoveSpeed_Level", 0);
        int radiusLvl = PlayerPrefs.GetInt("Upgrade_PickupRadius_Level", 0);

        baseMaxHealth *= (1f + (hpLvl * 0.10f));
        baseMoveSpeed *= (1f + (speedLvl * 0.05f));
        basePickupRadius *= (1f + (radiusLvl * 0.15f));
    }

    private void Update()
    {
        HandleIFrames();

        if (isDead)
        {
            UpdateShieldVisual();
            return;
        }

        CheckFallDeath();
        CheckGround();

        Vector3 moveInput = HandleMovement();

        HandleJump();
        ApplyGravity();
        HandleShieldRecharge();
        UpdateShieldVisual();
        HandleSpriteAnimation(moveInput);
        HandleHeartbeatSound();
        HandleCollectibleTracker();
    }

    private void LateUpdate()
    {
        if (billboardToCamera && spriteRenderer != null && cameraTransform != null)
        {
            spriteRenderer.transform.rotation = Quaternion.LookRotation(spriteRenderer.transform.position - cameraTransform.position);
        }

        if (billboardToCamera && shieldSpriteObj != null && cameraTransform != null)
        {
            shieldSpriteObj.transform.rotation = Quaternion.LookRotation(shieldSpriteObj.transform.position - cameraTransform.position);
        }
    }

    // --- IMMUNITY FRAME HANDLING ---

    private void HandleIFrames()
    {
        if (iFrameTimer > 0f)
        {
            iFrameTimer -= Time.deltaTime;

            // Visual flicker effect during immunity
            if (flashSpriteDuringIFrames && spriteRenderer != null)
            {
                float alpha = (Mathf.FloorToInt(Time.time / iFrameBlinkInterval) % 2 == 0) ? 0.3f : 1f;
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }

            if (iFrameTimer <= 0f)
            {
                RestoreSpriteAlpha();
            }
        }
    }

    private void RestoreSpriteAlpha()
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }
    }

    // --- TRANSPARENT SPRITE CIRCLE SHIELD GENERATOR & CONTROLLER ---

    private void GenerateSpriteShield()
    {
        if (shieldSpriteObj != null) return;

        shieldSpriteObj = new GameObject("_ShieldSpriteCircle");
        shieldSpriteObj.transform.SetParent(transform, false);
        shieldSpriteObj.transform.localPosition = shieldOffset;

        shieldSpriteRenderer = shieldSpriteObj.AddComponent<SpriteRenderer>();

        if (customShieldSprite != null)
        {
            shieldSpriteRenderer.sprite = customShieldSprite;
        }
        else
        {
            shieldSpriteRenderer.sprite = CreateProceduralCircleSprite(128);
        }

        shieldSpriteRenderer.color = shieldColor;
        if (spriteRenderer != null)
        {
            shieldSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shieldSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        shieldSpriteObj.SetActive(false);
    }

    private Sprite CreateProceduralCircleSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[resolution * resolution];
        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float radius = resolution / 2f - 2f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDist = dist / radius;

                if (normalizedDist <= 1f)
                {
                    float edgeGlow = Mathf.Sin(normalizedDist * Mathf.PI);
                    float alpha = Mathf.Pow(edgeGlow, 1.8f);

                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * resolution + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
    }

    private void UpdateShieldVisual()
    {
        if (shieldSpriteObj == null) return;

        bool hasShield = !isDead && CurrentShield > 0f && CurrentMaxShield > 0f;

        if (shieldSpriteObj.activeSelf != hasShield)
        {
            shieldSpriteObj.SetActive(hasShield);
        }

        if (hasShield)
        {
            shieldSpriteObj.transform.localPosition = shieldOffset;

            float shieldRatio = Mathf.Clamp01(CurrentShield / CurrentMaxShield);

            float pulseOffset = Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
            float currentScale = (shieldSize + pulseOffset) * Mathf.Lerp(0.85f, 1f, shieldRatio);
            shieldSpriteObj.transform.localScale = Vector3.one * currentScale;

            if (shieldSpriteRenderer != null)
            {
                Color updatedColor = shieldColor;
                updatedColor.a = Mathf.Lerp(0.12f, shieldColor.a, shieldRatio);
                shieldSpriteRenderer.color = updatedColor;
            }
        }
    }

    // --- SHIELD RECHARGE AUTOMATION ---

    private void ResetShieldRechargeTimer()
    {
        float baseInterval = Random.Range(minShieldRechargeInterval, maxShieldRechargeInterval);
        float chargeBonus = GameManager.Instance != null ? GameManager.Instance.GetTotalBuffValue(StatType.ShieldCharge) : 0f;

        shieldRechargeTimer = baseInterval / (1f + (chargeBonus / 100f));
    }

    private void HandleShieldRecharge()
    {
        if (isDead || CurrentMaxShield <= 0f || CurrentShield >= CurrentMaxShield) return;

        shieldRechargeTimer -= Time.deltaTime;
        if (shieldRechargeTimer <= 0f)
        {
            RechargeShield();
            ResetShieldRechargeTimer();
        }
    }

    public void RechargeShield(float amount = -1f)
    {
        if (isDead || CurrentMaxShield <= 0f) return;

        float restoreAmount = (amount > 0f) ? amount : (shieldRechargeAmount > 0f ? shieldRechargeAmount : CurrentMaxShield);
        CurrentShield = Mathf.Min(CurrentShield + restoreAmount, CurrentMaxShield);

        UpdateShieldVisual();
        Debug.Log($"[Player] Shield Recharged! Current Shield: {CurrentShield}/{CurrentMaxShield}");
    }

    // --- COLLECTIBLE ARROW TRACKER ---

    private void HandleCollectibleTracker()
    {
        if (arrowTransform == null) return;

        trackerScanTimer -= Time.deltaTime;
        if (trackerScanTimer <= 0f)
        {
            trackerScanTimer = trackerScanInterval;
            FindClosestCollectible();
        }

        if (closestCollectible != null)
        {
            if (!arrowTransform.gameObject.activeSelf)
            {
                arrowTransform.gameObject.SetActive(true);
            }

            Vector3 targetDirection = closestCollectible.position - transform.position;
            targetDirection.y = 0f;

            if (targetDirection.sqrMagnitude > 0.001f)
            {
                arrowTransform.rotation = Quaternion.LookRotation(targetDirection);
            }
        }
        else
        {
            if (hideArrowWhenNoneFound && arrowTransform.gameObject.activeSelf)
            {
                arrowTransform.gameObject.SetActive(false);
            }
        }
    }

    private void FindClosestCollectible()
    {
        GameObject[] collectibles = GameObject.FindGameObjectsWithTag(collectibleTag);
        float minDistanceSqr = float.MaxValue;
        Transform nearest = null;

        Vector3 currentPosition = transform.position;

        foreach (GameObject col in collectibles)
        {
            if (col == null || !col.activeInHierarchy) continue;

            float distSqr = (col.transform.position - currentPosition).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = col.transform;
            }
        }

        closestCollectible = nearest;
    }

    // --- FALL DEATH CHECK ---

    private void CheckFallDeath()
    {
        if (transform.position.y < killYThreshold)
        {
            Die();
        }
    }

    // --- HEALTH & DAMAGE ---

    public void ResetHealthAndShield()
    {
        isDead = false;
        CurrentHealth = CurrentMaxHealth;
        CurrentShield = CurrentMaxShield;
        heartbeatTimer = 0f;
        iFrameTimer = 0f;
        RestoreSpriteAlpha();
        ResetShieldRechargeTimer();
        UpdateShieldVisual();

        if (damageOverlayImage != null)
        {
            damageOverlayImage.color = Color.clear;
        }
    }

    public void TakeDamage(float damage)
    {
        // Block all incoming damage while invincible
        if (isDead || damage <= 0f || IsInvincible) return;

        // Activate i-frames timer
        iFrameTimer = iFrameDuration;

        if (spriteFlasher != null)
        {
            spriteFlasher.Flash(new Color(2f, 0.2f, 0.2f, 1f), 0.15f);
        }

        float previousShield = CurrentShield;
        bool tookShieldDamage = false;
        bool tookHealthDamage = false;

        if (CurrentShield > 0f)
        {
            tookShieldDamage = true;
            float shieldDamage = Mathf.Min(CurrentShield, damage);
            CurrentShield -= shieldDamage;
            damage -= shieldDamage;

            if (previousShield > 0f && CurrentShield <= 0f)
            {
                PlaySFX(shieldBreakSound);
            }
            else
            {
                PlaySFX(shieldHitSound);
            }
        }

        if (damage > 0f)
        {
            tookHealthDamage = true;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0f, CurrentHealth);
            PlaySFX(healthHitSound);
        }

        if (tookHealthDamage)
        {
            TriggerDamageFlash(healthHitColor);
        }
        else if (tookShieldDamage)
        {
            TriggerDamageFlash(shieldHitColor);
        }

        UpdateShieldVisual();

        if (HitStop.Instance != null) HitStop.Instance.Trigger(0.06f);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        iFrameTimer = 0f;
        RestoreSpriteAlpha();

        PlaySFX(playerDeathSound);
        UpdateShieldVisual();

        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying)
        {
            heartbeatAudioSource.Stop();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDied();
        }
    }

    // --- HEARTBEAT SYSTEM ---

    private void HandleHeartbeatSound()
    {
        if (heartbeatSound == null || isDead) return;

        if (CurrentHealth < CurrentMaxHealth && CurrentHealth > 0f)
        {
            heartbeatTimer -= Time.deltaTime;

            if (heartbeatTimer <= 0f)
            {
                float healthRatio = Mathf.Clamp01(CurrentHealth / CurrentMaxHealth);
                float currentInterval = Mathf.Lerp(minHeartbeatInterval, maxHeartbeatInterval, healthRatio);

                heartbeatAudioSource.PlayOneShot(heartbeatSound, heartbeatVolume);
                heartbeatTimer = currentInterval;
            }
        }
        else
        {
            heartbeatTimer = 0f;
        }
    }

    // --- VISUAL & AUDIO FEEDBACK HELPERS ---

    private void TriggerDamageFlash(Color flashColor)
    {
        if (damageOverlayImage == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(DamageFlashRoutine(flashColor));
    }

    private IEnumerator DamageFlashRoutine(Color targetColor)
    {
        float elapsed = 0f;
        damageOverlayImage.color = targetColor;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            damageOverlayImage.color = Color.Lerp(targetColor, Color.clear, t);
            yield return null;
        }

        damageOverlayImage.color = Color.clear;
        flashCoroutine = null;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, cameraTransform != null ? cameraTransform.position : transform.position, sfxVolume);
    }

    // --- MOVEMENT & PHYSICS ---

    private void CheckGround()
    {
        float bottom = characterController.bounds.min.y;

        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        isGrounded = Physics.CheckSphere(
            checkPosition,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            extraJumpsRemaining = MaxExtraJumps;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    private Vector3 HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 rawInput = new Vector3(horizontal, 0f, vertical);

        Vector3 cameraForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 cameraRight = cameraTransform != null ? cameraTransform.right : transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * vertical + cameraRight * horizontal;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        characterController.Move(moveDirection * CurrentMoveSpeed * Time.deltaTime);

        return rawInput;
    }

    private void HandleJump()
    {
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (coyoteTimer > 0f)
            {
                velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
                coyoteTimer = 0f;
            }
            else if (extraJumpsRemaining > 0)
            {
                velocity.y = Mathf.Sqrt(CurrentJumpHeight * -2f * gravity);
                extraJumpsRemaining--;
            }
        }
    }

    private void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // --- 8-WAY SPRITE ANIMATION CONTROLLER ---

    private void HandleSpriteAnimation(Vector3 inputVector)
    {
        if (spriteRenderer == null) return;

        bool isMoving = inputVector.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            float angle = Mathf.Atan2(inputVector.x, inputVector.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            lastDirectionIndex = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;
        }

        SpriteDirectionData dirData = GetDirectionData(lastDirectionIndex);
        if (dirData == null) return;

        if (!isGrounded && dirData.jumpFrames != null && dirData.jumpFrames.Count > 0)
        {
            animTimer += Time.deltaTime;
            float frameInterval = 1f / Mathf.Max(0.1f, animationFPS);

            if (animTimer >= frameInterval)
            {
                animTimer -= frameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % dirData.jumpFrames.Count;
            }

            currentFrameIndex %= dirData.jumpFrames.Count;
            SetSprite(dirData.jumpFrames[currentFrameIndex]);
        }
        else if (isGrounded && isMoving && dirData.walkFrames != null && dirData.walkFrames.Count > 0)
        {
            animTimer += Time.deltaTime;
            float frameInterval = 1f / Mathf.Max(0.1f, animationFPS);

            if (animTimer >= frameInterval)
            {
                animTimer -= frameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % dirData.walkFrames.Count;
            }

            currentFrameIndex %= dirData.walkFrames.Count;
            SetSprite(dirData.walkFrames[currentFrameIndex]);
        }
        else
        {
            animTimer = 0f;
            currentFrameIndex = 0;
            if (dirData.idleSprite != null)
            {
                SetSprite(dirData.idleSprite);
            }
        }
    }

    private void SetSprite(Sprite newSprite)
    {
        if (newSprite == null) return;

        spriteRenderer.sprite = newSprite;

        if (spriteRenderer.material != null && newSprite.texture != null)
        {
            spriteRenderer.material.SetTexture("_BaseMap", newSprite.texture);
        }
    }

    private SpriteDirectionData GetDirectionData(int index)
    {
        switch (index)
        {
            case 0: return north;
            case 1: return northEast;
            case 2: return east;
            case 3: return southEast;
            case 4: return south;
            case 5: return southWest;
            case 6: return west;
            case 7: return northWest;
            default: return south;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (characterController == null) return;

        float bottom = characterController.bounds.min.y;
        Vector3 checkPosition = new Vector3(
            characterController.bounds.center.x,
            bottom + groundCheckOffset,
            characterController.bounds.center.z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentPickupRadius);
    }

    public void Bounce(float force)
    {
        velocity.y = force;
        coyoteTimer = 0f;
    }
}