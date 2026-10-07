using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Runs actual terrain and scene checks in Play Mode once a valid Editor license is available.
[InitializeOnLoad]
public static class ForestStoryPlayValidation
{
    private const string Active="Blackpine.PlayValidation";
    private const string Batch="Blackpine.PlayValidation.Batch";
    static ForestStoryPlayValidation() { EditorApplication.update+=Tick; }

    [MenuItem("Tools/Forest/Blackpine/Run Play Mode Placement Checks")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Begin(false);
    }
    public static void RunBatch() => Begin(true);
    private static void Begin(bool batch)
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/ForestDemo.unity");
        SessionState.SetBool(Active,true); SessionState.SetBool(Batch,batch);
        SessionState.SetFloat(Active+"Start",(float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)) return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Active+"Start",0)>180)
                throw new InvalidOperationException("Story scene did not become ready in 180 seconds.");
            if(!EditorApplication.isPlaying) return;
            var story=UnityEngine.Object.FindFirstObjectByType<ForestStoryDirector>();
            if(story==null) return;
            if(!string.IsNullOrEmpty(story.buildError)) throw new InvalidOperationException(story.buildError);
            if(!story.Ready) return;
            ForestStorySetup.ValidateGrounding();
            string[] requiredItems={"briggs","power","diagram"};
            if(story.items.Count!=3) throw new InvalidOperationException("Only three deliberate interactions should remain.");
            foreach(string id in new[]{"shell","cloth","blockage"})
                if(story.observations.Count(x=>x.id==id && x.marker!=null && x.marker.GetComponent<ForestStoryItem>()==null)!=1)
                    throw new InvalidOperationException("Missing non-interactive observation: "+id);
            foreach(string id in requiredItems)
                if(story.items.Count(item=>item!=null && item.id==id)!=1)
                    throw new InvalidOperationException("Expected exactly one story item: "+id);
            if(story.creatures.Count!=3 || story.creatures.Any(creature=>creature==null))
                throw new InvalidOperationException("Missing story encounters.");
            foreach(var item in story.items)
            {
                if(item.GetComponent<BoxCollider>()==null || item.transform.localScale!=Vector3.one*.8f)
                    throw new InvalidOperationException("Inconsistent story placeholder: "+item.name);
                if(string.IsNullOrEmpty(item.promptMessage)) throw new InvalidOperationException("Missing prompt: "+item.name);
            }
            var filter=new NavMeshQueryFilter { agentTypeID=0,areaMask=NavMesh.AllAreas };
            foreach(var creature in story.creatures)
            {
                if(!NavMesh.SamplePosition(creature.transform.position,out var p,1,filter))
                    throw new InvalidOperationException("Enemy is off the NavMesh: "+creature.name);
                if(Mathf.Abs(p.position.y-ForestGrounding.Height(p.position))>.6f)
                    throw new InvalidOperationException("Enemy floor mismatch: "+creature.name);
                if(creature.powerWitness)
                {
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(p.position,creature.retreatPosition,filter,path) || path.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("Vibration retreat path disconnected.");
                }
            }
            Finish(true,"PASS: scene setup, grounded footprints, consistent interaction cubes, enemy floor and vibration retreat navigation. Full first-person playthrough remains a separate check.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    private static void Finish(bool success,string report)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/BlackpinePlayValidation.txt",report);
        SessionState.SetBool(Active,false);
        if(success) Debug.Log(report); else Debug.LogError(report);
        if(SessionState.GetBool(Batch,false)) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
