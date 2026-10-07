using System;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForestStorySetup
{
    private const string ScenePath="Assets/_Project/Scenes/ForestDemo.unity";
    private const string Folder="Assets/_Project/Environment/BlackpineStory";

    [MenuItem("Tools/Forest/Blackpine/Build Editable Story Preview")]
    public static void BuildPreview()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath) throw new InvalidOperationException("Open ForestDemo first.");
        Directory.CreateDirectory("ArtSource/ForestBackups");
        string backup="ArtSource/ForestBackups/ForestBeforeStoryPreview_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity";
        EditorSceneManager.SaveScene(scene,backup,true);
        var bootstrap=UnityEngine.Object.FindFirstObjectByType<ForestStoryBootstrap>();
        if(bootstrap==null) bootstrap=new GameObject("Blackpine Story - Demo 3A-3F").AddComponent<ForestStoryBootstrap>();
        bootstrap.BuildEditorPreview();
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        foreach(var material in bootstrap.authoredContent.GetComponentsInChildren<Renderer>(true).Select(x=>x.sharedMaterial).Distinct())
        {
            if(material==null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))) continue;
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+material.name+".mat"));
        }
        foreach(var surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            if(surface.navMeshData!=null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(surface.navMeshData)))
                AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Folder+"/StoryNavigation.asset"));
        ValidateGrounding();
        EditorUtility.SetDirty(bootstrap); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Debug.Log("BLACKPINE_PREVIEW_SAVED. Recovery: "+backup);
    }

    [MenuItem("Tools/Forest/Blackpine/Validate Story Grounding")]
    public static void ValidateGrounding()
    {
        var props=UnityEngine.Object.FindObjectsByType<ForestGrounding>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(props.Length==0) throw new InvalidOperationException("Build the story preview or enter Play Mode first.");
        var report=new System.Text.StringBuilder();
        foreach(var prop in props)
        {
            if(!prop.Validate(out string result)) throw new InvalidOperationException(result);
            report.AppendLine(result);
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BlackpineGrounding.txt",report.ToString());
        Debug.Log("BLACKPINE_GROUNDING_OK: "+props.Length+" footprints checked.");
    }

    public static void BatchBuild()
    {
        EditorSceneManager.OpenScene(ScenePath); BuildPreview();
    }
}
