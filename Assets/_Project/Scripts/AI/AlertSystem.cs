using System;
using UnityEngine;

// Global, scene-local gameplay signal. Enemies can raise an alert without
// knowing which MotherAI instances are listening.
public static class AlertSystem
{
    public static event Action<Vector3> OnEnemyAlerted;

    public static void RaiseEnemyAlerted(Vector3 position)
    {
        OnEnemyAlerted?.Invoke(position);
    }
}
