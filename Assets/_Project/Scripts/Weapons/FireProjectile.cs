using UnityEngine;
using UnityEngine.Rendering;

// Sweeps the entire travel segment each frame, including the camera-to-muzzle
// segment on spawn. Fast rounds cannot skip walls or damage their owner.
public sealed class FireProjectile : MonoBehaviour
{
    private Transform owner;
    private Vector3 velocity;
    private float damage;
    private float remainingLife = 3f;
    private Material glow;
    private bool impacted;
    private const float Radius = 0.025f;
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    public static bool FindHit(Vector3 start, Vector3 end, Transform owner, float radius, out RaycastHit nearest)
    {
        nearest = default;
        Vector3 segment = end - start;
        if (segment.sqrMagnitude < 0.000001f) return false;
        float distance = float.PositiveInfinity;
        int count = Physics.SphereCastNonAlloc(start, radius, segment.normalized, hitBuffer,
            segment.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = hitBuffer;
        // A full buffer may omit the nearest hit; fall back in dense scenes.
        if (count == hitBuffer.Length)
        {
            hits = Physics.SphereCastAll(start, radius, segment.normalized,
                segment.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (owner != null && hit.collider.transform.IsChildOf(owner)) continue;
            if (hit.distance >= distance) continue;
            nearest = hit;
            distance = hit.distance;
        }
        return distance < float.PositiveInfinity;
    }

    public static FireProjectile Launch(Vector3 eye, Vector3 muzzle, Vector3 direction,
        Transform owner, float damage, float speed)
    {
        var projectile = new GameObject("Glowing projectile").AddComponent<FireProjectile>();
        projectile.owner = owner;
        projectile.damage = damage;
        projectile.velocity = direction.normalized * Mathf.Max(1f, speed);
        projectile.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(direction));
        projectile.CreateVisuals();
        if (FindHit(eye, muzzle, owner, Radius, out RaycastHit obstruction))
            projectile.Impact(obstruction);
        return projectile;
    }

    private void CreateVisuals()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader != null)
        {
            glow = new Material(shader);
            glow.color = new Color(1f, 0.48f, 0.06f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.26f, 0.025f) * 8f);
        }

        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Hot core";
        core.transform.SetParent(transform, false);
        core.transform.localScale = new Vector3(0.055f, 0.055f, 0.13f);
        core.GetComponent<Collider>().enabled = false;
        Destroy(core.GetComponent<Collider>());
        var renderer = core.GetComponent<Renderer>();
        if (glow != null) renderer.sharedMaterial = glow;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.09f;
        trail.minVertexDistance = 0.015f;
        trail.startWidth = 0.07f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.85f, 0.2f);
        trail.endColor = new Color(1f, 0.08f, 0f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        if (glow != null) trail.sharedMaterial = glow;

        var sparksObject = new GameObject("Fire sparks");
        sparksObject.transform.SetParent(transform, false);
        var sparks = sparksObject.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.startLifetime = 0.12f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
        main.startColor = new Color(1f, 0.35f, 0.03f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;
        var emission = sparks.emission;
        emission.rateOverTime = 45f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;
        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
        if (glow != null) sparkRenderer.sharedMaterial = glow;
        sparks.Play();

        var light = gameObject.AddComponent<Light>();
        light.color = new Color(1f, 0.35f, 0.06f);
        light.range = 2f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (impacted) return;
        float step = Mathf.Min(Time.deltaTime, remainingLife);
        Vector3 next = transform.position + velocity * step;
        if (FindHit(transform.position, next, owner, Radius, out RaycastHit hit))
        {
            Impact(hit);
            return;
        }
        transform.position = next;
        remainingLife -= step;
        if (remainingLife <= 0f) Destroy(gameObject);
    }

    private void Impact(RaycastHit hit)
    {
        if (impacted) return;
        impacted = true;
        // A brief stationary flash at contact also confirms hits on scenery.
        transform.position = hit.point + hit.normal * Radius;
        var health = hit.collider.GetComponentInParent<EnemyHealth>();
        if (health != null) health.TakeDamage(damage);
        else hit.collider.GetComponentInParent<MotherStagger>()?.TriggerStagger();
        GetComponent<Light>().intensity = 5f;
        Destroy(gameObject, 0.09f);
    }

    private void OnDestroy()
    {
        if (glow != null) Destroy(glow);
    }
}
