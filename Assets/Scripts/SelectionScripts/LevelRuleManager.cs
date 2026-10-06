using UnityEngine;

public class LevelRuleManager : MonoBehaviour
{
    public static LevelRuleManager Instance { get; private set; }

    [Header("Default Physics")]
    public Vector3 baseGravity = new Vector3(0f, -9.81f, 0f);

    private bool round2TriggeredThisLevel = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ApplyActiveLevelRules()
    {
        if (GameManager.Instance == null) return;

        ApplyGravityRule();
        ApplyRandomSpawnRule();

        // Note: TiltedPlatforms and JumpPads are intentionally handled 
        // exclusively in MapGenerator.cs to prevent duplicate spawning 
        // and conflicting rotations during map generation.
    }

    // --- TARGET: LowerGravity ---
    private void ApplyGravityRule()
    {
        if (GameManager.Instance.HasLevelRule(LevelRuleType.LowerGravity))
        {
            float reducePercent = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.LowerGravity);
            float multiplier = Mathf.Clamp01(1f - (reducePercent / 100f));
            Physics.gravity = baseGravity * multiplier;
            Debug.Log($"[LevelRuleManager] Applied Lower Gravity: {reducePercent}% reduction.");
        }
        else
        {
            Physics.gravity = baseGravity;
        }
    }

    // --- TARGET: RandomSpawn ---
    private void ApplyRandomSpawnRule()
    {
        if (!GameManager.Instance.HasLevelRule(LevelRuleType.RandomSpawn)) return;

        Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
        if (playerTransform == null) return;

        if (TryGetRandomPlatformPosition(out Vector3 randomSpawnPos))
        {
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = randomSpawnPos + Vector3.up * 1f;

            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            Debug.Log($"[LevelRuleManager] Applied Random Spawn: Teleported player to {randomSpawnPos}.");
        }
    }

    // --- TARGET: Round2 ---
    public bool CheckShouldRepeatLevelForRound2()
    {
        if (GameManager.Instance != null && GameManager.Instance.HasLevelRule(LevelRuleType.Round2) && !round2TriggeredThisLevel)
        {
            float chancePercent = GameManager.Instance.GetTotalLevelRuleValue(LevelRuleType.Round2);
            float chance = chancePercent > 0 ? chancePercent / 100f : 1f;

            if (Random.value <= chance)
            {
                round2TriggeredThisLevel = true;
                Debug.Log("[LevelRuleManager] Round 2 Triggered! Replaying level.");
                return true;
            }
        }

        round2TriggeredThisLevel = false;
        return false;
    }

    private bool TryGetRandomPlatformPosition(out Vector3 position)
    {
        position = Vector3.zero;
        for (int i = 0; i < 20; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * 25f;
            Vector3 rayOrigin = new Vector3(randomCircle.x, 20f, randomCircle.y);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger)
                {
                    position = hit.point;
                    return true;
                }
            }
        }
        return false;
    }
}