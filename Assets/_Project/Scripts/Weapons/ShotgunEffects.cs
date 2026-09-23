using UnityEngine;
using UnityEngine.Rendering;

// Small, short-lived bursts: daylight shotgun pellets are not glowing fireballs.
public sealed class ShotgunEffects : MonoBehaviour
{
    private Material ownedMaterial;
    private static Shader burstShader;
    private static Shader shellShader;

    public static void Prewarm()
    {
        if (burstShader == null) burstShader = Resources.Load<Shader>("ShotgunSoft");
        if (shellShader == null) shellShader = Shader.Find("Universal Render Pipeline/Lit");
    }

    private static void Burst(Vector3 position, Vector3 direction, Color color, int count,
        float lifetime, float speed, float size, float intensity)
    {
        var go = new GameObject("Shotgun burst");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var cleanup = go.AddComponent<ShotgunEffects>();
        Prewarm();
        Shader shader = burstShader;
        if (shader == null) { Destroy(go); return; }
        cleanup.ownedMaterial = new Material(shader);
        cleanup.ownedMaterial.SetFloat("_Intensity", intensity);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.015f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var scale = ps.sizeOverLifetime;
        scale.enabled = true;
        scale.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1f));
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = cleanup.ownedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        Destroy(go, lifetime + 0.15f);
    }

    public static void Smoke(Vector3 position, Vector3 direction, int count = 8) =>
        Burst(position, direction, new Color(0.38f, 0.36f, 0.32f, 0.28f), count, 0.65f, 0.45f, 0.18f, 1f);

    public static void Fire(Vector3 position, Vector3 direction)
    {
        Burst(position, direction, new Color(1f, 0.65f, 0.18f), 7, 0.055f, 5f, 0.16f, 6f);
        Smoke(position, direction);
        var flash = new GameObject("Muzzle light");
        flash.transform.position = position;
        var light = flash.AddComponent<Light>();
        light.color = new Color(1f, 0.63f, 0.28f);
        light.intensity = 4f;
        light.range = 3f;
        light.shadows = LightShadows.None;
        Destroy(flash, 0.045f);
    }

    public static void Impact(Vector3 position, Vector3 normal, bool flesh) =>
        Burst(position, normal, flesh ? new Color(0.3f, 0.035f, 0.025f) : new Color(0.48f, 0.43f, 0.35f),
            3, 0.3f, 1.8f, 0.065f, 1f);

    public static void EjectShell(Vector3 position, Vector3 velocity)
    {
        var shell = CreateShellVisual("Spent shotgun shell");
        shell.transform.SetPositionAndRotation(position, Random.rotation);
        // Visual ejection uses a ballistic arc without colliding with the player.
        var flight = shell.AddComponent<ShotgunShell>();
        flight.velocity = velocity;
        Destroy(shell, 2f);
    }

    // Reload visuals stay alive until their weapon destroys them; only ejected shells expire.
    public static GameObject CreateShellVisual(string name)
    {
        Prewarm();
        var shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shell.name = name;
        shell.transform.localScale = new Vector3(0.018f, 0.027f, 0.018f);
        shell.GetComponent<Collider>().enabled = false;
        var cleanup = shell.AddComponent<ShotgunEffects>();
        var shader = shellShader;
        if (shader != null)
        {
            cleanup.ownedMaterial = new Material(shader);
            cleanup.ownedMaterial.color = new Color(0.32f, 0.035f, 0.02f);
            cleanup.ownedMaterial.SetFloat("_Smoothness", 0.45f);
            shell.GetComponent<Renderer>().sharedMaterial = cleanup.ownedMaterial;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Brass shell base";
            cap.transform.SetParent(shell.transform, false);
            cap.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            cap.transform.localScale = new Vector3(1.08f, 0.2f, 1.08f);
            cap.GetComponent<Collider>().enabled = false;
            var capCleanup = cap.AddComponent<ShotgunEffects>();
            capCleanup.ownedMaterial = new Material(shader);
            capCleanup.ownedMaterial.color = new Color(0.58f, 0.36f, 0.1f);
            capCleanup.ownedMaterial.SetFloat("_Metallic", 0.8f);
            capCleanup.ownedMaterial.SetFloat("_Smoothness", 0.6f);
            cap.GetComponent<Renderer>().sharedMaterial = capCleanup.ownedMaterial;
        }
        return shell;
    }

    private void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
}

internal sealed class ShotgunShell : MonoBehaviour
{
    public Vector3 velocity;
    private bool resting;
    private void Update()
    {
        if (resting) return;
        velocity += Physics.gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerHealth>() == null)
        {
            transform.position = hit.point + hit.normal * 0.015f;
            resting = true;
            enabled = false;
            return;
        }
        transform.position += step;
        transform.Rotate(new Vector3(380f, 200f, 140f) * Time.deltaTime);
    }
}
