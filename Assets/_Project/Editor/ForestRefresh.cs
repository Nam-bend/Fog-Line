using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ForestRefresh
{
    const string Request = "Logs/ForestRefresh.request";
    [InitializeOnLoadMethod]
    static void Listen() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "refresh") Refresh();
            else if (command == "polish") Polish();
            else if (command == "cleanup") ForestVegetationCleanup.Run();
            else if (command == "inspect-second") InspectSecond();
            else if (command == "mix-second") MixSecond();
        }
        catch (Exception e) { File.WriteAllText("Logs/ForestRefresh.result", e.ToString()); Debug.LogException(e); }
    }
    public static void Inspect()
    {
        var report = new System.Text.StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            report.AppendLine($"SCENE {scene.path} dirty={scene.isDirty}");
        }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Environment/Tree1/source/trees1.fbx");
        foreach (var t in asset.GetComponentsInChildren<Transform>(true))
        {
            report.AppendLine($"NODE {t.name} pos={t.localPosition} scale={t.localScale} rotation={t.localEulerAngles}");
            var renderer = t.GetComponent<Renderer>();
            if (renderer != null) report.AppendLine($"  bounds={renderer.bounds} mats={string.Join(",",renderer.sharedMaterials.Select(m=>m.name))}");
            var filter = t.GetComponent<MeshFilter>();
            if (filter != null) report.AppendLine($"  mesh={filter.sharedMesh.name} verts={filter.sharedMesh.vertexCount} submeshes={filter.sharedMesh.subMeshCount} bounds={filter.sharedMesh.bounds}");
        }
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Environment/Tree1/source/trees1.fbx").OfType<Material>())
            report.AppendLine($"MAT {m.name} color={m.color} texture={m.mainTexture}");
        foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            report.AppendLine($"TERRAIN {terrain.name} pos={terrain.transform.position} size={terrain.terrainData.size} layers={terrain.terrainData.terrainLayers.Length} shader={terrain.materialTemplate.shader.name}");
        File.WriteAllText("Logs/ForestRefresh.inspect",report.ToString());
    }
    public static void InspectSecond()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Environment/Tree2/source/tree1.fbx");
        var report=new System.Text.StringBuilder();
        foreach(var t in model.GetComponentsInChildren<Transform>())
        {
            report.AppendLine($"NODE {t.name} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale}");
            var renderer=t.GetComponent<Renderer>();
            if(renderer!=null)report.AppendLine($"  bounds={renderer.bounds} mats={string.Join(",",renderer.sharedMaterials.Select(m=>m.name))}");
            var filter=t.GetComponent<MeshFilter>();
            if(filter!=null)report.AppendLine($"  meshBounds={filter.sharedMesh.bounds}");
        }
        File.WriteAllText("Logs/ForestSecond.inspect",report.ToString());
    }

    [MenuItem("Tools/Forest/Apply Tree2 And Mix Woodland")]
    public static void MixSecond()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=ScenePath)
            throw new InvalidOperationException("Open ForestDemo in Edit mode.");
        terrain=Object.FindFirstObjectByType<Terrain>();
        const string sourcePath="Assets/_Project/Environment/Tree2/source/tree1.fbx";
        const string textureFolder="Assets/_Project/Environment/Tree2/textures/";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if(source==null)throw new InvalidOperationException("Tree2 model missing.");
        Material Convert(string name,string colorFile,string normalFile,bool leaves)
        {
            string path=Output+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            var normalImporter=(TextureImporter)AssetImporter.GetAtPath(textureFolder+normalFile);
            if(normalImporter.textureType!=TextureImporterType.NormalMap)
            {normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();}
            var colorImporter=(TextureImporter)AssetImporter.GetAtPath(textureFolder+colorFile);
            if(leaves && !colorImporter.alphaIsTransparency){colorImporter.alphaIsTransparency=true;colorImporter.SaveAndReimport();}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder+colorFile));
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder+normalFile));
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.65f);material.SetFloat("_Smoothness",.1f);
            material.enableInstancing=true;
            if(leaves){material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.45f);material.SetFloat("_Cull",0);
                material.EnableKeyword("_ALPHATEST_ON");material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=2450;}
            EditorUtility.SetDirty(material);return material;
        }
        var bark=Convert("Tree2Bark","tree1Color.png","tree1Normal.png",false);
        var foliage=Convert("Tree2Foliage","branch1Coloralpha.png","branch1Normal.png",true);
        var trunk=source.GetComponent<MeshFilter>().sharedMesh;
        float scale=9/trunk.bounds.size.y;
        var meshes=new List<CombineInstance>();var materials=new List<Material>();
        foreach(var r in source.GetComponentsInChildren<MeshRenderer>())
        {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                meshes.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,
                    transform=Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(Vector3.down*trunk.bounds.min.y)*source.transform.worldToLocalMatrix*r.transform.localToWorldMatrix});
                materials.Add(r.gameObject==source ? bark : foliage);
            }
        }
        var combined=new Mesh{name="WoodlandTreeLeafy"};combined.CombineMeshes(meshes.ToArray(),false,true);
        string meshPath=Output+"/WoodlandTreeLeafy.asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(saved==null)AssetDatabase.CreateAsset(combined,meshPath);else{EditorUtility.CopySerialized(combined,saved);Object.DestroyImmediate(combined);combined=saved;EditorUtility.SetDirty(saved);}
        var go=new GameObject("WoodlandTreeLeafy");go.AddComponent<MeshFilter>().sharedMesh=combined;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials.ToArray();
        var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.008f,new Renderer[]{renderer})});lod.RecalculateBounds();
        var collider=go.AddComponent<CapsuleCollider>();collider.radius=.65f;collider.height=5;collider.center=Vector3.up*2.5f;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Output+"/WoodlandTreeLeafy.prefab");Object.DestroyImmediate(go);
        var data=terrain.terrainData;
        Undo.RecordObject(data,"Mix Tree1 and Tree2 woodland");
        var prototypes=data.treePrototypes.Take(3).ToList();prototypes.Add(new TreePrototype{prefab=prefab});data.treePrototypes=prototypes.ToArray();
        var instances=data.treeInstances;int leafy=0;
        for(int i=0;i<instances.Length;i++)
        {
            var position=Vector3.Scale(instances[i].position,data.size)+terrain.transform.position;
            bool useLeafy=Mathf.PerlinNoise(position.x*.08f+51,position.z*.08f+17)>.49f;
            instances[i].prototypeIndex=useLeafy ? 3 : i%3;
            if(useLeafy)leafy++;
        }
        data.SetTreeInstances(instances,true);data.RefreshPrototypes();EditorUtility.SetDirty(data);terrain.Flush();
        AssetDatabase.SaveAssets();ForestDemoSetup.Bake();ForestDemoSetup.Validate();
        Capture();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Logs/Tree2Mix.result",$"TREE2_MIX_OK\nTree2 leafy: {leafy}\nTree1 dead: {instances.Length-leafy}\nTotal: {instances.Length}\nNavMesh and escape clearance validated.");
    }
    const string Output = "Assets/_Project/Environment/ForestRefresh";
    const string ScenePath = "Assets/_Project/Scenes/ForestDemo.unity";
    static Terrain terrain;
    static Transform root;
    static Vector3 Ground(float x,float z) => new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z)) + terrain.transform.position.y,z);

    [MenuItem("Tools/Forest/Refresh Forest With Imported Trees")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open ForestDemo first; current scene is preserved.");
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
        AssetDatabase.Refresh();
        string backup = "Assets/_Project/Scenes/Backups/ForestBeforeCleanup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene,backup,true)) throw new IOException("Could not back up current scene.");
        terrain = Object.FindFirstObjectByType<Terrain>();
        root = terrain.transform.parent;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"Clean forest map");
        // Work on a separate TerrainData; the backup retains its original sculpt and tree instances.
        string dataPath = Output + "/WoodlandTerrain.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath) != null)
            throw new InvalidOperationException("Refresh already applied. Edit the woodland directly; rebake navigation after edits.");
        var original = terrain.terrainData;
        var data = Object.Instantiate(original);
        AssetDatabase.CreateAsset(data,dataPath);
        terrain.terrainData = data;
        terrain.GetComponent<TerrainCollider>().terrainData = data;
        ExpandTerrain(original,data);
        PaintSoil(data);
        var prefabs = Enumerable.Range(1,3).Select(BuildTree).ToArray();
        PlantTrees(data,prefabs);
        foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t == null || t == root) continue;
            if (t.name.EndsWith(" boundary") || t.name.StartsWith("Refuge ") ||
                t.name == "Escape left" || t.name == "Escape right" || t.name.StartsWith("Escape gap") ||
                t.name.StartsWith("Cover - ") || t.GetComponent<TextMesh>() != null || t.name == "tent_detailedOpen")
                Undo.DestroyObjectImmediate(t.gameObject);
        }
        foreach (var go in scene.GetRootGameObjects())
            if (!go.activeSelf && new[] { "Ground","Door","Key","Directional Light" }.Contains(go.name))
                Undo.DestroyObjectImmediate(go);
        root.name = "Forest Environment";
        terrain.name = "Woodland Terrain";
        DressForest();
        TuneLight();
        var player = Object.FindFirstObjectByType<PlayerHealth>();
        var cc = player.GetComponent<CharacterController>();
        Undo.RecordObject(player.transform,"Place forest start");
        player.transform.position = Ground(-22,-27) + Vector3.up*(cc.height/2-cc.center.y+.1f);
        player.transform.rotation = Quaternion.Euler(0,25,0);
        var view = player.GetComponentInChildren<Camera>();
        Undo.RecordObject(view.transform,"Face forest trail");
        view.transform.localRotation = Quaternion.identity;
        terrain.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        ForestDemoSetup.Bake();
        ForestDemoSetup.Validate();
        EditorSceneManager.SaveScene(scene);
        Capture();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Logs/ForestRefresh.result",$"REFRESH_OK\nBackup: {backup}\nTrees: {data.treeInstanceCount}\n3 imported tree prototypes; boundary walls removed; terrain 160m; navigation validated.");
        Debug.Log("FOREST_REFRESH_OK",terrain);
    }

    static void ExpandTerrain(TerrainData original,TerrainData data)
    {
        data.heightmapResolution = 513;
        data.size = new Vector3(160,28,160);
        terrain.transform.position = new Vector3(-80,-.15f,-80);
        var heights = new float[513,513];
        for (int z=0;z<513;z++) for (int x=0;x<513;x++)
        {
            float wx = x/512f*160-80, wz=z/512f*160-80;
            float edge = Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz));
            float old = original.GetInterpolatedHeight(Mathf.Clamp01((wx+40)/80),Mathf.Clamp01((wz+40)/80));
            float rise = Mathf.SmoothStep(0,1,Mathf.InverseLerp(39,77,edge));
            heights[z,x] = (old + rise*(9+13*Mathf.PerlinNoise(wx*.035f+13,wz*.035f+7)))/28;
        }
        data.SetHeights(0,0,heights);
    }

    static float Trail(float z)
    {
        // Keep the route to the left of the rock refuge before curving toward the cabin.
        var points = new[] {new Vector2(-22,-27),new Vector2(-14,-16),new Vector2(-10,0),new Vector2(-10,14),new Vector2(9,20),new Vector2(23,26)};
        for (int i=1;i<points.Length;i++) if (z<=points[i].y)
            return Mathf.Lerp(points[i-1].x,points[i].x,Mathf.SmoothStep(0,1,Mathf.InverseLerp(points[i-1].y,points[i].y,z)));
        return 23;
    }

    static Texture2D SoilTexture(string name,Color color)
    {
        var texture = new Texture2D(128,128,TextureFormat.RGB24,false);
        var random = new System.Random(274);
        var pixels = new Color[128*128];
        for (int y=0;y<128;y++) for(int x=0;x<128;x++)
        {
            float n = .6f + .5f*Mathf.PerlinNoise(x*.09f,y*.09f)+(float)random.NextDouble()*.22f;
            pixels[y*128+x] = color*n;
        }
        texture.SetPixels(pixels); texture.Apply();
        string path = Output+"/"+name+".png";
        File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void PaintSoil(TerrainData data)
    {
        var layers = new List<TerrainLayer>();
        foreach(var entry in new[] {("ForestFloor",new Color(.20f,.22f,.14f)),("LeafTrail",new Color(.29f,.235f,.16f))})
        {
            var layer = new TerrainLayer { diffuseTexture=SoilTexture(entry.Item1,entry.Item2),tileSize=new Vector2(4,4),smoothness=0 };
            AssetDatabase.CreateAsset(layer,Output+"/"+entry.Item1+".terrainlayer"); layers.Add(layer);
        }
        data.terrainLayers = layers.ToArray();
        data.alphamapResolution = 512;
        var map = new float[512,512,2];
        for (int z=0;z<512;z++) for(int x=0;x<512;x++)
        {
            float wx=x/511f*160-80,wz=z/511f*160-80;
            float trail = Mathf.Clamp01(1-Mathf.Abs(wx-Trail(wz))/(2.4f+.5f*Mathf.PerlinNoise(wx,wz)));
            trail *= Mathf.Clamp01((wz+34)/6)*Mathf.Clamp01((33-wz)/6);
            map[z,x,0]=1-trail; map[z,x,1]=trail;
        }
        data.SetAlphamaps(0,0,map);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(mat,Output+"/WoodlandTerrain.mat");
        terrain.materialTemplate=mat;
        terrain.treeDistance=150; terrain.treeBillboardDistance=150; terrain.treeMaximumFullLODCount=1200;
    }

    static Material TreeMaterial(string name,bool branches)
    {
        string path=Output+"/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        string textureName=branches ? "branch1" : name;
        string folder="Assets/_Project/Environment/Tree1/textures/";
        string colorPath=folder+textureName+(name=="tree1" ? "COlor.png" : "Color.png");
        var color=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
        string normalPath=folder+textureName+"Normal.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(normalPath);
        if(importer.textureType!=TextureImporterType.NormalMap)
        { importer.textureType=TextureImporterType.NormalMap; importer.SaveAndReimport(); }
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap",color);
        material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale",.65f);
        material.SetFloat("_Smoothness",.12f); material.enableInstancing=true;
        if(branches)
        {
            material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.45f);
            material.SetFloat("_Cull",0); material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType","TransparentCutout"); material.renderQueue=2450;
        }
        AssetDatabase.CreateAsset(material,path);return material;
    }

    static GameObject BuildTree(int index)
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Environment/Tree1/source/trees1.fbx");
        var source=model.GetComponentsInChildren<Transform>().First(t=>t.name=="tree"+index);
        var renderers=source.GetComponentsInChildren<MeshRenderer>();
        var combines=new List<CombineInstance>(); var materials=new List<Material>();
        // FBX contains three separate trees offset from one another. Normalize each independently.
        var bounds=source.GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 basePoint=new Vector3(0,bounds.min.y,0);
        float scale=9f/bounds.size.y;
        foreach(var renderer in renderers)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                combines.Add(new CombineInstance {mesh=mesh,subMeshIndex=sub,
                    transform=Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(-basePoint)*source.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
                materials.Add(TreeMaterial(renderer==source.GetComponent<MeshRenderer>() ? "tree"+index : "branches",renderer.transform!=source));
            }
        }
        var combined=new Mesh {name="WoodlandTree"+index};combined.CombineMeshes(combines.ToArray(),false,true);
        AssetDatabase.CreateAsset(combined,Output+"/WoodlandTree"+index+".asset");
        var tree=new GameObject("WoodlandTree"+index);
        tree.AddComponent<MeshFilter>().sharedMesh=combined;
        var r=tree.AddComponent<MeshRenderer>();r.sharedMaterials=materials.ToArray();
        var lod=tree.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.008f,new Renderer[]{r})});lod.RecalculateBounds();
        var collider=tree.AddComponent<CapsuleCollider>();collider.radius=.38f;collider.height=5;collider.center=Vector3.up*2.5f;
        var prefab=PrefabUtility.SaveAsPrefabAsset(tree,Output+"/WoodlandTree"+index+".prefab");Object.DestroyImmediate(tree);return prefab;
    }

    static void PlantTrees(TerrainData data,GameObject[] trees)
    {
        data.treePrototypes=trees.Select(p=>new TreePrototype {prefab=p}).ToArray();
        var random=new System.Random(924);
        var positions=new List<Vector2>();var instances=new List<TreeInstance>();
        for(int i=0;i<6500 && instances.Count<800;i++)
        {
            float x=(float)random.NextDouble()*148-74,z=(float)random.NextDouble()*148-74;
            if(z>-34 && z<33 && Mathf.Abs(x-Trail(z))<5)continue;
            if((x>16 && x<33 && z>18 && z<33) || (Mathf.Abs(x)<9 && z>-6 && z<15))continue;
            if(Vector2.Distance(new Vector2(x,z),new Vector2(12,1))<5 || Vector2.Distance(new Vector2(x,z),new Vector2(-15,-16))<3)continue;
            if(positions.Any(p=>(p-new Vector2(x,z)).sqrMagnitude<10))continue;
            if(Mathf.PerlinNoise(x*.045f+8,z*.045f+4)<.34f && random.NextDouble()<.8)continue;
            positions.Add(new Vector2(x,z));
            float scale=.8f+(float)random.NextDouble()*.6f;
            instances.Add(new TreeInstance {prototypeIndex=instances.Count%3,position=new Vector3((x+80)/160,0,(z+80)/160),
                widthScale=scale,heightScale=scale,rotation=(float)random.NextDouble()*Mathf.PI*2,color=Color.white,lightmapColor=Color.white});
        }
        data.SetTreeInstances(instances.ToArray(),true);
    }

    static Material Matte(string name,Color color)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")) {color=color};
        material.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(material,Output+"/"+name+".mat");return material;
    }

    static void DressForest()
    {
        var rocks=new GameObject("Rock formations and fallen timber").transform;rocks.SetParent(root);
        var rock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Environment/ForestDemo/rock_largeA.prefab");
        var stone=Matte("WeatheredStone",new Color(.24f,.255f,.23f));
        void PlaceRock(float x,float z,Vector3 size,float angle)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(rock,rocks);
            go.name="Weathered boulder";go.transform.position=Ground(x,z)-Vector3.up*.35f;
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            go.transform.localScale=new Vector3(size.x/mesh.bounds.size.x,size.y/mesh.bounds.size.y,size.z/mesh.bounds.size.z);
            go.transform.rotation=Quaternion.Euler(0,angle,0);
            go.GetComponent<Renderer>().sharedMaterial=stone;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
        }
        // Replace rectangular shelter walls with a stone pocket. Entrance clearance remains 1.7m.
        PlaceRock(-3.45f,-1,new Vector3(5.2f,5.8f,4),0);
        PlaceRock(3.45f,-1,new Vector3(5.2f,5.1f,4),0);
        PlaceRock(-5,4,new Vector3(5,5.6f,7),-12);
        PlaceRock(5,4,new Vector3(5,6.2f,7),15);
        PlaceRock(-3,9,new Vector3(6,5,5),-25);
        PlaceRock(3,9,new Vector3(6,5.7f,5),18);
        var refuge=new GameObject("Stone pocket - AI exclusion");refuge.transform.SetParent(root);
        refuge.transform.position=Ground(0,4)+Vector3.up*3;
        var volume=refuge.AddComponent<NavMeshModifierVolume>();volume.size=new Vector3(14,18,16);volume.area=1;
        PlaceRock(-17,-15,new Vector3(3.8f,2.8f,3.1f),24);
        PlaceRock(13,12,new Vector3(4,3.2f,3),75);
        PlaceRock(-23,13,new Vector3(5,3.4f,3.5f),-20);
        var log=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Environment/ForestDemo/log_large.prefab");
        var bark=Matte("FallenBark",new Color(.18f,.135f,.085f));
        foreach(var pos in new[]{new Vector2(-18,-6),new Vector2(20,14),new Vector2(-29,25)})
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(log,rocks);go.name="Fallen trunk";
            go.transform.position=Ground(pos.x,pos.y);go.transform.rotation=Quaternion.Euler(0,pos.x*9,0);
            go.GetComponent<Renderer>().sharedMaterial=bark;go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
        }
        var cabin=root.Find("Ranger cabin - B");
        if(cabin!=null)
        {
            var wood=Matte("RangerWeatheredWood",new Color(.5f,.45f,.36f));
            wood.SetTexture("_BaseMap",SoilTexture("WeatheredBoards",new Color(.48f,.38f,.26f)));
            foreach(var renderer in cabin.GetComponentsInChildren<MeshRenderer>())
                if(!renderer.name.StartsWith("Roof"))renderer.sharedMaterial=wood;
            // Physical porch posts and shallow steps give the cabin a readable entrance.
            for(int side=-1;side<=1;side+=2)
            {
                var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="Porch post";post.transform.SetParent(cabin);
                post.transform.position=Ground(23+side*3.2f,21.9f)+Vector3.up*1.7f;
                post.transform.localScale=new Vector3(.22f,3.4f,.22f);post.GetComponent<Renderer>().sharedMaterial=wood;
            }
        }
    }

    static void TuneLight()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.22f,.25f,.29f);
        RenderSettings.ambientEquatorColor=new Color(.12f,.14f,.15f);
        RenderSettings.ambientGroundColor=new Color(.06f,.07f,.055f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.075f,.10f,.115f);RenderSettings.fogDensity=.018f;
        if(RenderSettings.sun!=null) {RenderSettings.sun.color=new Color(.72f,.81f,1);RenderSettings.sun.intensity=1.1f;}
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;}
        DynamicGI.UpdateEnvironment();
    }

    public static void Polish()
    {
        if(SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open ForestDemo.");
        terrain=Object.FindFirstObjectByType<Terrain>();root=terrain.transform.parent;
        // Rounded, uneven stone silhouettes fit the imported textured trees.
        const int rings=12,sides=20;
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
        {
            float latitude=ring*Mathf.PI/rings,longitude=side*Mathf.PI*2/sides;
            var v=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
            float rough=.9f+.16f*Mathf.PerlinNoise(v.x*2+8,v.z*2+v.y+3);
            vertices.Add(new Vector3(v.x*rough,Mathf.Max(-.72f,v.y*rough),v.z*rough));uv.Add(new Vector2(side/(float)sides,ring/(float)rings));
            if(ring<rings && side<sides)
            {
                int a=ring*(sides+1)+side,b=a+sides+1;
                triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});
            }
        }
        var mesh=new Mesh {name="WeatheredBoulder"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Output+"/WeatheredBoulder.asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}
        var stone=AssetDatabase.LoadAssetAtPath<Material>(Output+"/WeatheredStone.mat");
        stone.color=new Color(.7f,.72f,.68f);stone.SetTexture("_BaseMap",SoilTexture("RockGrain",new Color(.39f,.4f,.37f)));
        foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Weathered boulder"))
        {
            var filter=t.GetComponent<MeshFilter>();
            Vector3 size=Vector3.Scale(filter.sharedMesh.bounds.size,t.localScale);
            filter.sharedMesh=mesh;t.localScale=new Vector3(size.x/mesh.bounds.size.x,size.y/mesh.bounds.size.y,size.z/mesh.bounds.size.z);
            t.position=new Vector3(t.position.x,Ground(t.position.x,t.position.z).y+size.y*.28f,t.position.z);
            t.GetComponent<MeshCollider>().sharedMesh=mesh;
        }
        foreach(var prototype in terrain.terrainData.treePrototypes)
        {
            var pathToPrefab=AssetDatabase.GetAssetPath(prototype.prefab);
            var prefab=PrefabUtility.LoadPrefabContents(pathToPrefab);
            prefab.GetComponent<CapsuleCollider>().radius=.65f;
            PrefabUtility.SaveAsPrefabAsset(prefab,pathToPrefab);PrefabUtility.UnloadPrefabContents(prefab);
        }
        terrain.terrainData.RefreshPrototypes();
        AssetDatabase.SaveAssets();ForestDemoSetup.Bake();ForestDemoSetup.Validate();
        Capture();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllText("Logs/ForestRefresh.result","POLISH_OK: rounded boulders, tree collisions, navigation and player clearance validated.");
    }

    public static void Capture()
    {
        terrain=Object.FindFirstObjectByType<Terrain>();
        var player=Object.FindFirstObjectByType<PlayerHealth>();
        var camera=new GameObject("Forest preview").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;
        var target=new RenderTexture(1440,900,24);camera.targetTexture=target;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        void Shot(string name,Vector3 position,Vector3 look)
        {
            camera.transform.position=position;camera.transform.LookAt(look);camera.fieldOfView=65;camera.Render();
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        }
        Shot("ForestCleanPlayer",Ground(-22,-27)+Vector3.up*1.7f,Ground(-14,-10)+Vector3.up*2);
        Shot("ForestCleanCabin",Ground(18,12)+Vector3.up*1.7f,Ground(23,26)+Vector3.up*2);
        Shot("ForestCleanOverview",new Vector3(-43,38,-47),new Vector3(0,0,5));
        RenderTexture.active=null;camera.targetTexture=null;
        Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(camera.gameObject);
    }
}
