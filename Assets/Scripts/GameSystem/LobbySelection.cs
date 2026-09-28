using TMPro;
using UnityEngine;

public class LobbySelection : MonoBehaviour
{
    [Header("3D Pedestal Data")]
    public LobbyOption data;
    public LobbySelectionType selectionType;

    [Header("3D Visual Display Setup")]
    public SpriteRenderer optionIconRenderer;
    [Tooltip("Target max size in world units so all icon sprites render equally sized regardless of resolution.")]
    public float targetWorldSize = 1.5f;

    [Tooltip("The World Space Canvas or Transform parent holding your TMP text elements.")]
    public Transform textCanvasTransform;

    [Tooltip("If checked, rotates both the icon sprite and floating text to face the main camera in 3D space.")]
    public bool billboardToCamera = true;

    [Header("Floating Text Setup")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;

    private bool hasBeenSelected = false;
    private Transform mainCameraTransform;

    private void Awake()
    {
        CacheMainCamera();
    }

    public void SetupPedestal(LobbyOption optionData, LobbySelectionType type)
    {
        data = optionData;
        selectionType = type;
        hasBeenSelected = false; // Reset state when pedestal is initialized

        // Populate floating text
        if (titleText != null) titleText.text = data?.title ?? string.Empty;
        if (descriptionText != null) descriptionText.text = data?.description ?? string.Empty;

        // Assign and configure the option's 2D sprite
        if (optionIconRenderer != null && data != null && data.icon != null)
        {
            optionIconRenderer.sprite = data.icon;

            // Automatically scale sprite to match targetWorldSize in 3D units
            NormalizeSpriteSize(optionIconRenderer, targetWorldSize);

            // Sync _BaseMap for URP Lit shader so shadows and alpha clipping function properly
            if (optionIconRenderer.material != null && optionIconRenderer.material.HasProperty("_BaseMap"))
            {
                optionIconRenderer.material.SetTexture("_BaseMap", data.icon.texture);
            }
        }
    }

    private void LateUpdate()
    {
        if (!billboardToCamera) return;

        // Cache main camera if initialized late, changed, or swapped during scene load
        if (mainCameraTransform == null)
        {
            CacheMainCamera();
        }

        if (mainCameraTransform == null) return;

        // Billboard the 2D icon sprite to face camera
        if (optionIconRenderer != null)
        {
            optionIconRenderer.transform.rotation = Quaternion.LookRotation(optionIconRenderer.transform.position - mainCameraTransform.position);
        }

        // Billboard the text canvas / text container to face camera
        if (textCanvasTransform != null)
        {
            textCanvasTransform.rotation = Quaternion.LookRotation(textCanvasTransform.position - mainCameraTransform.position);
        }
    }

    private void OnMouseDown()
    {
        SelectThisOption();
    }

    public void SelectThisOption()
    {
        // Guard against rapid double-clicks
        if (hasBeenSelected) return;

        if (GameManager.Instance != null && data != null)
        {
            hasBeenSelected = true;
            GameManager.Instance.ConfirmSelection(data, selectionType);
        }
    }

    /// <summary>
    /// Uniformly scales the SpriteRenderer's Transform so its largest side fits targetWorldSize.
    /// </summary>
    private void NormalizeSpriteSize(SpriteRenderer renderer, float desiredSize)
    {
        if (renderer == null || renderer.sprite == null) return;

        Vector3 spriteBounds = renderer.sprite.bounds.size;
        float maxDimension = Mathf.Max(spriteBounds.x, spriteBounds.y);

        if (maxDimension > 0f)
        {
            float scaleFactor = desiredSize / maxDimension;
            renderer.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
        }
    }

    private void CacheMainCamera()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }
}