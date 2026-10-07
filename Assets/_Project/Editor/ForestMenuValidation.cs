using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ForestMenuValidation
{
    private const string Key="Blackpine.MenuValidation";
    static ForestMenuValidation() { EditorApplication.update+=Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        SessionState.SetBool(Key,true); SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);
        SessionState.SetInt(Key+"Frames",0); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false)) return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120) throw new Exception("Menu did not initialize in time.");
            if(!EditorApplication.isPlaying) return;
            var menu=UnityEngine.Object.FindFirstObjectByType<ForestMainMenu>();
            if(menu==null)
            {
                var story=UnityEngine.Object.FindFirstObjectByType<ForestStoryDirector>();
                if(story==null) return;
                if(!string.IsNullOrEmpty(story.buildError)) throw new Exception("Menu loaded ForestDemo but scene setup failed: "+story.buildError);
                if(!story.Ready) return;
                File.WriteAllText("Logs/MainMenuValidation.txt","PASS: title, four main actions, checkpoint availability, button bounds, settings panel, render captures, and direct new-game transition to ready ForestDemo.");
                SessionState.SetBool(Key,false); EditorApplication.Exit(0); return;
            }
            int frames=SessionState.GetInt(Key+"Frames",0)+1; SessionState.SetInt(Key+"Frames",frames);
            if(frames<20) return;
            if(frames==20)
            {
                Canvas.ForceUpdateCanvases();
                var texts=menu.GetComponentsInChildren<Text>();
                if(!texts.Any(x=>x.text=="GREYHAVEN")) throw new Exception("Title missing.");
                var buttons=menu.GetComponentsInChildren<Button>();
                if(buttons.Length!=4) throw new Exception("Expected four main menu actions.");
                var continueButton=buttons.First(x=>x.GetComponentInChildren<Text>().text=="TIẾP TỤC");
                if(continueButton.interactable!=ForestMenuFlow.TryReadCheckpoint(out _,out _)) throw new Exception("Continue state does not match checkpoint availability.");
                foreach(var button in buttons)
                {
                    var corners=new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    if(corners.Any(p=>p.x<0 || p.y<0 || p.x>Screen.width+1 || p.y>Screen.height+1)) throw new Exception("Off-screen button: "+button.name);
                }
                Capture(menu,"Logs/MainMenu.png");
                Click(menu,"CÀI ĐẶT");
                if(menu.GetComponentInChildren<Slider>()==null) throw new Exception("Settings slider missing.");
                Capture(menu,"Logs/MainMenuSettings.png"); Click(menu,"←  QUAY LẠI");
            }
            if(frames==31)
            {
                Click(menu,"CHƠI MỚI");
            }
        }
        catch(Exception e)
        {
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MainMenuValidation.txt",e.ToString());
            SessionState.SetBool(Key,false); Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    private static void Click(ForestMainMenu menu,string label)
    {
        var button=menu.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.GetComponentInChildren<Text>()?.text==label);
        if(button==null) throw new InvalidOperationException("Menu button missing: "+label+". Visible actions: "+string.Join(", ",menu.GetComponentsInChildren<Button>().Select(x=>x.GetComponentInChildren<Text>()?.text)));
        if(!button.IsInteractable()) throw new InvalidOperationException("Menu button disabled: "+label);
        button.onClick.Invoke();
    }
    private static void Capture(ForestMainMenu menu,string path)
    {
        var canvas=menu.GetComponentInChildren<Canvas>(); var camera=menu.GetComponentInChildren<Camera>();
        var target=new RenderTexture(1600,900,24); var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        foreach(var node in canvas.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=5;
        camera.cullingMask=1<<5; camera.targetTexture=target;
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes(path,image.EncodeToPNG());
        RenderTexture.active=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
        camera.targetTexture=null; camera.cullingMask=0;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    }
}
