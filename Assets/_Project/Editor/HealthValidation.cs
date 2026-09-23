using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Runs against a preview scene so the user's open scene is never modified.
public static class HealthValidation
{
    [MenuItem("Tools/Validate Player Health")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SampleScene.unity");
        try
        {
            PlayerHealth health = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                health = root.GetComponentInChildren<PlayerHealth>();
                if (health != null) break;
            }
            Check(health != null, "Scene must contain PlayerHealth.");
            var ui = health.GetComponent<PlayerUI>();
            Check(ui != null, "Player must contain PlayerUI.");
            var front = Field<Image>(ui, "frontHealthBar");
            var back = Field<Image>(ui, "backHealthBar");
            Check(front != null && back != null, "Both health images must be assigned.");
            Check(front.type == Image.Type.Filled && back.type == Image.Type.Filled,
                "Health images must use Filled mode.");

            Invoke(health, "Awake");
            Invoke(health, "Start");
            Check(health.CurrentHealth == 100f && front.fillAmount == 1f && back.fillAmount == 1f,
                "Player must start with full health.");

            health.TakeDamage(25f);
            Check(health.CurrentHealth == 75f && front.fillAmount == 0.75f && back.fillAmount == 1f,
                "Damage must update the front immediately and retain the damage trail.");
            health.TakeDamage(-10f);
            health.TakeDamage(float.NaN);
            health.TakeDamage(float.PositiveInfinity);
            health.Heal(-10f);
            Check(health.CurrentHealth == 75f, "Invalid amounts must not affect health.");

            health.Heal(10f);
            Check(health.CurrentHealth == 85f && front.fillAmount == 0.85f && back.fillAmount == 0.85f,
                "Healing must synchronize both bars.");
            health.Heal(1000f);
            Check(health.CurrentHealth == 100f, "Healing must clamp to maximum health.");

            int deaths = 0;
            Field<UnityEvent>(health, "onDeath").AddListener(() => deaths++);
            health.TakeDamage(1000f);
            health.TakeDamage(10f);
            health.Heal(100f);
            Check(health.IsDead && health.CurrentHealth == 0f && front.fillAmount == 0f && deaths == 1,
                "Death must clamp health, fire once and prevent normal healing.");

            health.RestoreFullHealth();
            Check(!health.IsDead && front.fillAmount == 1f && back.fillAmount == 1f,
                "Explicit restoration must revive and reset both bars.");
            health.TakeDamage(100f);
            Check(deaths == 2, "A restored player must be able to die again.");
            Debug.Log("Health validation PASSED: scene wiring, damage, healing, limits, death and restoration.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
