using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class ForestStoryDiagnostics
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/ForestDemo.unity");
        var report = new StringBuilder();
        var terrain = Object.FindFirstObjectByType<Terrain>();
        report.AppendLine($"Terrain {terrain.transform.position} size {terrain.terrainData.size}");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            report.AppendLine($"Root {root.name}: {root.transform.position}");
        var mesh = NavMesh.CalculateTriangulation();
        report.AppendLine($"Nav vertices {mesh.vertices.Length}");
        for (int z = -40; z <= 40; z += 5)
        for (int x = -40; x <= 40; x += 5)
        {
            Vector3 p = new Vector3(x, terrain.SampleHeight(new Vector3(x,0,z)) + terrain.transform.position.y,z);
            bool found = NavMesh.SamplePosition(p, out var hit, 2f, new NavMeshQueryFilter { agentTypeID=0, areaMask=NavMesh.AllAreas });
            report.AppendLine($"{x},{z}: y={p.y:F2} nav={found} {(found ? hit.position.ToString() : "")}");
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ForestStoryDiagnostics.txt",report.ToString());
    }
}
