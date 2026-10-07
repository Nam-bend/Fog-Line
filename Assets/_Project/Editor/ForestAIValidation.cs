using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class ForestAIValidation
{
    private const string Key="Forest.AIValidation";
    private static IEnumerator checks;
    static ForestAIValidation() { EditorApplication.update+=Tick; }
    public static void RunBatch()
    {
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
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>150)
                throw new Exception("AI validation timeout.");
            if(!EditorApplication.isPlaying) return;
            var story=UnityEngine.Object.FindFirstObjectByType<ForestStoryDirector>();
            if(story==null) return;
            if(!string.IsNullOrEmpty(story.buildError)) throw new Exception(story.buildError);
            if(!story.Ready) return;
            if(checks==null) checks=Check(story);
            if(!checks.MoveNext()) Finish(true,"PASS: nearby rear player ignored, cover blocks sight, sustained front sight triggers chase, noise triggers search, and actual terrain route traversed without teleporting.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    private static IEnumerator Check(ForestStoryDirector story)
    {
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        player.GetComponent<InputManager>().enabled=false;
        var controller=player.GetComponent<CharacterController>();
        var creature=story.creatures.First(x=>!x.returnOnly && !x.powerWitness);
        foreach(var other in story.creatures) if(other!=creature) other.gameObject.SetActive(false);
        var agent=creature.GetComponent<NavMeshAgent>();
        var navigation=creature.GetComponent<EnemyNavigation>();
        var combat=creature.GetComponent<EnemyAI>();
        Vector3 origin=creature.transform.position;
        Vector3 forward=creature.transform.forward;
        Action<Vector3> place=p=>{ controller.enabled=false; player.transform.position=p; controller.enabled=true; Physics.SyncTransforms(); };
        // Close enough to exercise the old omnidirectional 2m shortcut.
        place(origin-forward*1.8f);
        float until=Time.time+1.2f;
        while(Time.time<until) yield return null;
        if(creature.Perception=="Chasing") throw new Exception("Stationary player behind creature was detected.");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name="AI validation sight blocker";
        wall.transform.position=origin+forward*1.4f+Vector3.up;
        wall.transform.rotation=Quaternion.LookRotation(forward);
        wall.transform.localScale=new Vector3(4,5,.3f);
        place(origin+forward*3f);
        until=Time.time+1.2f;
        while(Time.time<until) yield return null;
        if(creature.Perception=="Chasing") throw new Exception("Player detected through cover.");
        wall.SetActive(false); Physics.SyncTransforms();
        until=Time.time+1f;
        while(Time.time<until && creature.Perception!="Chasing") yield return null;
        if(creature.Perception!="Chasing") throw new Exception("Visible player did not trigger chase.");
        place(origin+Vector3.up*50);
        until=Time.time+1f;
        while(Time.time<until) yield return null;
        ForestNoise.Emit(origin+forward*4,20);
        until=Time.time+.25f;
        while(Time.time<until) yield return null;
        if(creature.Perception!="Searching") throw new Exception("Noise failed to start investigation: "+creature.Perception);
        creature.enabled=false; combat.enabled=false; navigation.Stop();
        var path=new NavMeshPath();
        var filter=new NavMeshQueryFilter {agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        Vector3 target=Vector3.zero; bool found=false;
        foreach(var point in story.outward)
        {
            float distance=Vector3.Distance(agent.transform.position,point);
            if(distance<7 || distance>13) continue;
            if(NavMesh.SamplePosition(point,out var hit,1f,filter) && agent.CalculatePath(hit.position,path) && path.status==NavMeshPathStatus.PathComplete)
            { target=hit.position; found=true; break; }
        }
        if(!found) throw new Exception("No connected terrain test destination.");
        // Simulate being held by another actor, then release and verify recovery.
        float speed=agent.speed; agent.speed=0;
        until=Time.time+2f;
        while(Time.time<until) { navigation.Move(target,.35f); yield return null; }
        agent.speed=speed;
        until=Time.time+18f;
        while(Time.time<until && Vector3.Distance(creature.transform.position,target)>.8f)
        { navigation.Move(target,.35f); yield return null; }
        if(Vector3.Distance(creature.transform.position,target)>.8f)
            throw new Exception("Creature did not reach terrain target; remaining="+Vector3.Distance(creature.transform.position,target));
        navigation.Stop();
        if(navigation.Moving) throw new Exception("Navigation still moving after stop.");
        if(navigation.Move(creature.transform.position+Vector3.up*40,.35f))
            throw new Exception("Accepted an unreachable target far above the terrain.");
    }
    private static void Finish(bool success,string report)
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ForestAIValidation.txt",report);
        SessionState.SetBool(Key,false); checks=null;
        if(success) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
