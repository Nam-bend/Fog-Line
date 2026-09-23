using System.Linq;
using UnityEditor;
using UnityEngine;

// Increase the authored idle's breathing while preserving its pose, feet and loop.
public static class MotherIdleAnimation
{
    public const string AssetPath = "Assets/_Project/Animations/Enemies/MotherMoster/MotherMoster_Idle.anim";

    public static AnimationClip Build(AnimationClip source)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetPath);
        if (clip == null)
        {
            clip = Object.Instantiate(source);
            AssetDatabase.CreateAsset(clip, AssetPath);
        }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = "MotherMoster_Idle";
        var bindings = AnimationUtility.GetCurveBindings(source);
        foreach (var group in bindings.Where(b => b.propertyName.StartsWith("m_LocalRotation.")).GroupBy(b => b.path))
        {
            string bone = group.Key.Split('/').Last();
            float gain = bone == "spine" ? 6f : bone == "chest" ? -6f : bone == "head" ? 2f : 1f;
            if (gain == 1f) continue;
            var axes = new[] { "x", "y", "z", "w" };
            var rotationBindings = axes.Select(axis => group.First(b => b.propertyName == "m_LocalRotation." + axis)).ToArray();
            var curves = rotationBindings.Select(b => AnimationUtility.GetEditorCurve(source, b)).ToArray();
            var output = axes.Select(_ => new AnimationCurve()).ToArray();
            Quaternion Sample(float time) => new Quaternion(curves[0].Evaluate(time), curves[1].Evaluate(time),
                curves[2].Evaluate(time), curves[3].Evaluate(time)).normalized;
            Quaternion rest = Sample(0);
            Quaternion previous = rest;
            int frames = Mathf.CeilToInt(source.length * 30f);
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = source.length * frame / frames;
                Quaternion rotation = rest * Quaternion.SlerpUnclamped(Quaternion.identity,
                    Quaternion.Inverse(rest) * Sample(time), gain);
                if (Quaternion.Dot(previous, rotation) < 0)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                for (int axis = 0; axis < 4; axis++) output[axis].AddKey(time, rotation[axis]);
                previous = rotation;
            }
            for (int axis = 0; axis < 4; axis++) AnimationUtility.SetEditorCurve(clip, rotationBindings[axis], output[axis]);
        }
        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
