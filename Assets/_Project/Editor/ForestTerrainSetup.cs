using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ForestTerrainSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/SampleScene.unity";
    private const string Folder = "Assets/_Project/Environment";
    private const string PrefabFolder = Folder + "/Prefabs";
    private const string TerrainAsset = Folder + "/ForestTerrainData.asset";
    private const string TerrainMaterial = Folder + "/ForestTerrain.mat";
    private const string TreePrefabPath = PrefabFolder + "/LowPolyTree.prefab";
    private const string BushPrefabPath = PrefabFolder + "/LowPolyBush.prefab";

    [MenuItem("Tools/Forest/Build T3.1 Terrain + T3.2 Vegetation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building the forest terrain.");
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var terrain = FindTerrain(scene);
        if (terrain == null)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAsset);
            if (data == null)
            {
                data = new TerrainData { name = "ForestTerrainData" };
                AssetDatabase.CreateAsset(data, TerrainAsset);
            }
            ConfigureTerrain(data);
            terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.name = "Forest Terrain (T3.1)";
            terrain.transform.position = new Vector3(-40f, -0.15f, -40f);
            SceneManager.MoveGameObjectToScene(terrain.gameObject, scene);
        }
        else ConfigureTerrain(terrain.terrainData);
        terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterial);

        var tree = BuildTreePrefab();
        var bush = BuildBushPrefab();
        ConfigureVegetation(terrain.terrainData, tree, bush);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrain.gameObject;
        Debug.Log("T3.1/T3.2 complete: forest terrain, low-poly trees and bushes created.", terrain);
    }

    private static Terrain FindTerrain(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var terrain = root.GetComponentInChildren<Terrain>(true);
            if (terrain != null) return terrain;
        }
        return null;
    }

    private static void ConfigureTerrain(TerrainData data)
    {
        data.heightmapResolution = 257;
        data.size = new Vector3(80f, 5f, 80f);
        int resolution = data.heightmapResolution;
        var heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            float nx = x / (resolution - 1f);
            float nz = z / (resolution - 1f);
            float broad = Mathf.PerlinNoise(nx * 1.8f + 10f, nz * 1.8f + 4f) * .06f;
            float hillA = Mathf.Exp(-Mathf.Pow((nx - .22f) / .20f, 2f) - Mathf.Pow((nz - .72f) / .25f, 2f)) * .18f;
            float hillB = Mathf.Exp(-Mathf.Pow((nx - .78f) / .22f, 2f) - Mathf.Pow((nz - .28f) / .20f, 2f)) * .12f;
            float slope = (nx - .5f) * .025f + (nz - .5f) * .018f;
            heights[z, x] = Mathf.Clamp01(broad + hillA + hillB + slope + .015f);
        }
        data.SetHeights(0, 0, heights);
        var material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterial);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? Shader.Find("Nature/Terrain/Standard");
            if (shader != null)
            {
                material = new Material(shader) { name = "ForestTerrain" };
                material.color = new Color(.16f, .22f, .12f);
                AssetDatabase.CreateAsset(material, TerrainMaterial);
            }
        }
        data.SetDetailResolution(1024, 32);
    }

    private static GameObject BuildTreePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
        if (existing != null) return existing;
        var root = new GameObject("LowPolyTree");
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk"; trunk.transform.SetParent(root.transform, false);
        trunk.transform.localPosition = new Vector3(0, 1.3f, 0); trunk.transform.localScale = new Vector3(.22f, 1.3f, .22f);
        trunk.GetComponent<Renderer>().sharedMaterial = MakeMaterial("TreeBark", new Color(.20f, .11f, .055f));
        var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Crown"; crown.transform.SetParent(root.transform, false);
        crown.transform.localPosition = new Vector3(0, 3.1f, 0); crown.transform.localScale = new Vector3(1.35f, 1.9f, 1.35f);
        crown.GetComponent<Renderer>().sharedMaterial = MakeMaterial("TreeLeaves", new Color(.08f, .23f, .09f));
        StripCollider(trunk); StripCollider(crown);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildBushPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BushPrefabPath);
        if (existing != null) return existing;
        var root = new GameObject("LowPolyBush");
        for (int i = 0; i < 3; i++)
        {
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaf.name = "Leaf" + i; leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3((i - 1) * .35f, .35f + (i % 2) * .15f, (i % 2) * .2f);
            leaf.transform.localScale = Vector3.one * (.55f + (i % 2) * .12f);
            leaf.GetComponent<Renderer>().sharedMaterial = MakeMaterial("BushLeaves", new Color(.12f, .30f, .10f));
            StripCollider(leaf);
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BushPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        return material;
    }

    private static void StripCollider(GameObject obj)
    {
        var collider = obj.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void ConfigureVegetation(TerrainData data, GameObject tree, GameObject bush)
    {
        data.treePrototypes = new[] { new TreePrototype { prefab = tree }, new TreePrototype { prefab = bush } };
        var instances = new List<TreeInstance>();
        for (int i = 0; i < 34; i++)
        {
            float angle = i * 2.39996f;
            float radius = .27f + (i % 7) * .075f;
            float x = .5f + Mathf.Cos(angle) * radius;
            float z = .5f + Mathf.Sin(angle) * radius;
            if (Mathf.Abs(x - .5f) < .12f && Mathf.Abs(z - .5f) < .18f) continue;
            instances.Add(new TreeInstance
            {
                prototypeIndex = i % 5 == 0 ? 1 : 0,
                position = new Vector3(Mathf.Clamp01(x), data.GetInterpolatedHeight(x, z) / data.size.y, Mathf.Clamp01(z)),
                widthScale = i % 5 == 0 ? .7f : .85f + (i % 3) * .14f,
                heightScale = i % 5 == 0 ? .65f : .85f + (i % 4) * .16f,
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        data.SetTreeInstances(instances.ToArray(), true);
    }
}
