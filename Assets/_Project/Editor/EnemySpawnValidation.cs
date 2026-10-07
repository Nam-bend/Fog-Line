using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class EnemySpawnValidation
{
    const string Request = "Logs/EnemySpawnValidation.request";
    [InitializeOnLoadMethod]
    static void Listen()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Validate(); }
        catch (Exception e) { File.WriteAllText("Logs/EnemySpawnValidation.txt", e.ToString()); Debug.LogException(e); }
    }

    public static void Batch()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/ForestDemo.unity");
        Validate();
    }

    [MenuItem("Tools/Forest/Validate Enemy Spawn")]
    public static void Validate()
    {
        var player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null) throw new InvalidOperationException("Open the game scene first.");
        Physics.SyncTransforms();
        var report = new StringBuilder();
        var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        report.AppendLine($"Player pivot: {player.transform.position}");
        report.AppendLine("Old 4 m pivot query: " + NavMesh.SamplePosition(player.transform.position, out _, 4, filter));
        var controller = player.GetComponent<CharacterController>();
        Vector3 feet = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>();
        var serialized = new SerializedObject(spawner);
        var placed = serialized.FindProperty("sceneEnemy").objectReferenceValue as GameObject;
        float distance = serialized.FindProperty("spawnDistance").floatValue;
        Check("Current scene placement", feet, placed != null ? placed.transform : null);
        Check("Player starts 20 m higher", feet + Vector3.up * 20, null);
        Check("Player on the ground", FloorFeet(), null);
        if (placed != null)
        {
            if (!EnemySpawnPlacement.TryFloorNav(placed.transform.position - Vector3.up * 20,
                    player.transform, placed.transform, filter, out var floor))
                throw new InvalidOperationException("Cannot recover buried scene enemy.");
            report.AppendLine($"Scene enemy buried 20 m: recovered to {floor.position}.");
        }
        foreach (var mother in Object.FindObjectsByType<MotherAI>(FindObjectsSortMode.None))
        {
            var agent = mother.GetComponent<NavMeshAgent>();
            var motherFilter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!EnemySpawnPlacement.TryFloorNav(mother.transform.position, player.transform, mother.transform,
                    motherFilter, out var floor, 4f))
                throw new InvalidOperationException("No walkable floor for Mother at " + mother.transform.position);
            report.AppendLine($"Mother: {mother.transform.position} -> {floor.position}.");
        }
        var blocked = new NavMeshQueryFilter { agentTypeID = 0, areaMask = 0 };
        if (EnemySpawnPlacement.TryFind(feet, player.transform.forward, player.transform, null,
                distance, blocked, out _, out _)) throw new InvalidOperationException("Spawn accepted excluded navigation areas.");
        report.AppendLine("No permitted navigation areas: correctly rejected.");
        report.AppendLine("ENEMY_SPAWN_VALIDATION_OK");
        File.WriteAllText("Logs/EnemySpawnValidation.txt", report.ToString());
        Debug.Log(report.ToString());

        Vector3 FloorFeet()
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var local = feet - t.transform.position;
                if (local.x >= 0 && local.z >= 0 && local.x <= t.terrainData.size.x && local.z <= t.terrainData.size.z)
                    return new Vector3(feet.x, t.SampleHeight(feet) + t.transform.position.y + .1f, feet.z);
            }
            return feet;
        }

        Vector3 Check(string label, Vector3 start, Transform authored)
        {
            if (!EnemySpawnPlacement.TryFind(start, player.transform.forward, player.transform, authored,
                    distance, filter, out var point, out string failure))
                throw new InvalidOperationException(label + ": " + failure);
            if (!NavMesh.SamplePosition(point, out _, .1f, filter)) throw new InvalidOperationException(label + ": point not on navigation.");
            report.AppendLine($"{label}: PASS at {point}");
            return point;
        }
    }
}
