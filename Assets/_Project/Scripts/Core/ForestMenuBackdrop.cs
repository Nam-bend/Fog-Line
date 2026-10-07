using UnityEngine;
using UnityEngine.UI;

// Vector scenery: no external images, source packs or generated textures needed.
public sealed class ForestMenuBackdrop : MaskableGraphic
{
    private float nextFrame;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect r = rectTransform.rect;
        Quad(mesh,r.xMin,r.yMin,r.width,r.height,new Color(.025f,.052f,.06f),new Color(.075f,.13f,.145f));
        for (int layer=0;layer<3;layer++)
        {
            Color c=layer==0 ? new Color(.08f,.145f,.15f) : layer==1 ? new Color(.038f,.092f,.097f) : new Color(.017f,.052f,.059f);
            for(int i=0;i<23;i++)
            {
                float seed=Mathf.Repeat(Mathf.Sin(i*17.73f+layer*31.17f)*143.8f,1);
                float x=r.xMin+r.width*(.26f+i*.041f)+layer*15;
                float ground=r.yMin+r.height*(.12f+layer*.07f+.035f*Mathf.Sin(i*.7f));
                float height=r.height*(.18f+seed*.35f+layer*.1f);
                Quad(mesh,x-2,ground,4,height,c,c);
                for(int b=0;b<5;b++)
                {
                    float y=ground+height*(.2f+b*.15f), half=height*(.2f-b*.025f);
                    Triangle(mesh,new Vector2(x-half,y),new Vector2(x+half,y),new Vector2(x,y+height*.38f),c);
                }
            }
        }
        float drift=Mathf.Sin(Time.unscaledTime*.09f)*.025f;
        for(int i=0;i<5;i++)
        {
            float y=r.yMin+r.height*(.12f+i*.13f+drift);
            Quad(mesh,r.xMin,y,r.width,r.height*.095f,new Color(.36f,.48f,.46f,0),new Color(.36f,.48f,.46f,.047f));
            Quad(mesh,r.xMin,y+r.height*.095f,r.width,r.height*.075f,new Color(.36f,.48f,.46f,.047f),new Color(.36f,.48f,.46f,0));
        }
        // Dark reading area fades into the forest, with a quiet warm light at the trail's end.
        Quad(mesh,r.xMin,r.yMin,r.width*.5f,r.height,new Color(.012f,.027f,.032f,.92f),new Color(.012f,.027f,.032f,.9f));
        float glow=.5f+.1f*Mathf.Sin(Time.unscaledTime*1.7f);
        Quad(mesh,r.xMin+r.width*.77f,r.yMin+r.height*.245f,3,5,new Color(.94f,.65f,.3f,glow),new Color(.94f,.65f,.3f,glow));
    }
    private void Update()
    {
        if(Time.unscaledTime<nextFrame) return;
        nextFrame=Time.unscaledTime+.05f; SetVerticesDirty();
    }
    private static void Quad(VertexHelper m,float x,float y,float width,float height,Color bottom,Color top)
    {
        int n=m.currentVertCount;
        m.AddVert(new Vector3(x,y),bottom,Vector2.zero); m.AddVert(new Vector3(x,y+height),top,Vector2.zero);
        m.AddVert(new Vector3(x+width,y+height),top,Vector2.zero); m.AddVert(new Vector3(x+width,y),bottom,Vector2.zero);
        m.AddTriangle(n,n+1,n+2); m.AddTriangle(n,n+2,n+3);
    }
    private static void Triangle(VertexHelper m,Vector2 a,Vector2 b,Vector2 c,Color color)
    {
        int n=m.currentVertCount; m.AddVert(a,color,Vector2.zero); m.AddVert(b,color,Vector2.zero); m.AddVert(c,color,Vector2.zero); m.AddTriangle(n,n+2,n+1);
    }
}
