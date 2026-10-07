using System;
using UnityEngine;

public static class ForestNoise
{
    public static event Action<Vector3, float> Heard;
    public static void Emit(Vector3 position, float radius) => Heard?.Invoke(position, radius);
}
