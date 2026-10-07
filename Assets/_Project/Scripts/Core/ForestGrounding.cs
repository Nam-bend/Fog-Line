using System;
using UnityEngine;

// Authored placements keep the sampled footprint so the editor can audit them later.
public sealed class ForestGrounding : MonoBehaviour
{
    public float clearance = .025f;
    public float maximumCornerGap;
    public static Terrain TerrainAt(Vector3 point)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 p=point-t.transform.position, size=t.terrainData.size;
            if(p.x>=0 && p.z>=0 && p.x<size.x && p.z<size.z) return t;
        }
        throw new InvalidOperationException("No terrain under " + point);
    }
    public static float Height(Vector3 p)
    {
        var t=TerrainAt(p); Vector3 local=p-t.transform.position; var data=t.terrainData;
        int x=Mathf.Clamp((int)(local.x/data.size.x*data.holesResolution),0,data.holesResolution-1);
        int z=Mathf.Clamp((int)(local.z/data.size.z*data.holesResolution),0,data.holesResolution-1);
        if(data.IsHole(x,z)) throw new InvalidOperationException("Terrain hole under " + p);
        return t.SampleHeight(p)+t.transform.position.y;
    }
    public void Place()
    {
        var box=GetComponent<BoxCollider>();
        if(box==null) throw new InvalidOperationException("Grounded story prop requires BoxCollider.");
        // Derive bounds from local geometry, including inactive barriers and disabled trail colliders.
        Physics.SyncTransforms(); Bounds b=WorldBounds();
        float highest=float.NegativeInfinity, lowest=float.PositiveInfinity;
        for(int x=0;x<3;x++) for(int z=0;z<3;z++)
        {
            float y=Height(new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/2f),0,Mathf.Lerp(b.min.z,b.max.z,z/2f)));
            highest=Mathf.Max(highest,y); lowest=Mathf.Min(lowest,y);
        }
        transform.position+=Vector3.up*(highest+clearance-b.min.y);
        maximumCornerGap=highest-lowest;
    }
    public bool Validate(out string reason)
    {
        Physics.SyncTransforms(); Bounds b=WorldBounds();
        float highest=float.NegativeInfinity;
        for(int x=0;x<3;x++) for(int z=0;z<3;z++)
            highest=Mathf.Max(highest,Height(new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/2f),0,Mathf.Lerp(b.min.z,b.max.z,z/2f))));
        float gap=b.min.y-highest;
        reason=name+": bottom clearance="+gap.ToString("F3")+"m, footprint relief="+maximumCornerGap.ToString("F3")+"m";
        return gap>=-.005f && gap<=.075f;
    }
    private Bounds WorldBounds()
    {
        var box=GetComponent<BoxCollider>();
        Bounds bounds=new Bounds(transform.TransformPoint(box.center),Vector3.zero);
        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            bounds.Encapsulate(transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z))));
        return bounds;
    }
}
