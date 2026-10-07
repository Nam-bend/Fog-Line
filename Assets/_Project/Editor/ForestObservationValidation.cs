using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ForestObservationValidation
{
    private const string Key="Forest.ObservationValidation";
    private static IEnumerator checks;
    static ForestObservationValidation() { EditorApplication.update+=Tick; }
    public static void RunBatch()
    {
        // This test intentionally saves checkpoints. Run only in a separate test profile.
        if(Application.productName!="FPS_Validation") throw new InvalidOperationException("Use the isolated FPS_Validation project profile.");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/ForestDemo.unity");
        SessionState.SetBool(Key,true);
        SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false)) return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>150) throw new Exception("Observation test timeout.");
            if(!EditorApplication.isPlaying) return;
            var story=UnityEngine.Object.FindFirstObjectByType<ForestStoryDirector>();
            if(story==null) return;
            if(!string.IsNullOrEmpty(story.buildError)) throw new Exception(story.buildError);
            if(!story.Ready) return;
            if(checks==null) checks=Check(story);
            if(!checks.MoveNext()) Finish(true,"PASS: three interactions, three non-interactive observations, no decorative trail cubes, automatic shell progress, disk checkpoint scene reload preserving observations/gate/diagram, power, diagram pickup and automatic return blockage.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    private static IEnumerator Check(ForestStoryDirector story)
    {
        if(story.items.Count!=3 || story.observations.Count!=3) throw new Exception("Unexpected item/observation count.");
        if(GameObject.Find("Trail mark")!=null) throw new Exception("Decorative trail cubes remain.");
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        player.GetComponent<InputManager>().enabled=false;
        foreach(var creature in story.creatures) creature.gameObject.SetActive(false);
        var shell=story.observations.First(x=>x.id=="shell");
        MoveNear(player,story,shell);
        float until=Time.time+2;
        while(Time.time<until && !story.Has("shell")) yield return null;
        if(story.State.stage!=1) throw new Exception("Walking past the shell did not advance the story.");
        story.State=JsonUtility.FromJson<ForestStoryState>(JsonUtility.ToJson(story.State));
        until=Time.time+1;
        while(Time.time<until) yield return null;
        if(story.State.clues.Count(x=>x=="shell")!=1) throw new Exception("Observation duplicated after state restore.");
        story.Interact(story.Item("power"));
        until=Time.time+9;
        while(Time.time<until && story.State.stage<2) yield return null;
        if(story.State.stage!=2) throw new Exception("Power interaction blocked after automatic shell.");
        foreach(var creature in story.creatures) creature.gameObject.SetActive(false);
        var cloth=story.observations.First(x=>x.id=="cloth");
        MoveNear(player,story,cloth);
        until=Time.time+2;
        while(Time.time<until && !story.Has("cloth")) yield return null;
        if(!story.Has("cloth")) throw new Exception("Cabin remark was not reachable from the route.");
        story.Interact(story.Item("diagram"));
        typeof(ForestStoryDirector).GetMethod("CloseInspection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(story,null);
        if(story.State.stage!=3 || story.Item("diagram").gameObject.activeSelf) throw new Exception("Diagram was not collected once.");
        var previousStory=story;
        typeof(ForestStoryDirector).GetMethod("ReloadCheckpoint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(story,new object[]{true});
        yield return null;
        while(ForestStoryDirector.Instance==null || ForestStoryDirector.Instance==previousStory || !ForestStoryDirector.Instance.Ready) yield return null;
        story=ForestStoryDirector.Instance;
        player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        player.GetComponent<InputManager>().enabled=false;
        foreach(var creature in story.creatures) creature.gameObject.SetActive(false);
        if(story.State.stage!=3 || !story.Has("shell") || !story.Has("cloth") || !story.Has("diagram")
            || story.serviceGate.activeSelf || story.Item("diagram").gameObject.activeSelf || !story.landslide.activeSelf)
            throw new Exception("Disk checkpoint did not restore observations and world state.");
        if(story.State.CanObserve("shell") || story.State.CanObserve("cloth")) throw new Exception("Saved remarks can replay after scene reload.");
        MoveNear(player,story,story.observations.First(x=>x.id=="blockage"));
        until=Time.time+2;
        while(Time.time<until && !story.Has("blockage")) yield return null;
        if(!story.Has("blockage")) throw new Exception("Return blockage did not trigger automatically.");
        story.State.shadowSeen=true;
        if(!story.State.CanFinish) throw new Exception("Simplified item flow cannot finish.");
    }
    private static void MoveNear(PlayerHealth player,ForestStoryDirector story,ForestObservation observation)
    {
        if(observation.marker.GetComponent<ForestStoryItem>()!=null) throw new Exception("Observation still has an interaction prompt.");
        var cc=player.GetComponent<CharacterController>();
        var camera=player.GetComponentInChildren<Camera>();
        Vector3 eyeOffset=camera.transform.position-player.transform.position;
        string approaches="";
        foreach(var point in story.outward.Concat(story.homeward).OrderBy(p=>(p-observation.marker.position).sqrMagnitude))
        {
            Vector3 position=point+Vector3.up*(cc.height*.5f-cc.center.y+.12f);
            if(Vector3.Distance(position,observation.marker.position)>observation.radius) continue;
            if(Physics.Linecast(position+eyeOffset,observation.marker.position,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                && hit.transform!=observation.marker) { approaches+=" ["+point+": "+hit.transform.name+"]"; continue; }
            cc.enabled=false; player.transform.position=position; cc.enabled=true; Physics.SyncTransforms(); return;
        }
        throw new Exception("No visible approach to observation: "+observation.id+" at "+observation.marker.position+" eye offset "+eyeOffset+approaches);
    }
    private static void Finish(bool success,string report)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ForestObservationValidation.txt",report);
        SessionState.SetBool(Key,false); checks=null;
        if(success) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
