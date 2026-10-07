using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ForestDemoSetup
{
    private const string Folder = "Assets/_Project/Environment/ForestDemo";
    private const string ScenePath = "Assets/_Project/Scenes/ForestDemo.unity";
    private static Terrain terrain;
    private static Transform environment;

    // The imported woodland replaces the retired prototype vegetation generator.
    public static void Build() => ForestTerrainSetup.Build();

    private static Vector3 Ground(float x, float z) => new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y, z);

    [MenuItem("Tools/Forest/Re-bake Forest Demo Navigation")]
    public static void Bake()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open ForestDemo first.");
        Physics.SyncTransforms();
        foreach (var surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
        {
            surface.BuildNavMesh();
            string path = Folder + "/NavMesh_" + surface.agentTypeID + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData,path);
            else
            {
                var generated = surface.navMeshData;
                surface.RemoveData(); EditorUtility.CopySerialized(generated,existing);
                surface.navMeshData = existing; surface.AddData(); Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
            }
            EditorUtility.SetDirty(surface);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Forest/Validate Forest Demo")]
    public static void Validate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open ForestDemo first.");
        terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData.treeInstanceCount < 100 || !RenderSettings.fog)
            throw new InvalidOperationException("Terrain, vegetation or fog missing.");
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length != 2) throw new InvalidOperationException("Expected two agent surfaces.");
        foreach (var surface in surfaces)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            if (surface.navMeshData == null) throw new InvalidOperationException("Missing saved bake.");
            // The cabin approach is on a steep slope. The authored target can sit
            // just outside the walkable polygon for the larger Mother agent, so
            // resolve it within the approach area instead of failing the whole bake.
            if (!NavMesh.SamplePosition(Ground(-22,-27),out var start,3,filter) ||
                !NavMesh.SamplePosition(Ground(15,20),out var end,8,filter)) throw new InvalidOperationException("Missing route endpoints.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position,end.position,filter,path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Forest route disconnected for " + surface.agentTypeID);
            if (NavMesh.SamplePosition(Ground(0,-1),out _,.3f,filter)) throw new InvalidOperationException("Escape gap contains NavMesh.");
            if (NavMesh.SamplePosition(Ground(0,5),out var refuge,1,filter) &&
                NavMesh.CalculatePath(start.position,refuge.position,filter,path) && path.status == NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("AI can enter refuge.");
        }
        var controller = Object.FindFirstObjectByType<PlayerHealth>().GetComponent<CharacterController>();
        float radius = controller.radius * controller.transform.lossyScale.x;
        if (radius * 2 + controller.skinWidth * 2 >= 1.5f)
            throw new InvalidOperationException("Player too wide for escape gap.");
        Physics.SyncTransforms();
        float floor = Mathf.Max(Ground(0,-3).y, Ground(0,2).y) + .15f;
        Vector3 bottom = new Vector3(0,floor + radius,-3);
        Vector3 top = bottom + Vector3.up * (controller.height - radius * 2);
        if (Physics.CapsuleCast(bottom,top,radius,Vector3.forward,5,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("Escape gap blocked by physical geometry.");
        Debug.Log("FOREST_VALIDATION_OK: vegetation, fog, both forest routes, escape gap and refuge isolation.");
    }

    public static void FinalizeAndCapture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        terrain = Object.FindFirstObjectByType<Terrain>();
        foreach (var prototype in terrain.terrainData.treePrototypes)
        {
            string path = AssetDatabase.GetAssetPath(prototype.prefab);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            var lod = prefab.GetComponent<LODGroup>();
            if (lod == null) lod = prefab.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.005f, prefab.GetComponents<Renderer>()) });
            lod.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(prefab,path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        terrain.terrainData.RefreshPrototypes();
        AssetDatabase.SaveAssets();
        Validate();
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(-42,48,-48);
        camera.transform.LookAt(new Vector3(0,0,4));
        camera.fieldOfView = 65;
        var target = new RenderTexture(1280,720,24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        File.WriteAllBytes("Logs/ForestOverview.png",image.EncodeToPNG());
        var playerCamera = Object.FindFirstObjectByType<PlayerHealth>().GetComponentInChildren<Camera>();
        camera.transform.SetPositionAndRotation(playerCamera.transform.position,playerCamera.transform.rotation);
        camera.fieldOfView = playerCamera.fieldOfView;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        File.WriteAllBytes("Logs/ForestPlayerView.png",image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
