using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    private Coroutine hitStopCoroutine;

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
    /// Freezes time briefly using unscaled time.
    /// </summary>
    /// <param name="duration">Duration in seconds (e.g. 0.04s for standard hits, 0.08s for heavy kills).</param>
    public void Trigger(float duration = 0.04f)
    {
        if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        hitStopCoroutine = null;
    }
}