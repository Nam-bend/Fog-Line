using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Adjust existing placements: keep the authored forest, sources, terrain and trail.
public static class ForestTreePolish
{
    const string ScenePath = "Assets/_Project/Scenes/ForestDemo.unity";
    const string ReportPath = "Logs/ForestTreePolish.txt";

    public static void InspectResult()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        ForestRefresh.Capture();
        var report = new System.Text.StringBuilder();
        report.AppendLine("Trees: " + terrain.terrainData.treeInstanceCount);
        float maxGap = float.NegativeInfinity;
        var data = terrain.terrainData;
        foreach (var tree in data.treeInstances)
        {
            var prefab = data.treePrototypes[tree.prototypeIndex].prefab;
            var origin = terrain.transform.position + Vector3.Scale(tree.position, data.size);
            var rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
            foreach (var vertex in RootBand(prefab))
            {
                var point = origin + rotation * Vector3.Scale(vertex, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
                maxGap = Mathf.Max(maxGap, point.y - terrain.SampleHeight(point) - terrain.transform.position.y);
            }
        }
        report.AppendLine($"Saved root-band maximum clearance: {maxGap:F3} m; all tree prototypes resolved.");
        foreach (var surface in Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsSortMode.None))
        {
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = -1 };
            foreach (var p in new[] { new Vector3(-22,0,-27), new Vector3(15,0,20) })
            {
                var point = p; point.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                bool found = UnityEngine.AI.NavMesh.SamplePosition(point, out var hit, 15, filter);
                report.AppendLine($"Agent {surface.agentTypeID}: {point} found={found} closest={hit.position} distance={hit.distance}");
            }
        }
        File.WriteAllText("Logs/ForestTreeDiagnosis.txt", report.ToString());
        if (maxGap > 0) throw new InvalidOperationException($"Saved roots are not grounded: {maxGap:F4} m.");
    }

    static Vector3[] RootBand(GameObject prefab)
    {
        if (prefab == null) throw new InvalidOperationException("Missing tree prototype.");
        bool grounded = prefab.name.EndsWith("_Grounded");
        string name = prefab.name.Replace("_Grounded", "");
        float collar = name == "WoodlandTree1" ? .92f : name == "WoodlandTree2" ? 2.06f :
            name == "WoodlandTree3" ? 1.40f : name == "WoodlandTreeLeafy" ? 1.87f : -1;
        if (collar < 0) throw new InvalidOperationException("Unrecognized tree: " + name);
        if (grounded) collar -= 4;
        return prefab.GetComponent<MeshFilter>().sharedMesh.vertices.Where(v => v.y <= collar).ToArray();
    }

    public static void FixTerrainFloorClamping()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play before adjusting trees.");
        // Lift the anchor inside Terrain's normalized vertical range, compensating
        // in mesh/collider space. Keep shared originals for other scenes and backups.
        EditorSceneManager.OpenScene(ScenePath);
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var data = terrain.terrainData;
        var prototypes = data.treePrototypes;
        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prefab.name.EndsWith("_Grounded")) continue;
            string name = prototypes[i].prefab.name + "_Grounded";
            string folder = "Assets/_Project/Environment/ForestRefresh/";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
            if (existing != null) { prototypes[i] = new TreePrototype { prefab = existing }; continue; }
            var mesh = Object.Instantiate(prototypes[i].prefab.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = name;
            mesh.vertices = mesh.vertices.Select(v => v - Vector3.up * 4).ToArray();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + name + ".asset");
            var go = Object.Instantiate(prototypes[i].prefab);
            go.name = name;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = go.GetComponent<CapsuleCollider>();
            collider.center -= Vector3.up * 4;
            go.GetComponent<LODGroup>().RecalculateBounds();
            prototypes[i] = new TreePrototype { prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + name + ".prefab") };
            Object.DestroyImmediate(go);
        }
        var trees = data.treeInstances;
        var groundedTrees = new List<TreeInstance>();
        var bands = prototypes.Select(p => RootBand(p.prefab)).ToArray();
        for (int i = 0; i < trees.Length; i++)
        {
            var tree = trees[i];
            var world = terrain.transform.position + Vector3.Scale(tree.position, data.size);
            var rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
            float originY = float.PositiveInfinity;
            foreach (var vertex in bands[tree.prototypeIndex])
            {
                var offset = rotation * Vector3.Scale(vertex, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
                originY = Mathf.Min(originY, terrain.SampleHeight(world + offset) + terrain.transform.position.y - offset.y - .08f);
            }
            tree.position.y = (originY - terrain.transform.position.y) / data.size.y;
            // Terrain serializes normalized anchors only inside its vertical range.
            // Leave the few extreme hilltop/bottom placements clear instead of clamping roots.
            if (tree.position.y < 0 || tree.position.y > 1) continue;
            groundedTrees.Add(tree);
        }
        data.treePrototypes = prototypes;
        if (groundedTrees.Count < 300) throw new InvalidOperationException("Too few grounded trees.");
        data.SetTreeInstances(groundedTrees.ToArray(), false);
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        terrain.Flush();
        AssetDatabase.SaveAssets();
        ForestDemoSetup.Bake();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        InspectResult();
        File.AppendAllText(ReportPath, $"Final grounded tree count: {data.treeInstanceCount}\nGrounded prefab anchors avoid Terrain height clamping; saved roots verified.\n");
    }

    [MenuItem("Tools/Forest/Thin Trees And Ground Roots")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play before adjusting trees.");
        if (Application.isBatchMode) EditorSceneManager.OpenScene(ScenePath);
        if (SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open ForestDemo first.");
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var data = terrain.terrainData;
        var prototypes = data.treePrototypes;
        // The trunk's authored origin is at its root collar, above the lowest root tip.
        var rootBands = new List<Vector3[]>();
        foreach (var prototype in prototypes)
        {
            rootBands.Add(RootBand(prototype.prefab));
        }

        string backup = "ArtSource/ForestBackups/TreePolish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        // Include the live scene and shared TerrainData/navigation, not just a scene reference.
        string sceneSnapshot = "Logs/ForestBeforeTreePolish.unity";
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), sceneSnapshot, true))
            throw new IOException("Cannot back up current scene.");
        using (var archive = ZipFile.Open(backup, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(sceneSnapshot, ScenePath);
            var paths = new[] { ScenePath + ".meta", AssetDatabase.GetAssetPath(data) }
                .Concat(Directory.GetFiles("Assets/_Project/Environment/ForestDemo", "NavMesh_*.asset"));
            foreach (string path in paths)
            {
                archive.CreateEntryFromFile(path, path.Replace('\\', '/'));
                if (File.Exists(path + ".meta")) archive.CreateEntryFromFile(path + ".meta", path.Replace('\\', '/') + ".meta");
            }
        }
        ForestRefresh.Capture();
        foreach (string view in new[] { "Player", "Cabin", "Overview" })
            File.Copy("Logs/ForestClean" + view + ".png", "Logs/ForestTreesBefore" + view + ".png", true);
        ForestDemoSetup.Bake();
        string baseline = "Passed";
        try { ForestDemoSetup.Validate(); }
        catch (InvalidOperationException e) { baseline = e.Message; }

        var kept = new List<TreeInstance>();
        var positions = new List<Vector2>();
        int before = data.treeInstanceCount;
        float maxRootGap = float.NegativeInfinity;
        foreach (var original in data.treeInstances)
        {
            var tree = original;
            Vector3 world = terrain.transform.position + Vector3.Scale(tree.position, data.size);
            var point = new Vector2(world.x, world.z);
            bool central = Mathf.Abs(world.x) < 36 && Mathf.Abs(world.z) < 36;
            float spacing = central ? 5.0f : 4.2f;
            if (positions.Any(p => (p - point).sqrMagnitude < spacing * spacing)) continue;
            if (data.GetSteepness(tree.position.x, tree.position.z) > 42) continue;
            // Clear the start's immediate view and the cabin approach without moving landmarks.
            if (Vector2.Distance(point, new Vector2(-22, -27)) < 7) continue;
            var rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
            var scale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
            float originY = float.PositiveInfinity;
            foreach (var vertex in rootBands[tree.prototypeIndex])
            {
                var offset = rotation * Vector3.Scale(vertex, scale);
                float ground = terrain.SampleHeight(world + offset) + terrain.transform.position.y;
                originY = Mathf.Min(originY, ground - offset.y - .08f);
            }
            float centerGround = terrain.SampleHeight(world) + terrain.transform.position.y;
            // Avoid deep burial on abrupt slopes; keep those areas as natural clearings.
            if (centerGround - originY > 4.0f * tree.heightScale) continue;
            tree.position.y = (originY - terrain.transform.position.y) / data.size.y;
            kept.Add(tree);
            positions.Add(point);
            foreach (var vertex in rootBands[tree.prototypeIndex])
            {
                var offset = rotation * Vector3.Scale(vertex, scale);
                float ground = terrain.SampleHeight(world + offset) + terrain.transform.position.y;
                maxRootGap = Mathf.Max(maxRootGap, originY + offset.y - ground);
            }
        }
        if (kept.Count < 100 || maxRootGap > -.07f)
            throw new InvalidOperationException("Tree density or root grounding validation failed.");
        Undo.RecordObject(data, "Thin forest and ground roots");
        // Snapping would discard the root-collar and slope correction.
        data.SetTreeInstances(kept.ToArray(), false);
        EditorUtility.SetDirty(data);
        terrain.Flush();
        AssetDatabase.SaveAssets();
        ForestDemoSetup.Bake();
        string navigation = "Passed";
        try { ForestDemoSetup.Validate(); }
        catch (InvalidOperationException e) { navigation = e.Message; }
        ForestRefresh.Capture();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText(ReportPath, $"TREE_POLISH_OK\nTrees: {before} -> {kept.Count}\nMaximum root-band clearance: {maxRootGap:F3} m\nBackup: {backup}\nNavigation before tree edits: {baseline}\nNavigation after tree edits: {navigation}\n");
        Debug.Log(File.ReadAllText(ReportPath));
        if (prototypes.Any(p => !p.prefab.name.EndsWith("_Grounded"))) FixTerrainFloorClamping();
    }
}
