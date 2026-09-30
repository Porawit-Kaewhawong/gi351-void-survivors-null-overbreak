using UnityEngine;
using TMPro;

public class FloatingDamageText : MonoBehaviour
{
    [Header("Text Settings")]
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float fadeDuration = 0.5f;

    private float timer;
    private Color initialColor;

    private void Awake()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
        }

        if (textMesh != null)
        {
            initialColor = textMesh.color;
        }
    }

    /// <summary>
    /// Initializes text value, random slight offset, and camera alignment.
    /// </summary>
    public void Setup(float damageAmount)
    {
        if (textMesh != null)
        {
            textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
            textMesh.color = initialColor;
        }

        // Align rotation with main camera so text always faces player view
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }

        timer = fadeDuration;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        // Rise upward
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // Fade out alpha curve
        timer -= Time.deltaTime;
        if (textMesh != null)
        {
            float alpha = Mathf.Clamp01(timer / fadeDuration);
            textMesh.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
        }

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}