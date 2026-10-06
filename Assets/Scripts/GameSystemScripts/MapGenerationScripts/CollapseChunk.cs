using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EscapeCollapseChunk : MonoBehaviour
{
    [Header("Escape Collapse Settings")]
    [Tooltip("Time in seconds player can stand on the platform after stepping on it before it crumbles.")]
    public float delayBeforeFall = 1.2f;

    [Tooltip("Intensity of the platform shake warning.")]
    public float shakeIntensity = 0.12f;

    [Header("Disappear Animation Settings")]
    [Tooltip("Duration of the flash and sink/shrink animation.")]
    public float fadeDuration = 0.35f;

    [Tooltip("Flash color right before disappearing.")]
    public Color flashColor = Color.white;

    [Tooltip("How far down the platform sinks during the crumble animation.")]
    public float sinkDistance = 1.5f;

    [Header("Detection Settings")]
    [Tooltip("Height of the detection zone above the platform floor surface.")]
    public float detectionZoneHeight = 1.5f;

    private Vector3 initialPosition;
    private Vector3 initialScale;
    private bool isCollapsing = false;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Bounds combinedBounds;
    private bool boundsCalculated = false;

    // Cache original material colors for smooth lerping
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        initialScale = transform.localScale;

        CacheOriginalMaterialColors();
        CalculateCombinedBounds();
    }

    private void CacheOriginalMaterialColors()
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials; // Instantiates unique material instances safely
            Color[] colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                    colors[i] = mats[i].GetColor("_Color");
                else if (mats[i].HasProperty("_BaseColor"))
                    colors[i] = mats[i].GetColor("_BaseColor");
                else
                    colors[i] = Color.white;
            }
            originalColors[r] = colors;
        }
    }

    private void CalculateCombinedBounds()
    {
        if (colliders == null || colliders.Length == 0) return;

        combinedBounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            combinedBounds.Encapsulate(colliders[i].bounds);
        }
        boundsCalculated = true;
    }

    private void Update()
    {
        if (isCollapsing || !boundsCalculated) return;

        // Check if exit is unlocked in GameManager
        if (GameManager.Instance != null && GameManager.Instance.ExitUnlocked)
        {
            CheckPlayerStandingOnPlatform();
        }
    }

    private void CheckPlayerStandingOnPlatform()
    {
        Vector3 center = combinedBounds.center + Vector3.up * (combinedBounds.extents.y + (detectionZoneHeight * 0.5f));
        Vector3 halfExtents = new Vector3(combinedBounds.extents.x, detectionZoneHeight * 0.5f, combinedBounds.extents.z);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                StartCoroutine(CrumbleSequence());
                break;
            }
        }
    }

    private IEnumerator CrumbleSequence()
    {
        isCollapsing = true;
        initialPosition = transform.position;

        // 1. Shake Warning Phase
        float elapsed = 0f;
        while (elapsed < delayBeforeFall)
        {
            Vector3 shake = Random.insideUnitSphere * shakeIntensity;
            shake.y = 0f; // Keep shake horizontal
            transform.position = initialPosition + shake;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = initialPosition;

        // 2. Flash White, Sink into Void & Shrink Animation Phase
        elapsed = 0f;
        Vector3 targetSinkPos = initialPosition + (Vector3.down * sinkDistance);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            // Flash material color to white
            ApplyColorFlash(t);

            // Sink and scale down smoothly
            transform.position = Vector3.Lerp(initialPosition, targetSinkPos, t);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

            yield return null;
        }

        // 3. Permanently Disable Platform (No Respawning)
        SetPlatformActive(false);
    }

    private void ApplyColorFlash(float progress)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            Color[] startColors = originalColors.ContainsKey(r) ? originalColors[r] : null;

            for (int i = 0; i < mats.Length; i++)
            {
                Color baseCol = (startColors != null && i < startColors.Length) ? startColors[i] : Color.white;
                Color targetCol = Color.Lerp(baseCol, flashColor, progress);

                if (mats[i].HasProperty("_Color"))
                    mats[i].SetColor("_Color", targetCol);
                else if (mats[i].HasProperty("_BaseColor"))
                    mats[i].SetColor("_BaseColor", targetCol);
            }
        }
    }

    private void SetPlatformActive(bool active)
    {
        foreach (var r in renderers) if (r != null) r.enabled = active;
        foreach (var c in colliders) if (c != null) c.enabled = active;
    }

    private void OnDrawGizmosSelected()
    {
        if (boundsCalculated)
        {
            Gizmos.color = Color.red;
            Vector3 center = combinedBounds.center + Vector3.up * (combinedBounds.extents.y + (detectionZoneHeight * 0.5f));
            Vector3 size = new Vector3(combinedBounds.size.x, detectionZoneHeight, combinedBounds.size.z);
            Gizmos.DrawWireCube(center, size);
        }
    }
}