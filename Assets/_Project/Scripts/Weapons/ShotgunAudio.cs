using UnityEngine;

// Original synthesized fallback clips, built once on equip rather than per shot.
// Production recordings can be assigned in Inspector without changing timing.
[DisallowMultipleComponent]
public sealed class ShotgunAudio : MonoBehaviour
{
    public enum Cue { Fire, Dry, Open, Eject, Insert, Close }
    [SerializeField] private AudioClip fire, dry, open, eject, insert, close;
    [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
    private readonly AudioClip[] clips = new AudioClip[6];
    private readonly bool[] owned = new bool[6];
    private AudioSource source;

    public void Initialize()
    {
        if (source != null) return;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        AudioClip[] assigned = { fire, dry, open, eject, insert, close };
        for (int i = 0; i < clips.Length; i++)
        {
            owned[i] = assigned[i] == null;
            clips[i] = assigned[i] != null ? assigned[i] : Synthesize((Cue)i);
        }
    }

    public void Play(Cue cue)
    {
        Initialize();
        source.PlayOneShot(clips[(int)cue], cue == Cue.Fire ? 1f : 0.5f);
    }

    private static AudioClip Synthesize(Cue cue)
    {
        const int rate = 22050;
        float length = cue == Cue.Fire ? 0.48f : cue == Cue.Open ? 0.18f : 0.11f;
        var samples = new float[Mathf.CeilToInt(length * rate)];
        var random = new System.Random(1837 + (int)cue);
        float filtered = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            filtered = Mathf.Lerp(filtered, noise, 0.22f);
            float sample;
            if (cue == Cue.Fire)
                sample = noise * Mathf.Exp(-t * 85f) * 0.55f
                    + filtered * Mathf.Exp(-t * 12f) * 0.6f
                    + Mathf.Sin(t * 2f * Mathf.PI * (95f - 45f * t)) * Mathf.Exp(-t * 20f) * 0.32f;
            else
            {
                float frequency = cue == Cue.Dry ? 2400f : cue == Cue.Close ? 440f : 1200f + 170f * (int)cue;
                sample = (noise * 0.5f + Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.3f)
                    * Mathf.Exp(-t * (cue == Cue.Open ? 28f : 65f));
            }
            float attack = Mathf.Min(1f, t / 0.001f);
            float fade = Mathf.Clamp01((length - t) / 0.02f);
            samples[i] = Mathf.Clamp(sample * attack * fade, -0.95f, 0.95f);
        }
        var clip = AudioClip.Create("Shotgun " + cue, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable() { if (source != null) source.Stop(); }
    private void OnDestroy()
    {
        for (int i = 0; i < clips.Length; i++) if (owned[i] && clips[i] != null) Destroy(clips[i]);
        if (source != null) Destroy(source);
    }
}
