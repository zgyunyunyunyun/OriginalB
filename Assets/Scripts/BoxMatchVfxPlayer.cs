using UnityEngine;

[DisallowMultipleComponent]
public class BoxMatchVfxPlayer : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool playEnabled = true;
    [SerializeField] private ParticleSystem burstPrefab;
    [SerializeField] private bool parentToTarget = true;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float autoDestroyDelay = 10.5f;

    [Header("Scale")]
    [SerializeField, Min(0.05f)] private float worldScale = 0.4f;
    [SerializeField, Min(0.05f)] private float selfPreviewScaleMultiplier = 0.9f;

    [Header("Scatter")]
    [SerializeField] private bool emitFromCenterPoint = true;
    [SerializeField] private bool useWorldSimulationSpace = true;

    [Header("Self Preview")]
    [SerializeField] private bool playContinuouslyOnSelf = true;
    [SerializeField, Min(0.05f)] private float selfPreviewInterval = 0.35f;
    [SerializeField] private Color selfPreviewColor = new Color(1f, 0.66f, 0.18f, 1f);
    [SerializeField] private bool selfPreviewUseUnscaledTime = true;
    [SerializeField] private bool selfPreviewUseScreenCenter = true;
    [SerializeField] private Camera selfPreviewCamera;
    [SerializeField, Min(0f)] private float selfPreviewScreenDepth;
    [SerializeField] private bool selfPreviewReuseSingleSystem = true;

    [Header("Tint")]
    [SerializeField] private bool tintByMatchColor = true;
    [SerializeField] private Color fallbackTint = new Color(1f, 0.95f, 0.6f, 1f);
    [SerializeField] private float tintBrightness = 1.25f;

    [Header("Burst")]
    [SerializeField, Min(4)] private int burstCount = 108;
    [SerializeField, Min(0f)] private float radius = 1f;
    [SerializeField, Min(0.01f)] private float lifetime = 10f;
    [SerializeField, Min(0f)] private float speed = 5f;
    [SerializeField, Min(0.01f)] private float sizeMin = 0.01f;
    [SerializeField, Min(0.01f)] private float sizeMax = 1f;
    [SerializeField, Min(0f)] private float gravity = 0f;

    private Material runtimeParticleMaterial;
    private float nextSelfPreviewTime;
    private ParticleSystem selfPreviewRuntimeParticle;

    private void OnEnable()
    {
        nextSelfPreviewTime = 0f;
    }

    private void OnDisable()
    {
        ReleaseSelfPreviewParticle();
    }

    private void Update()
    {
        if (!playContinuouslyOnSelf || !playEnabled || !Application.isPlaying)
        {
            return;
        }

        var now = selfPreviewUseUnscaledTime ? Time.unscaledTime : Time.time;
        if (now < nextSelfPreviewTime)
        {
            return;
        }

        var interval = Mathf.Max(0.05f, selfPreviewInterval);
        nextSelfPreviewTime = now + interval;

        var previewTarget = ResolveSelfPreviewPosition();
        if (selfPreviewReuseSingleSystem)
        {
            PlaySelfPreviewBurst(previewTarget, selfPreviewColor);
            return;
        }

        PlayBurstAtPosition(previewTarget + localOffset, parentToTarget ? transform : null, selfPreviewColor, worldScale * selfPreviewScaleMultiplier);
    }

    public void PlayBurst(Transform target, Color matchColor)
    {
        PlayBurst(target, matchColor, parentToTarget);
    }

    public void PlayBurst(Transform target, Color matchColor, bool attachToTarget)
    {
        if (!playEnabled || target == null)
        {
            return;
        }

        var worldPosition = target.position + localOffset;
        PlayBurstAtPosition(worldPosition, attachToTarget ? target : null, matchColor, worldScale);
    }

    private void PlayBurstAtPosition(Vector3 worldPosition, Transform parent, Color matchColor, float scale)
    {
        if (burstPrefab != null)
        {
            var instance = Instantiate(burstPrefab, worldPosition, Quaternion.identity, parent);
            TryApplyTint(instance, matchColor);
            instance.Play(true);
            var destroyDelay = Mathf.Max(0.1f, autoDestroyDelay, lifetime + 0.2f);
            Destroy(instance.gameObject, destroyDelay);
            return;
        }

        var runtime = new GameObject("Match4SparkleBurst");
        runtime.transform.position = worldPosition;
        if (parent != null)
        {
            runtime.transform.SetParent(parent, true);
        }

        var ps = runtime.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureRuntimeParticle(ps, matchColor, scale);
        ps.Play(true);
        var runtimeDestroyDelay = Mathf.Max(0.2f, autoDestroyDelay, lifetime + 0.2f);
        Destroy(runtime, runtimeDestroyDelay);
    }

    private Vector3 ResolveSelfPreviewPosition()
    {
        if (!selfPreviewUseScreenCenter)
        {
            return transform.position;
        }

        var cameraRef = selfPreviewCamera != null ? selfPreviewCamera : Camera.main;
        if (cameraRef == null)
        {
            return transform.position;
        }

        if (cameraRef.orthographic)
        {
            return new Vector3(cameraRef.transform.position.x, cameraRef.transform.position.y, transform.position.z);
        }

        var depth = selfPreviewScreenDepth;
        if (depth <= 0f)
        {
            depth = Mathf.Abs(transform.position.z - cameraRef.transform.position.z);
            if (depth <= 0.001f)
            {
                depth = 10f;
            }
        }

        return cameraRef.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
    }

    private void PlaySelfPreviewBurst(Vector3 previewPosition, Color previewColor)
    {
        if (burstPrefab != null)
        {
            PlayBurstAtPosition(previewPosition + localOffset, parentToTarget ? transform : null, previewColor, worldScale * selfPreviewScaleMultiplier);
            return;
        }

        var ps = GetOrCreateSelfPreviewParticle();
        if (ps == null)
        {
            return;
        }

        ps.transform.position = previewPosition + localOffset;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureRuntimeParticle(ps, previewColor, worldScale * selfPreviewScaleMultiplier);
        ps.Play(true);
    }

    private ParticleSystem GetOrCreateSelfPreviewParticle()
    {
        if (selfPreviewRuntimeParticle != null)
        {
            return selfPreviewRuntimeParticle;
        }

        var runtime = new GameObject("SelfPreviewParticle");
        runtime.transform.SetParent(transform, true);
        selfPreviewRuntimeParticle = runtime.AddComponent<ParticleSystem>();
        return selfPreviewRuntimeParticle;
    }

    private void TryApplyTint(ParticleSystem particle, Color matchColor)
    {
        if (particle == null)
        {
            return;
        }

        var tint = ResolveTint(matchColor);
        var main = particle.main;
        main.startColor = tint;

        var colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tint, 0f),
                new GradientColorKey(Color.Lerp(tint, Color.white, 0.45f), 0.6f),
                new GradientColorKey(tint, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.9f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void ConfigureRuntimeParticle(ParticleSystem ps, Color matchColor, float scale)
    {
        if (ps == null)
        {
            return;
        }

        var tint = ResolveTint(matchColor);
        var safeScale = Mathf.Max(0.05f, scale);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        if (!ps.isPlaying)
        {
            main.duration = Mathf.Max(0.1f, lifetime + 0.15f);
        }

        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.78f, lifetime * 1.08f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f * safeScale, speed * 1.25f * safeScale);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin * safeScale, sizeMax * safeScale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.maxParticles = Mathf.Max(4, burstCount * 2);
        main.gravityModifier = gravity;
        main.simulationSpace = useWorldSimulationSpace
            ? ParticleSystemSimulationSpace.World
            : (parentToTarget ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(burstCount, 4, 256))
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = emitFromCenterPoint ? 0f : radius * safeScale;
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.randomDirectionAmount = 1f;
        shape.radiusThickness = emitFromCenterPoint ? 0f : 1f;
        shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tint, 0f),
                new GradientColorKey(Color.Lerp(tint, Color.white, 0.6f), 0.48f),
                new GradientColorKey(tint, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.9f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.25f, 1.15f),
            new Keyframe(1f, 0.05f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.4f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.material = GetOrCreateRuntimeMaterial();
        renderer.minParticleSize = 0.001f;
        renderer.maxParticleSize = 0.18f;
    }

    private Color ResolveTint(Color matchColor)
    {
        var baseColor = tintByMatchColor ? matchColor : fallbackTint;
        var bright = baseColor * Mathf.Max(0.01f, tintBrightness);
        bright.a = 1f;
        return bright;
    }

    private Material GetOrCreateRuntimeMaterial()
    {
        if (runtimeParticleMaterial != null)
        {
            return runtimeParticleMaterial;
        }

        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        runtimeParticleMaterial = shader != null ? new Material(shader) : null;
        return runtimeParticleMaterial;
    }

    private void OnDestroy()
    {
        ReleaseSelfPreviewParticle();

        if (runtimeParticleMaterial != null)
        {
            Destroy(runtimeParticleMaterial);
            runtimeParticleMaterial = null;
        }
    }

    private void ReleaseSelfPreviewParticle()
    {
        if (selfPreviewRuntimeParticle == null)
        {
            return;
        }

        if (selfPreviewRuntimeParticle.gameObject != null)
        {
            Destroy(selfPreviewRuntimeParticle.gameObject);
        }

        selfPreviewRuntimeParticle = null;
    }
}
