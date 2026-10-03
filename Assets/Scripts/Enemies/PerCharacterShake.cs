using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Text))]
public class PerCharacterShake : BaseMeshEffect
{
    [Tooltip("Intensity/magnitude of the individual character jitter.")]
    public float shakeMagnitude = 4f;

    [Tooltip("Speed/frequency of the character shaking.")]
    public float shakeSpeed = 25f;

    [Tooltip("Enable or disable continuous character shaking.")]
    public bool isShaking = true;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || !isShaking || shakeMagnitude <= 0f) return;

        List<UIVertex> verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        // Standard UI.Text generates 6 vertices per character (2 triangles)
        for (int i = 0; i < verts.Count; i += 6)
        {
            // Unique noise seed per character index
            float charSeed = i * 1.357f;

            float offsetX = (Mathf.PerlinNoise(charSeed, Time.time * shakeSpeed) - 0.5f) * 2f * shakeMagnitude;
            float offsetY = (Mathf.PerlinNoise(charSeed + 50f, Time.time * shakeSpeed) - 0.5f) * 2f * shakeMagnitude;
            Vector3 offset = new Vector3(offsetX, offsetY, 0f);

            for (int j = 0; j < 6; j++)
            {
                if (i + j < verts.Count)
                {
                    UIVertex vert = verts[i + j];
                    vert.position += offset;
                    verts[i + j] = vert;
                }
            }
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void Update()
    {
        if (isShaking && graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }
}