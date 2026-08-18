using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SwimmableWater : MonoBehaviour
{
    [SerializeField] private float surfaceOffset = 0f;

    [Header("Entry Effects")]
    [SerializeField] private Material splashMaterial;
    [SerializeField] private Material rippleMaterial;
    [SerializeField, Min(1)] private int splashDropletCount = 16;
    [SerializeField, Min(0.1f)] private float splashHeight = 4.5f;
    [SerializeField, Min(0.1f)] private float rippleLifetime = 1.6f;
    [SerializeField, Min(0.1f)] private float rippleSpeed = 2.5f;

    public float SurfaceHeight => transform.position.y + surfaceOffset;

    private readonly List<SplashDroplet> activeDroplets = new List<SplashDroplet>();
    private readonly List<SurfaceRipple> activeRipples = new List<SurfaceRipple>();
    private readonly HashSet<Rigidbody> bodiesInWater = new HashSet<Rigidbody>();
    private Transform effectsRoot;

    private sealed class SplashDroplet
    {
        public GameObject gameObject;
        public Vector3 velocity;
        public Vector3 initialScale;
        public float age;
        public float lifetime;
    }

    private sealed class SurfaceRipple
    {
        public GameObject gameObject;
        public LineRenderer renderer;
        public Vector3 center;
        public float age;
        public float delay;
        public float lifetime;
        public float speed;
        public float startRadius;
        public float startWidth;
    }

    private void Awake()
    {
        GameObject root = new GameObject("Runtime Water Entry Effects");
        effectsRoot = root.transform;
        effectsRoot.SetParent(transform.parent, false);
        effectsRoot.localPosition = Vector3.zero;
    }

    private void Update()
    {
        UpdateDroplets(Time.deltaTime);
        UpdateRipples(Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody enteringBody = other.attachedRigidbody;
        if (enteringBody == null || enteringBody.GetComponent<Player>() == null || !bodiesInWater.Add(enteringBody))
        {
            return;
        }

        Vector3 velocity = enteringBody.linearVelocity;
        if (enteringBody.worldCenterOfMass.y < SurfaceHeight - 1.25f || velocity.y > 1f)
        {
            return;
        }

        Vector3 splashPosition = enteringBody.worldCenterOfMass;
        splashPosition.y = SurfaceHeight + 0.03f;
        float impactStrength = Mathf.Clamp01((-velocity.y + 1f) / 9f);
        SpawnEntryEffect(splashPosition, impactStrength);
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody exitingBody = other.attachedRigidbody;
        if (exitingBody != null)
        {
            bodiesInWater.Remove(exitingBody);
        }
    }

    private void OnDestroy()
    {
        if (effectsRoot != null)
        {
            Destroy(effectsRoot.gameObject);
        }
    }

    private void SpawnEntryEffect(Vector3 position, float impactStrength)
    {
#if UNITY_EDITOR
        Debug.Log("Water entry splash at " + position + " (impact " + impactStrength.ToString("0.00") + ")", this);
#endif

        int dropletCount = Mathf.RoundToInt(Mathf.Lerp(splashDropletCount * 0.65f, splashDropletCount, impactStrength));
        for (int index = 0; index < dropletCount; index++)
        {
            SpawnDroplet(position, impactStrength, index);
        }

        for (int index = 0; index < 3; index++)
        {
            SpawnRipple(position, impactStrength, index);
        }
    }

    private void SpawnDroplet(Vector3 position, float impactStrength, int index)
    {
        GameObject dropletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dropletObject.name = "Splash Droplet " + (index + 1);
        dropletObject.transform.SetParent(effectsRoot, true);
        dropletObject.transform.position = position + Vector3.up * Random.Range(0.02f, 0.12f);

        float size = Random.Range(0.035f, 0.085f) * Mathf.Lerp(0.8f, 1.35f, impactStrength);
        Vector3 scale = new Vector3(size, size * Random.Range(1.4f, 2.4f), size);
        dropletObject.transform.localScale = scale;

        Collider dropletCollider = dropletObject.GetComponent<Collider>();
        if (dropletCollider != null)
        {
            dropletCollider.enabled = false;
            Destroy(dropletCollider);
        }

        MeshRenderer meshRenderer = dropletObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null && splashMaterial != null)
        {
            meshRenderer.sharedMaterial = splashMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float horizontalSpeed = Random.Range(0.7f, 2.3f) * Mathf.Lerp(0.75f, 1.25f, impactStrength);
        Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * horizontalSpeed;
        float verticalSpeed = Random.Range(splashHeight * 0.55f, splashHeight) * Mathf.Lerp(0.75f, 1.2f, impactStrength);

        activeDroplets.Add(new SplashDroplet
        {
            gameObject = dropletObject,
            velocity = outward + Vector3.up * verticalSpeed,
            initialScale = scale,
            age = 0f,
            lifetime = Random.Range(0.85f, 1.3f)
        });
    }

    private void SpawnRipple(Vector3 position, float impactStrength, int index)
    {
        GameObject rippleObject = new GameObject("Surface Ripple " + (index + 1));
        rippleObject.transform.SetParent(effectsRoot, true);

        LineRenderer line = rippleObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 65;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.generateLightingData = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = rippleMaterial != null ? rippleMaterial : splashMaterial;
        line.enabled = false;

        activeRipples.Add(new SurfaceRipple
        {
            gameObject = rippleObject,
            renderer = line,
            center = new Vector3(position.x, SurfaceHeight + 0.035f + index * 0.006f, position.z),
            age = 0f,
            delay = index * 0.14f,
            lifetime = rippleLifetime * (1f + index * 0.12f),
            speed = rippleSpeed * Mathf.Lerp(0.9f, 1.25f, impactStrength),
            startRadius = 0.18f + index * 0.08f,
            startWidth = Mathf.Lerp(0.055f, 0.11f, impactStrength)
        });
    }

    private void UpdateDroplets(float deltaTime)
    {
        for (int index = activeDroplets.Count - 1; index >= 0; index--)
        {
            SplashDroplet droplet = activeDroplets[index];
            if (droplet.gameObject == null)
            {
                activeDroplets.RemoveAt(index);
                continue;
            }

            droplet.age += deltaTime;
            droplet.velocity += Physics.gravity * deltaTime;
            droplet.gameObject.transform.position += droplet.velocity * deltaTime;

            float remaining = Mathf.Clamp01(1f - droplet.age / droplet.lifetime);
            droplet.gameObject.transform.localScale = droplet.initialScale * Mathf.Lerp(0.35f, 1f, remaining);

            bool returnedToWater = droplet.age > 0.12f
                && droplet.velocity.y < 0f
                && droplet.gameObject.transform.position.y <= SurfaceHeight;
            if (droplet.age >= droplet.lifetime || returnedToWater)
            {
                Destroy(droplet.gameObject);
                activeDroplets.RemoveAt(index);
            }
        }
    }

    private void UpdateRipples(float deltaTime)
    {
        const int segmentCount = 64;

        for (int index = activeRipples.Count - 1; index >= 0; index--)
        {
            SurfaceRipple ripple = activeRipples[index];
            if (ripple.gameObject == null)
            {
                activeRipples.RemoveAt(index);
                continue;
            }

            ripple.age += deltaTime;
            if (ripple.age < ripple.delay)
            {
                continue;
            }

            float effectAge = ripple.age - ripple.delay;
            float progress = Mathf.Clamp01(effectAge / ripple.lifetime);
            if (progress >= 1f)
            {
                Destroy(ripple.gameObject);
                activeRipples.RemoveAt(index);
                continue;
            }

            ripple.renderer.enabled = true;
            float radius = ripple.startRadius + effectAge * ripple.speed;
            float width = ripple.startWidth * (1f - progress) * (1f - progress);
            ripple.renderer.startWidth = width;
            ripple.renderer.endWidth = width;

            for (int segment = 0; segment <= segmentCount; segment++)
            {
                float angle = segment / (float)segmentCount * Mathf.PI * 2f;
                Vector3 point = ripple.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                ripple.renderer.SetPosition(segment, point);
            }
        }
    }
}
