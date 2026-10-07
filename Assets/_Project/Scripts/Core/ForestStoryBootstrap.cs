using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Attached only to ForestDemo. Builds a reversible greybox layer over the user's terrain.
[DefaultExecutionOrder(-200)]
public sealed class ForestStoryBootstrap : MonoBehaviour
{
    public ForestStoryDirector authoredStory;
    public Transform authoredContent;
    private ForestStoryDirector story;
    private PlayerHealth player;
    private GameObject originalEnemy;
    private ForestTerrainRoute routes;
    private Transform content;
    private Material propMaterial, darkMaterial;
    private readonly List<Material> materials = new List<Material>();
    private readonly List<GameObject> generated = new List<GameObject>();
    private readonly List<string> report = new List<string>();

    private void Awake()
    {
        Prepare();
    }
    private void Prepare()
    {
        // Disable the sandbox's direct-spawn combat and old end trigger before their Start.
        foreach(var spawner in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) spawner.enabled=false;
        foreach(var end in FindObjectsByType<ForestDestination>(FindObjectsSortMode.None)) end.enabled=false;
        foreach(var mother in FindObjectsByType<MotherAI>(FindObjectsSortMode.None)) mother.gameObject.SetActive(false);
        originalEnemy=GameObject.Find("Enemy - Melee AI");
        if(originalEnemy!=null) originalEnemy.SetActive(false);
        player=FindFirstObjectByType<PlayerHealth>();
        story=authoredStory != null ? authoredStory : gameObject.AddComponent<ForestStoryDirector>();
    }
    private IEnumerator Start()
    {
        // Yield once for the existing player's weapon/health Awake and Start initialization.
        yield return null;
        try
        {
            if(authoredStory!=null && authoredContent!=null)
            {
                content=authoredContent;
                foreach(var grounding in content.GetComponentsInChildren<ForestGrounding>(true))
                {
                    grounding.Place(); generated.Add(grounding.gameObject);
                }
                story.Initialize(player); RebuildNavigation();
                foreach(var creature in story.creatures)
                {
                    if(creature==null) continue;
                    Vector3 p=creature.transform.position; p.y=ForestGrounding.Height(p);
                    if(!NavMesh.SamplePosition(p,out var hit,3,new NavMeshQueryFilter {agentTypeID=0,areaMask=NavMesh.AllAreas}))
                        throw new InvalidOperationException("Authored creature has no walkable terrain: "+creature.name);
                    creature.GetComponent<NavMeshAgent>().Warp(hit.position);
                }
                Validate(); story.Begin();
            }
            else Build();
        }
        catch(Exception e)
        {
            story.buildError=e.Message; Debug.LogException(e); Cursor.visible=true; Cursor.lockState=CursorLockMode.None;
            WriteReport("FAILED: "+e);
        }
    }
    public void BuildEditorPreview()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring the preview.");
        if(authoredContent!=null) throw new InvalidOperationException("A story preview is already authored. Edit it directly rather than duplicating it.");
        Prepare(); Build(); authoredStory=story; authoredContent=content;
    }
    private void Build()
    {
        if(player==null) throw new InvalidOperationException("ForestDemo has no player.");
        if(Application.isPlaying) story.Initialize(player);
        content=new GameObject("Blackpine Story - Generated Greybox").transform;
        propMaterial=Material("Shared story cube",new Color(.6f,.48f,.24f));
        darkMaterial=Material("Passing shadow",new Color(.018f,.025f,.03f));
        var terrain=ForestGrounding.TerrainAt(player.transform.position);
        routes=new ForestTerrainRoute(terrain,player.transform);
        var oldDestination=FindFirstObjectByType<ForestDestination>(FindObjectsInactive.Include);
        Vector3 desiredCabin=oldDestination!=null ? oldDestination.transform.position : new Vector3(23,0,26);
        desiredCabin.y=ForestGrounding.Height(desiredCabin);
        Vector3 start=routes.SafePoint(player.transform.position,false), cabin=routes.SafePoint(desiredCabin,false);
        story.outward=routes.Find(start,cabin);
        if(story.outward.Count<16) throw new InvalidOperationException("Outbound route too short to separate story encounters.");
        routes.Avoid(story.outward);
        Vector3 south=routes.SafePoint((start+cabin)*.5f+new Vector3(15,0,-16),false);
        story.homeward=routes.Find(cabin,south);
        Append(story.homeward,routes.Find(south,start));

        AddItem("briggs","Briggs — Trạm gác",start+Side(story.outward,0)*2);
        AddObservation("shell","Vỏ đạn đã bắn",At(story.outward,.2f)+Side(story.outward,.2f)*2,new Vector3(.25f,.08f,.25f));
        AddItem("power","Cầu dao — Thử nền",At(story.outward,.61f)+Side(story.outward,.61f)*2);
        story.PowerPoint=story.Item("power").transform.position;
        var rig=story.Item("power").gameObject;
        story.powerIndicator=rig.AddComponent<Light>(); story.powerIndicator.range=7;
        story.powerIndicator.color=new Color(.6f,1,.45f); story.powerIndicator.intensity=2; story.powerIndicator.enabled=false;
        story.serviceGate=Cube("Service gate",At(story.outward,.69f),new Vector3(.8f,1.8f,.8f),propMaterial);
        AddItem("diagram","Sơ đồ G-07",cabin);
        AddObservation("cloth","Mảnh vải ở lán",cabin+Vector3.left*2,new Vector3(.4f,.05f,.3f));
        story.landslide=Cube("Landslide temporary fallen trunk",At(story.outward,.88f),new Vector3(.8f,1.2f,.8f),propMaterial);
        story.observations.Add(new ForestObservation {id="blockage",marker=story.landslide.transform,radius=6f});
        story.landslide.SetActive(false);
        // Simple repeatable cover and a visible alternative line of passage around each encounter.
        Vector3 encounter=At(story.outward,.4f);
        Cover(encounter,Side(story.outward,.4f));
        Cover(At(story.homeward,.72f),Side(story.homeward,.72f));
        BuildShadow();
        RebuildNavigation();
        Spawn("first",encounter+Side(story.outward,.4f)*3,false,false);
        Spawn("witness",story.PowerPoint+Side(story.outward,.61f)*4,false,true);
        Spawn("return",At(story.homeward,.72f)+Side(story.homeward,.72f)*3,true,false);

        // Correct the player feet rather than its pivot, which has an offset in the existing prefab.
        var cc=player.GetComponent<CharacterController>();
        cc.enabled=false;
        player.transform.position=start+Vector3.up*(cc.height*.5f-cc.center.y+.12f);
        player.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(story.outward[1]-start,Vector3.up));
        cc.enabled=true;
        Physics.SyncTransforms(); Validate();
        if(Application.isPlaying) story.Begin();
        WriteReport("PASS: map generated; runtime Play validation still required for complete encounters.");
    }
    private static void Append(List<Vector3> target,List<Vector3> other) { for(int i=1;i<other.Count;i++) target.Add(other[i]); }
    private static Vector3 At(List<Vector3> path,float fraction) => path[Mathf.Clamp(Mathf.RoundToInt((path.Count-1)*fraction),0,path.Count-1)];
    private static Vector3 Side(List<Vector3> path,float fraction)
    {
        int i=Mathf.Clamp(Mathf.RoundToInt((path.Count-1)*fraction),0,path.Count-2);
        return Vector3.Cross(Vector3.up,(path[i+1]-path[i]).normalized).normalized;
    }
    private Vector3 FindFree(Vector3 preferred,bool nearTrail=false,Vector3? observationSize=null)
    {
        // Recheck after each created prop so successive cubes cannot stack inside one another.
        for(int ring=0;ring<8;ring++) for(int a=0;a<12;a++)
        {
            Vector3 p=preferred+Quaternion.Euler(0,a*30,0)*Vector3.forward*ring*1.2f;
            try
            {
                p.y=ForestGrounding.Height(p);
                if(ForestTerrainRoute.Relief(p,.45f)>.32f || !routes.HasClearance(p,.65f)) continue;
                // Keep the walking centerline free when placing interaction cubes.
                float distance=float.PositiveInfinity;
                foreach(var path in new[] { story.outward,story.homeward,story.optional })
                    foreach(var q in path) distance=Mathf.Min(distance,Vector3.ProjectOnPlane(q-p,Vector3.up).sqrMagnitude);
                if(nearTrail && (distance<1.44f || distance>12.25f)) continue;
                if(!nearTrail && distance<1.1f && ring<2) continue;
                if(observationSize.HasValue && !VisibleFromApproach(p,observationSize.Value)) continue;
                return p;
            }
            catch(InvalidOperationException) { }
        }
        throw new InvalidOperationException("No flat, unoccupied prop footprint near "+preferred);
    }
    private bool VisibleFromApproach(Vector3 ground,Vector3 size)
    {
        // Match the grounded prop's centre, including terrain relief under its footprint.
        float highest=float.NegativeInfinity;
        for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
            highest=Mathf.Max(highest,ForestGrounding.Height(ground+new Vector3(x*size.x*.5f,0,z*size.z*.5f)));
        Vector3 target=new Vector3(ground.x,highest+.025f+size.y*.5f,ground.z);
        var cc=player.GetComponent<CharacterController>();
        Vector3 eyeOffset=player.GetComponentInChildren<Camera>().transform.position-player.transform.position;
        foreach(var point in story.outward)
        {
            Vector3 position=point+Vector3.up*(cc.height*.5f-cc.center.y+.12f);
            if(Vector3.Distance(position,target)>5f) continue;
            if(!Physics.Linecast(position+eyeOffset,target,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }
    private ForestStoryItem AddItem(string id,string title,Vector3 preferred)
    {
        Vector3 p=FindFree(preferred,true);
        var cube=Cube(title,p,Vector3.one*.8f,propMaterial); cube.layer=LayerMask.NameToLayer("Interactable");
        var item=cube.AddComponent<ForestStoryItem>(); item.id=id; item.title=title;
        item.promptMessage="[E] "+title; item.groundPoint=p; story.items.Add(item); return item;
    }
    private void AddObservation(string id,string title,Vector3 preferred,Vector3 size)
    {
        var prop=Cube(title,FindFree(preferred,true,size),size,propMaterial);
        story.observations.Add(new ForestObservation {id=id,marker=prop.transform,radius=5f});
    }
    private GameObject Cube(string name,Vector3 p,Vector3 size,Material material)
    {
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name=name; cube.transform.SetParent(content,false);
        cube.transform.position=p; cube.transform.localScale=size; cube.GetComponent<Renderer>().sharedMaterial=material;
        cube.AddComponent<ForestGrounding>().Place(); Physics.SyncTransforms(); generated.Add(cube); return cube;
    }
    private void Cover(Vector3 center,Vector3 side)
    {
        for(int i=0;i<2;i++) Cube("Encounter cover",FindFree(center+side*(i==0 ? -3 : 6)),new Vector3(.8f,2f,.8f),propMaterial);
    }
    private void BuildShadow()
    {
        Vector3 midpoint=At(story.homeward,.5f),side=Side(story.homeward,.5f);
        var wall=Cube("Rock face for passing silhouette",FindFree(midpoint+side*5),new Vector3(.8f,5,.8f),propMaterial);
        var shade=GameObject.CreatePrimitive(PrimitiveType.Cube); shade.name="Mother passing shadow - temporary silhouette";
        shade.transform.SetParent(content); shade.transform.localScale=new Vector3(.85f,3f,.12f);
        shade.GetComponent<Collider>().enabled=false; shade.GetComponent<Renderer>().sharedMaterial=darkMaterial;
        story.motherSilhouette=shade;
        story.shadowStart=new GameObject("Shadow start").transform; story.shadowStart.SetParent(content);
        story.shadowEnd=new GameObject("Shadow end").transform; story.shadowEnd.SetParent(content);
        story.shadowStart.position=wall.transform.position+new Vector3(-.3f,.3f,-.48f);
        story.shadowEnd.position=wall.transform.position+new Vector3(.3f,.6f,-.48f);
        shade.SetActive(false);
    }
    private void RebuildNavigation()
    {
        // Existing terrain and rocks participate; player and story cubes use their real colliders.
        foreach(var surface in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
        {
            if(surface.agentTypeID!=0) continue;
            surface.collectObjects=CollectObjects.All;
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            // Preserve terrain height detail so agents follow sculpted slopes accurately.
            surface.buildHeightMesh=true;
            Physics.SyncTransforms(); surface.BuildNavMesh();
        }
    }
    private void Spawn(string id,Vector3 preferred,bool returnOnly,bool witness)
    {
        Vector3 point=FindFree(preferred);
        if(!NavMesh.SamplePosition(point,out var hit,3,new NavMeshQueryFilter { agentTypeID=0,areaMask=NavMesh.AllAreas }))
            throw new InvalidOperationException("No enemy NavMesh at "+id+" "+point);
        GameObject enemy;
        if(originalEnemy!=null) enemy=Instantiate(originalEnemy,content);
        else { enemy=GameObject.CreatePrimitive(PrimitiveType.Cube); enemy.SetActive(false); enemy.transform.SetParent(content); }
        enemy.name="Blackpine creature - "+id; enemy.transform.position=hit.position;
        var agent=enemy.GetComponent<NavMeshAgent>(); if(agent==null) agent=enemy.AddComponent<NavMeshAgent>();
        agent.agentTypeID=0; agent.height=2; agent.radius=.4f; agent.speed=2.8f; agent.acceleration=10;
        if(enemy.GetComponent<EnemyHealth>()==null) enemy.AddComponent<EnemyHealth>();
        if(enemy.GetComponent<EnemyAI>()==null) enemy.AddComponent<EnemyAI>();
        var creature=enemy.AddComponent<ForestCreature>(); creature.storyId=id; creature.returnOnly=returnOnly; creature.powerWitness=witness;
        // Face away from the approach: observing before detection is possible.
        enemy.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(hit.position-story.outward[0],Vector3.up).normalized);
        enemy.SetActive(true); agent.Warp(hit.position);
        Vector3 retreat=routes.SafePoint(point+Vector3.ProjectOnPlane(point-story.PowerPoint,Vector3.up).normalized*12,false);
        if(NavMesh.SamplePosition(retreat,out var back,4,new NavMeshQueryFilter { agentTypeID=0,areaMask=NavMesh.AllAreas })) creature.retreatPosition=back.position;
        else creature.retreatPosition=hit.position;
        story.creatures.Add(creature);
    }
    private void Validate()
    {
        foreach(var prop in generated)
        {
            var grounding=prop.GetComponent<ForestGrounding>();
            if(!grounding.Validate(out string result)) throw new InvalidOperationException(result);
            if(prop.GetComponent<ForestStoryItem>()!=null && grounding.maximumCornerGap>.35f)
                throw new InvalidOperationException("Interaction cube needs flatter footing: "+prop.name);
            report.Add(result);
        }
        foreach(var item in story.items)
        {
            if(item.transform.localScale!=Vector3.one*.8f) throw new InvalidOperationException("Inconsistent placeholder shape: "+item.name);
            bool reachable=false;
            foreach(var p in story.outward) if(Vector3.Distance(p,item.groundPoint)<5) { reachable=true; break; }
            if(!reachable) foreach(var p in story.optional) if(Vector3.Distance(p,item.groundPoint)<5) { reachable=true; break; }
            if(!reachable) foreach(var p in story.homeward) if(Vector3.Distance(p,item.groundPoint)<5) { reachable=true; break; }
            if(!reachable) throw new InvalidOperationException("Interaction farther than reach from trail: "+item.name);
        }
        report.Add("Routes: outbound="+story.outward.Count+", return="+story.homeward.Count+", optional="+story.optional.Count);
        report.Add("Story cubes: "+story.items.Count+". All 0.8m cubes, sampled at 9 footprint points.");
    }
    private void WriteReport(string result)
    {
        var text=new StringBuilder(result+"\n"); foreach(var line in report) text.AppendLine(line);
        Debug.Log(text.ToString());
        try { File.WriteAllText(Path.Combine(Application.persistentDataPath,"blackpine-placement-report.txt"),text.ToString()); }
        catch(Exception e) { Debug.LogWarning("Cannot save placement report: "+e.Message); }
    }
    private Material Material(string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material=new Material(shader) { name=name,color=color }; materials.Add(material); return material;
    }
    private void OnDestroy()
    {
        if(!Application.isPlaying) return;
        foreach(var material in materials) if(material!=null) Destroy(material);
        if(Application.isPlaying && authoredContent==null && content!=null) Destroy(content.gameObject);
    }
}
