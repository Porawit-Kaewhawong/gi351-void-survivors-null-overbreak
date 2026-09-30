using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [Tooltip("Flash silhouette color (usually pure White or bright Red).")]
    public Color flashColor = Color.white;

    [Tooltip("Flash duration in seconds.")]
    public float flashDuration = 0.08f;

    // Static shared flash material so memory isn't duplicated
    private static Material sharedFlashMaterial;

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Material> originalMaterials = new List<Material>();
    private readonly List<Color> originalColors = new List<Color>();
    private Coroutine flashCoroutine;

    private void Awake()
    {
        InitializeFlashMaterial();
        CacheRenderers();
    }

    private void OnEnable()
    {
        ResetFlash();
    }

    private void OnDisable()
    {
        ResetFlash();
    }

    private void InitializeFlashMaterial()
    {
        if (sharedFlashMaterial == null)
        {
            // GUI/Text Shader renders solid colored silhouettes using texture alpha
            Shader flashShader = Shader.Find("GUI/Text Shader");
            if (flashShader == null)
            {
                flashShader = Shader.Find("Sprites/Default");
            }
            sharedFlashMaterial = new Material(flashShader);
        }
    }

    /// <summary>
    /// Finds and caches all child SpriteRenderers and their default materials/colors.
    /// </summary>
    public void CacheRenderers()
    {
        renderers.Clear();
        originalMaterials.Clear();
        originalColors.Clear();

        SpriteRenderer[] found = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in found)
        {
            if (sr != null)
            {
                renderers.Add(sr);
                originalMaterials.Add(sr.sharedMaterial);
                originalColors.Add(sr.color);
            }
        }
    }

    public void Flash()
    {
        Flash(flashColor, flashDuration);
    }

    public void Flash(Color targetColor, float duration)
    {
        if (renderers.Count == 0) CacheRenderers();
        if (renderers.Count == 0) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(FlashRoutine(targetColor, duration));
    }

    private IEnumerator FlashRoutine(Color targetColor, float duration)
    {
        // Swap material to solid silhouette shader and apply target color
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sharedMaterial = sharedFlashMaterial;
                renderers[i].color = targetColor;
            }
        }

        // Use unscaled realtime so Hit-Stop time freezes don't delay the flash
        yield return new WaitForSecondsRealtime(duration);

        ResetFlash();
    }

    public void ResetFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        // Restore original materials and colors
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null && i < originalMaterials.Count)
            {
                renderers[i].sharedMaterial = originalMaterials[i];
                renderers[i].color = originalColors[i];
            }
        }
    }
}