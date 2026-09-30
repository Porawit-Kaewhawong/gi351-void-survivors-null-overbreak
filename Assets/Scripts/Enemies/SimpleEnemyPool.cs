using System.Collections.Generic;
using UnityEngine;

public class SimpleEnemyPool : MonoBehaviour
{
    public static SimpleEnemyPool Instance { get; private set; }

    // Maps prefab references to active/inactive pooled instances
    private readonly Dictionary<GameObject, List<GameObject>> poolDictionary = new Dictionary<GameObject, List<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Fetches an inactive enemy instance matching the prefab, or instantiates a new one if all instances are active.
    /// </summary>
    public GameObject GetEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary[prefab] = new List<GameObject>();
        }

        List<GameObject> pool = poolDictionary[prefab];

        // Search for an available inactive instance
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].activeInHierarchy)
            {
                pool[i].transform.SetPositionAndRotation(position, rotation);
                pool[i].SetActive(true);
                return pool[i];
            }
        }

        // Instantiate new instance if no inactive object exists in pool
        GameObject newEnemy = Instantiate(prefab, position, rotation, transform);
        pool.Add(newEnemy);
        return newEnemy;
    }

    /// <summary>
    /// Returns an active enemy back to the pool by disabling it and unregistering it from GameManager.
    /// </summary>
    public void ReturnEnemy(GameObject enemyInstance)
    {
        if (enemyInstance == null) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterEnemy(enemyInstance);
        }

        enemyInstance.SetActive(false);
    }
}