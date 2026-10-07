using System;
using System.Collections.Generic;
using UnityEngine;

// A* on the actual terrain. No heightmap editing and no guessed world-space Y values.
public sealed class ForestTerrainRoute
{
    private const float Step=2f;
    private readonly int width, depth;
    private readonly Vector3[] points;
    private readonly bool[] clear;
    private readonly float[] penalty;
    private readonly Transform player;
    public ForestTerrainRoute(Terrain terrain,Transform actor)
    {
        player=actor;
        width=Mathf.CeilToInt(terrain.terrainData.size.x/Step)-1;
        depth=Mathf.CeilToInt(terrain.terrainData.size.z/Step)-1;
        points=new Vector3[width*depth]; clear=new bool[points.Length]; penalty=new float[points.Length];
        Physics.SyncTransforms();
        for(int z=0;z<depth;z++) for(int x=0;x<width;x++)
        {
            int i=z*width+x; Vector3 p=terrain.transform.position+new Vector3((x+1)*Step,0,(z+1)*Step);
            try
            {
                p.y=ForestGrounding.Height(p); points[i]=p;
                clear[i]=terrain.terrainData.GetSteepness((p.x-terrain.transform.position.x)/terrain.terrainData.size.x,
                    (p.z-terrain.transform.position.z)/terrain.terrainData.size.z)<36 && HasClearance(p);
            }
            catch(InvalidOperationException) { clear[i]=false; }
        }
    }
    public bool HasClearance(Vector3 p,float radius=.65f)
    {
        foreach(var collider in Physics.OverlapCapsule(p+Vector3.up*(radius+.12f),p+Vector3.up*(1.8f-radius),radius,
            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
        {
            if(collider is TerrainCollider || collider.transform.IsChildOf(player)
                || collider.GetComponentInParent<EnemyAI>()!=null || collider.GetComponentInParent<MotherAI>()!=null) continue;
            return false;
        }
        return true;
    }
    private int Nearest(Vector3 p, bool flat=false)
    {
        int best=-1; float distance=float.PositiveInfinity;
        for(int i=0;i<points.Length;i++)
        {
            if(!clear[i]) continue;
            float d=(points[i]-p).sqrMagnitude;
            if(d>=distance) continue;
            if(flat && Relief(points[i],.45f)>.32f) continue;
            best=i; distance=d;
        }
        if(best<0 || distance>625) throw new InvalidOperationException("No clear terrain placement within 25m of "+p);
        return best;
    }
    public Vector3 SafePoint(Vector3 p,bool flat=true) => points[Nearest(p,flat)];
    public static float Relief(Vector3 p,float radius)
    {
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
        {
            float y=ForestGrounding.Height(p+new Vector3(x*radius,0,z*radius)); min=Mathf.Min(min,y); max=Mathf.Max(max,y);
        }
        return max-min;
    }
    public List<Vector3> Find(Vector3 from,Vector3 to)
    {
        int start=Nearest(from),end=Nearest(to);
        float[] costs=new float[points.Length]; int[] parent=new int[points.Length]; bool[] closed=new bool[points.Length];
        for(int i=0;i<costs.Length;i++) { costs[i]=float.PositiveInfinity; parent[i]=-1; }
        var open=new SortedSet<KeyValuePair<float,int>>(Comparer<KeyValuePair<float,int>>.Create((a,b)=>
        {int c=a.Key.CompareTo(b.Key); return c!=0 ? c : a.Value.CompareTo(b.Value);}));
        costs[start]=0; open.Add(new KeyValuePair<float,int>(0,start));
        while(open.Count>0)
        {
            var first=open.Min; open.Remove(first); int current=first.Value;
            if(closed[current]) continue; if(current==end) break; closed[current]=true;
            int cx=current%width,cz=current/width;
            for(int dz=-1;dz<=1;dz++) for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0 && dz==0) continue;
                int x=cx+dx,z=cz+dz; if(x<0||z<0||x>=width||z>=depth) continue;
                int next=z*width+x; if(!clear[next]||closed[next]) continue;
                if(dx!=0 && dz!=0 && (!clear[cz*width+x]||!clear[z*width+cx])) continue;
                Vector3 delta=points[next]-points[current]; float flat=new Vector2(delta.x,delta.z).magnitude;
                if(Mathf.Abs(delta.y)>flat*.7f) continue;
                Vector3 midpoint=(points[next]+points[current])*.5f;
                midpoint.y=ForestGrounding.Height(midpoint);
                if(Mathf.Abs(midpoint.y-(points[current].y+points[next].y)*.5f)>.45f || !HasClearance(midpoint,.55f)) continue;
                float cost=costs[current]+delta.magnitude+Mathf.Abs(delta.y)*3+penalty[next];
                if(cost>=costs[next]) continue;
                costs[next]=cost; parent[next]=current;
                open.Add(new KeyValuePair<float,int>(cost+Vector3.Distance(points[next],points[end]),next));
            }
        }
        if(start!=end && parent[end]<0) throw new InvalidOperationException("Terrain route disconnected: "+from+" -> "+to);
        var result=new List<Vector3>();
        for(int i=end;i>=0;i=parent[i]) { result.Add(points[i]); if(i==start) break; }
        result.Reverse(); return result;
    }
    public void Avoid(List<Vector3> route)
    {
        for(int i=0;i<points.Length;i++) foreach(var p in route)
            if(Vector3.ProjectOnPlane(points[i]-p,Vector3.up).sqrMagnitude<36) { penalty[i]=18; break; }
    }
}
