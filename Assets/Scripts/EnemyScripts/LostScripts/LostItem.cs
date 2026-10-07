using UnityEngine;
using System;

public class WatcherFogItem : MonoBehaviour
{
    [Header("Particle Effect Settings")]
    [Tooltip("Optional custom Fog Particle Prefab. If left empty, a dynamic fog effect is generated procedurally.")]
    [SerializeField] private GameObject customParticlePrefab;

    [Tooltip("Assign a Particle Material here to prevent Shader Stripping in builds.")]
    [SerializeField] private Material fogParticleMaterial;

    [Tooltip("Color of the fog mist particles.")]
    [SerializeField] private Color fogColor = new Color(0.8f, 0.85f, 0.9f, 0.25f);

    [Tooltip("Density / rate of fog particles spawned per second.")]
    [SerializeField] private float particleEmissionRate = 20f;

    [Tooltip("Radius around the item where fog particles drift.")]
    [SerializeField] private float fogRadius = 3f;

    private Action onCollectedCallback;
    private ParticleSystem proceduralParticleSystem;

    private void Awake()
    {
        EnsurePhysicsSetup();
    }

    public void Initialize(Action onCollected)
    {
        onCollectedCallback = onCollected;
        EnsurePhysicsSetup();

        if (customParticlePrefab != null)
        {
            Instantiate(customParticlePrefab, transform.position, Quaternion.identity, transform);
        }
        else if (GetComponentInChildren<ParticleSystem>() == null)
        {
            CreateDynamicFogEffect();
        }
    }

    private void EnsurePhysicsSetup()
    {
        // 1. Ensure 3D Collider exists and is set as trigger
        Collider col3D = GetComponent<Collider>();
        if (col3D != null)
        {
            col3D.isTrigger = true;
        }

        // 2. Ensure kinematic Rigidbody exists so OnTriggerEnter fires with CharacterController
        Rigidbody rb3D = GetComponent<Rigidbody>();
        if (rb3D == null)
        {
            rb3D = gameObject.AddComponent<Rigidbody>();
        }
        rb3D.isKinematic = true;
        rb3D.useGravity = false;

        // 3. Support 2D colliders/rigidbodies if applicable
        Collider2D col2D = GetComponent<Collider2D>();
        if (col2D != null)
        {
            col2D.isTrigger = true;
            Rigidbody2D rb2D = GetComponent<Rigidbody2D>();
            if (rb2D == null)
            {
                rb2D = gameObject.AddComponent<Rigidbody2D>();
            }
            rb2D.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void CreateDynamicFogEffect()
    {
        GameObject fogObj = new GameObject("FogMistEffect");
        fogObj.transform.SetParent(transform, false);

        proceduralParticleSystem = fogObj.AddComponent<ParticleSystem>();
        proceduralParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = fogObj.GetComponent<ParticleSystemRenderer>();

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

        var emission = proceduralParticleSystem.emission;
        emission.rateOverTime = particleEmissionRate;

        var shape = proceduralParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = fogRadius;

        var colorOverLifetime = proceduralParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(fogColor, 0.0f), new GradientColorKey(fogColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(fogColor.a, 0.3f), new GradientAlphaKey(0f, 1.0f) }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = proceduralParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.6f);
        curve.AddKey(1f, 1.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Prioritize assigned Inspector material to avoid build shader stripping
        Material particleMat = fogParticleMaterial;
        if (particleMat == null)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader != null) particleMat = new Material(particleShader);
        }

        if (particleMat != null && psRenderer != null)
        {
            psRenderer.material = particleMat;
        }

        proceduralParticleSystem.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckCollection(other.gameObject);
    }

    private void CheckCollection(GameObject target)
    {
        if (target.CompareTag("Player") || target.GetComponentInParent<PlayerController>() != null)
        {
            Collider col3D = GetComponent<Collider>();
            if (col3D != null) col3D.enabled = false;

            Collider2D col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;

            onCollectedCallback?.Invoke();
        }
    }
}