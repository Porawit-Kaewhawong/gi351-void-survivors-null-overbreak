using UnityEngine;
using System;

public class WatcherFogItem : MonoBehaviour
{
    [Header("Particle Effect Settings")]
    [Tooltip("Optional custom Fog Particle Prefab. If left empty, a dynamic fog effect is generated procedurally.")]
    [SerializeField] private GameObject customParticlePrefab;

    [Tooltip("Color of the fog mist particles.")]
    [SerializeField] private Color fogColor = new Color(0.8f, 0.85f, 0.9f, 0.25f);

    [Tooltip("Density / rate of fog particles spawned per second.")]
    [SerializeField] private float particleEmissionRate = 20f;

    [Tooltip("Radius around the item where fog particles drift.")]
    [SerializeField] private float fogRadius = 3f;

    private Action onCollectedCallback;
    private ParticleSystem proceduralParticleSystem;

    public void Initialize(Action onCollected)
    {
        onCollectedCallback = onCollected;

        if (customParticlePrefab != null)
        {
            Instantiate(customParticlePrefab, transform.position, Quaternion.identity, transform);
        }
        else if (GetComponentInChildren<ParticleSystem>() == null)
        {
            CreateDynamicFogEffect();
        }
    }

    private void CreateDynamicFogEffect()
    {
        GameObject fogObj = new GameObject("FogMistEffect");
        fogObj.transform.SetParent(transform, false);

        proceduralParticleSystem = fogObj.AddComponent<ParticleSystem>();

        // Ensure system is explicitly stopped before modifying main module duration/settings
        proceduralParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Disable default Renderer emission until configured
        ParticleSystemRenderer psRenderer = fogObj.GetComponent<ParticleSystemRenderer>();

        // Main module settings
        var main = proceduralParticleSystem.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 3.5f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.startColor = fogColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        // Emission module
        var emission = proceduralParticleSystem.emission;
        emission.rateOverTime = particleEmissionRate;

        // Shape module (Sphere volume around item)
        var shape = proceduralParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = fogRadius;

        // Color over lifetime (Fade in and fade out gracefully)
        var colorOverLifetime = proceduralParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0.0f), new GradientColorKey(fogColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(fogColor.a, 0.3f), new GradientAlphaKey(0f, 1.0f) }
        );
        colorOverLifetime.color = gradient;

        // Size over lifetime (Slightly expand as mist drifts)
        var sizeOverLifetime = proceduralParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.6f);
        curve.AddKey(1f, 1.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Assign default unlit particle material
        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (particleMat != null && psRenderer != null)
        {
            // Enable soft blending modes if available
            particleMat.SetFloat("_Mode", 2); // Fade mode
            psRenderer.material = particleMat;
        }

        proceduralParticleSystem.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckCollection(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckCollection(other.gameObject);
    }

    private void CheckCollection(GameObject target)
    {
        // Check if collision matches Player tag or PlayerController component
        if (target.CompareTag("Player") || target.GetComponentInParent<PlayerController>() != null)
        {
            // Disable item colliders immediately to prevent duplicate triggers
            Collider col3D = GetComponent<Collider>();
            if (col3D != null) col3D.enabled = false;

            Collider2D col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;

            // Trigger pickup callback
            onCollectedCallback?.Invoke();

            // Note: Immediate Destroy(gameObject) is omitted here so that TheWatcherEnemy
            // can manage fog particle fade-out over successDisplayDuration.
        }
    }
}