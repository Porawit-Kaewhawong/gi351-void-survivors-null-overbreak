using UnityEngine;

public class MapChunk : MonoBehaviour
{
    [Header("Spawn Validation")]
    [Tooltip("Check this if players should NEVER spawn on this chunk during Random Spawn (e.g., pits, hazards, narrow bridges).")]
    public bool isBlacklistedFromSpawn = false;

    [Tooltip("Optional custom spawn point transforms on this platform. If empty, the chunk center position will be used.")]
    public Transform[] customSpawnPoints;
}