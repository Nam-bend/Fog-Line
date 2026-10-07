using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class ForestObservation
{
    public string id;
    public Transform marker;
    public float radius=5f;
}

[DefaultExecutionOrder(-80)]
public sealed class ForestStoryDirector : MonoBehaviour
{
    public static ForestStoryDirector Instance { get; private set; }
    public static bool BlocksInput => Instance != null && (Instance.modal || !Instance.ready || Instance.State.stage == 4);
    public ForestStoryState State = new ForestStoryState();
    public bool PowerRunning { get; private set; }
    public bool Ready => ready;
    public Vector3 PowerPoint;
    public List<Vector3> outward = new List<Vector3>(), homeward = new List<Vector3>(), optional = new List<Vector3>();
    public List<ForestStoryItem> items = new List<ForestStoryItem>();
    public List<ForestObservation> observations = new List<ForestObservation>();
    public List<ForestCreature> creatures = new List<ForestCreature>();
    public GameObject serviceGate, landslide, guardGate, motherSilhouette;
    public Transform shadowStart, shadowEnd;
    public Light powerIndicator;
    public string buildError;
    private PlayerHealth player;
    private FirstPersonWeapon weapon;
    private Light flashlight;
    private bool ready, modal, mapOpen, inspection, busy, powerDone;
    private string paperTitle, paperText, subtitle, notice;
    private Action inspectDone;
    private float subtitleUntil, noticeUntil, ambientAt, nextObservation;
    private readonly Queue<string> dialogue = new Queue<string>();
    private AudioSource sound;
    private AudioClip chirp, rumble, leaves;
    private GUIStyle textStyle, titleStyle;
    private static string pendingLoad;
    private string memoryCheckpoint;
    private string SavePath => ForestMenuFlow.CheckpointPath;
    public static void PrepareMenuStart(string checkpointJson)
    {
        pendingLoad=checkpointJson; GameManager.SmallEnemyKillCount=0; Time.timeScale=1;
    }
    private void ReturnToMenu()
    {
        pendingLoad=null; Time.timeScale=1; SceneManager.LoadScene(ForestMenuFlow.MenuScene);
    }

    private void Awake() { Instance = this; }
    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Time.timeScale = 1; }
        if (chirp != null) Destroy(chirp); if (rumble != null) Destroy(rumble); if (leaves != null) Destroy(leaves);
    }
    public void Initialize(PlayerHealth actor)
    {
        Instance = this; player = actor; weapon = player.GetComponent<FirstPersonWeapon>();
        if (!string.IsNullOrEmpty(pendingLoad))
        {
            try { State = JsonUtility.FromJson<ForestStoryState>(pendingLoad) ?? new ForestStoryState(); }
            catch (Exception e) { Debug.LogWarning("Checkpoint unreadable: " + e.Message); }
            pendingLoad = null;
        }
        GameManager.SmallEnemyKillCount = State.kills;
        sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false;
        chirp = Tone(950, .65f, .25f); rumble = Tone(58, 2.2f, .45f); leaves = Tone(170, .8f, .07f);
        var lightObject = new GameObject("Elias flashlight");
        lightObject.transform.SetParent(player.GetComponentInChildren<Camera>().transform,false);
        flashlight = lightObject.AddComponent<Light>(); flashlight.type = LightType.Spot;
        flashlight.range = 25; flashlight.spotAngle = 62; flashlight.intensity = 3; flashlight.shadows = LightShadows.Soft;
        flashlight.color = new Color(1,.94f,.8f);
    }
    public void Begin()
    {
        ready = true; SetModal(false); ApplyWorldState();
        if (State.position != Vector3.zero)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.SetPositionAndRotation(State.position, Quaternion.Euler(0,State.yaw,0)); cc.enabled = true;
            player.RestoreStoryHealth(State.health); weapon.RestoreStoryAmmo(State);
        }
        foreach (var creature in creatures)
        {
            var snapshot = State.enemies.Find(x => x.id == creature.storyId);
            if (State.Has("dead:" + creature.storyId) || (snapshot != null && snapshot.health <= 0))
                creature.gameObject.SetActive(false);
            else if (snapshot != null)
            {
                creature.GetComponent<EnemyHealth>().RestoreHealth(snapshot.health);
                var agent = creature.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent.isOnNavMesh) agent.Warp(snapshot.position);
            }
            if (creature.powerWitness && State.stage < 2) creature.gameObject.SetActive(false);
        }
        if (State.stage == 0 && !State.Has("shell"))
            Say("NHIỆM VỤ — Tìm Jonah Reed. Hai thợ săn nói anh có thể đã đến lán kiểm lâm.",
                "ELIAS [RADIO] — Tôi theo đường phía tây đến lán. Cứ mười lăm phút gọi một lần. Nếu mất liên lạc hai lần, đừng để một người vào một mình.",
                "RADIO — Đã rõ. Kiểm tra dấu vết trên đường. M: bản đồ / nhật ký · F: đèn pin · E: tương tác.");
        SaveCheckpoint(false);
    }
    public bool Has(string id) => State.Has(id);
    public ForestStoryItem Item(string id) => items.Find(x => x != null && x.id == id);
    public void Say(params string[] lines) { foreach (string line in lines) dialogue.Enqueue(line); }
    public void Notify(string text) { notice = text; noticeUntil = Time.unscaledTime + 5; }

    public void Interact(ForestStoryItem item)
    {
        if (!ready || modal || busy || player.IsDead) return;
        switch (item.id)
        {
            case "power":
                if (State.stage < 1) { Say("ELIAS — Tôi cần kiểm tra vỏ đạn trên đường trước đã."); return; }
                if (State.stage >= 2) { Say("ELIAS — Dây nguồn đã chập. Cổng vẫn mở."); return; }
                StartCoroutine(PowerSequence()); break;
            case "diagram":
                if (State.stage < 2) { Say("ELIAS — Tôi chưa kiểm tra trạm kỹ thuật và đường vào lán."); return; }
                if (Has("diagram")) { Inspect("Sơ đồ G-07",DiagramText(),null); return; }
                Inspect("Sơ đồ G-07",DiagramText(),TakeDiagram); break;
            case "briggs":
                if (!State.CanFinish) { Say("BRIGGS — Tôi giữ trạm. Có tin gì thì báo về."); return; }
                StartCoroutine(EndSequence()); break;
        }
    }
    private string DiagramText() => "PROJECT G-07 / Khu văn phòng\n\nGhi chú của Jonah: ‘Lối bảo trì — chưa được kiểm tra trong biên bản cuối.’\n\nSơ đồ không chỉ vị trí hốc hang.\n\nELIAS — Cậu vẫn giữ chuyện này…";
    private void Inspect(string title, string body, Action done)
    {
        inspection = true; mapOpen = false;
        paperTitle = title; paperText = body; inspectDone = done; SetModal(true);
    }
    private void CloseInspection()
    {
        inspection = false; SetModal(false); var done = inspectDone; inspectDone = null; done?.Invoke();
    }
    private void TakeDiagram()
    {
        if (!State.CollectDiagram()) return;
        Notify("Đã cất sơ đồ G-07 vào nhật ký"); ApplyWorldState();
        Say("ELIAS [RADIO] — Jonah đã ở lán. Hiện cậu ấy không còn ở đây. Có dấu kéo về nền đá, nhưng bị nước xóa ở bờ suối.",
            "RADIO — Đã rõ. Mang những gì tìm được về trạm.");
        Play(rumble); SaveCheckpoint();
    }
    private void CheckObservations()
    {
        if(busy || Time.time<nextObservation) return;
        nextObservation=Time.time+.25f;
        var camera=player.GetComponentInChildren<Camera>();
        foreach(var observation in observations)
        {
            if(observation.marker==null || !observation.marker.gameObject.activeInHierarchy || !State.CanObserve(observation.id)) continue;
            Vector3 point=observation.marker.position;
            if(Vector3.Distance(player.transform.position,point)>observation.radius) continue;
            // Do not comment on clues through the cabin wall or a rock face.
            Vector3 eye=camera!=null ? camera.transform.position : player.transform.position+Vector3.up;
            if(Physics.Linecast(eye,point,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                && hit.transform!=observation.marker && !hit.transform.IsChildOf(observation.marker)) continue;
            if(!State.Observe(observation.id)) continue;
            switch(observation.id)
            {
                case "shell":
                    Say("ELIAS — Vỏ đạn đã bắn… Họ đâu chỉ nghe thấy tiếng động.","ELIAS [RADIO] — Hỏi lại hai thợ săn. Tôi tiếp tục đến lán.");
                    StartCoroutine(DistantCall()); SaveCheckpoint(); break;
                case "cloth":
                    Say("ELIAS — Vải áo của Jonah. Cậu ấy đã tới đây."); SaveCheckpoint(); break;
                case "blockage":
                    Say("ELIAS [RADIO] — Đường bị cây đổ chắn rồi. Tôi vòng qua khe đá phía nam.");
                    SaveCheckpoint(); break;
            }
            break;
        }
    }
    private IEnumerator PowerSequence()
    {
        busy = true; PowerRunning = true;
        foreach (var creature in creatures)
            if (creature != null && creature.powerWitness && !Has("dead:" + creature.storyId)) creature.gameObject.SetActive(true);
        if (powerIndicator != null) powerIndicator.enabled = true;
        Say("ELIAS — Cầu dao nhảy. Tôi cấp điện tạm cho cổng.","[Bộ rung phát nhịp trầm. Con vật lùi khỏi nền đá về phía bùn.]");
        Notify("Đang giữ cầu dao thử nền — quan sát con vật bên bệ đá.");
        Play(rumble);
        if (serviceGate != null) serviceGate.SetActive(false);
        yield return new WaitForSeconds(7);
        PowerRunning = false; powerDone = true; State.OpenServiceGate();
        if (powerIndicator != null) powerIndicator.enabled = false;
        Say("[Dây nguồn chập. Nhịp rung tắt, con vật ngừng lùi.]", "ELIAS — Nó tránh cái nền đá… hay cái máy?");
        busy = false; SaveCheckpoint();
    }
    private IEnumerator DistantCall()
    {
        Play(chirp); yield return new WaitForSeconds(2.5f); Play(rumble);
        Say("[Một tiếng gọi cao từ bụi cây. Một âm trầm đáp lại rất xa.]");
    }
    public void EnemyKilled(string id, Vector3 position)
    {
        State.kills = GameManager.SmallEnemyKillCount; State.Record("dead:" + id);
        StartCoroutine(DistantCall());
        Say("[Tiếng gọi đứt quãng. Dịch sẫm bắn lên tay áo Elias.]");
    }
    private IEnumerator ShadowSequence()
    {
        State.shadowSeen = true; Play(rumble);
        Say("[Một bóng lớn lướt trên vách. Cành phía trên bị đẩy sang bên.]");
        if (motherSilhouette != null)
        {
            motherSilhouette.SetActive(true);
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                motherSilhouette.transform.position = Vector3.Lerp(shadowStart.position,shadowEnd.position,t/5);
                yield return null;
            }
            motherSilhouette.SetActive(false);
        }
        SaveCheckpoint();
    }
    private IEnumerator EndSequence()
    {
        busy = true;
        if (guardGate != null) guardGate.SetActive(false);
        Say("BRIGGS — Tìm thấy nó chưa?", "ELIAS — Chưa. Gọi mọi người về làng. Đừng để ai đi đường rừng nữa.",
            "RADIO — Các tổ xác nhận lại số người mất tích.");
        while (dialogue.Count > 0 || Time.time < subtitleUntil) yield return null;
        State.Complete(); busy = false; SetModal(true);
    }
    private void ApplyWorldState()
    {
        var diagram=Item("diagram");
        if(diagram!=null) diagram.gameObject.SetActive(!Has("diagram"));
        powerDone = State.stage >= 2;
        if (serviceGate != null) serviceGate.SetActive(!powerDone);
        if (landslide != null) landslide.SetActive(State.stage >= 3);
        if (guardGate != null) guardGate.SetActive(State.stage < 4);
        if (motherSilhouette != null) motherSilhouette.SetActive(false);
    }
    private void Update()
    {
        if (!ready) return;
        if (!player.IsDead && !modal && Time.time >= subtitleUntil && dialogue.Count > 0)
        {
            subtitle = dialogue.Dequeue(); subtitleUntil = Time.time + Mathf.Clamp(subtitle.Length / 17f,4,12);
        }
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame && !modal) flashlight.enabled = !flashlight.enabled;
            if (keyboard.qKey.wasPressedThisFrame && !modal && dialogue.Count == 0 && Time.time >= subtitleUntil)
                Say("ELIAS [RADIO] — Tôi vẫn đang trên tuyến tìm kiếm.", "RADIO — Đã nhận. " + Objective() + ".");
            if (keyboard.mKey.wasPressedThisFrame && !inspection && State.stage < 4 && !player.IsDead)
            { mapOpen = !mapOpen; SetModal(mapOpen); }
            if (keyboard.escapeKey.wasPressedThisFrame && !inspection && State.stage < 4 && !player.IsDead)
            { mapOpen = !mapOpen; SetModal(mapOpen); }
            if (keyboard.rKey.wasPressedThisFrame && player.IsDead) ReloadCheckpoint();
        }
        if (player.IsDead) { SetModal(true); return; }
        if (modal) return;
        CheckObservations();
        if (State.stage == 3 && Has("blockage") && !State.shadowSeen && homeward.Count > 2 && Distance(homeward[homeward.Count/2]) < 6)
            StartCoroutine(ShadowSequence());
        if (Time.time > ambientAt)
        {
            ambientAt = Time.time + 8;
            bool threatened = creatures.Exists(x => x != null && x.isActiveAndEnabled && x.Perception == "Chasing");
            if (!threatened) Play(State.stage < 3 ? chirp : leaves);
        }
    }
    private float Distance(Vector3 p) => Vector3.ProjectOnPlane(player.transform.position-p,Vector3.up).magnitude;
    private void SetModal(bool value)
    {
        modal = value; Time.timeScale = value ? 0 : 1;
        Cursor.visible = value; Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
    }
    public void SaveCheckpoint(bool disk = true)
    {
        if (player == null || player.IsDead || PowerRunning) return;
        State.position = player.transform.position; State.yaw = player.transform.eulerAngles.y;
        State.health = player.CurrentHealth; State.kills = GameManager.SmallEnemyKillCount; weapon.SaveStoryAmmo(State);
        State.enemies.Clear();
        foreach (var enemy in creatures)
            if (enemy != null) State.enemies.Add(new ForestEnemySnapshot { id=enemy.storyId, health=enemy.GetComponent<EnemyHealth>().CurrentHealth, position=enemy.transform.position });
        memoryCheckpoint = JsonUtility.ToJson(State);
        if (!disk) return;
        try { File.WriteAllText(SavePath,memoryCheckpoint); Notify("Đã lưu checkpoint"); }
        catch (Exception e) { Notify("Checkpoint giữ trong phiên này; không ghi được tệp."); Debug.LogWarning(e.Message); }
    }
    private void ReloadCheckpoint(bool disk = false)
    {
        try { pendingLoad = disk && File.Exists(SavePath) ? File.ReadAllText(SavePath) : memoryCheckpoint; }
        catch (Exception e) { Notify(e.Message); return; }
        Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    private void NewGame() { pendingLoad = null; Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    private string Objective()
    {
        if (State.stage == 0) return "Theo đường rừng đến lán kiểm lâm";
        if (State.stage == 1) return "Vượt quái con — cấp điện mở cổng công vụ";
        if (State.stage == 2) return "Tìm sơ đồ của Jonah trong lán kiểm lâm";
        if (State.stage == 3) return !Has("blockage") ? "Mang sơ đồ về cho Briggs ở trạm gác" : "Theo đường vòng phía nam về trạm gác";
        return "BLACKPINE FOREST — KẾT DEMO";
    }
    private void OnGUI()
    {
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { fontSize=18, wordWrap=true, normal={textColor=Color.white} };
            titleStyle = new GUIStyle(textStyle) { fontSize=24, fontStyle=FontStyle.Bold };
        }
        GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        if (!string.IsNullOrEmpty(buildError))
        {
            GUI.Box(new Rect(160,140,960,400),"");
            GUI.Label(new Rect(190,170,900,270),"Không dựng được đường an toàn trên terrain:\n"+buildError,textStyle);
            if(GUI.Button(new Rect(430,460,420,50),"Về màn hình chính")) ReturnToMenu();
            return;
        }
        if (!ready) { GUI.Label(new Rect(30,30,1100,80),"Đang chuẩn bị đường rừng và kiểm tra mặt đất…",textStyle); return; }
        GUI.Box(new Rect(18,16,740,76),""); GUI.Label(new Rect(30,22,715,32),Objective(),textStyle);
        GUI.Label(new Rect(30,55,715,26),"E: tương tác   M: bản đồ   Q: radio   F: đèn pin   Esc: dừng",textStyle);
        if (Time.time < subtitleUntil && !modal) { GUI.Box(new Rect(150,565,980,118),""); GUI.Label(new Rect(170,579,940,98),subtitle,textStyle); }
        if (Time.unscaledTime < noticeUntil) GUI.Label(new Rect(30,105,900,55),notice,textStyle);
        if (player.IsDead)
        {
            GUI.Box(new Rect(330,220,620,240),""); GUI.Label(new Rect(355,250,570,65),"Elias đã chết. Tải checkpoint để thử lại.",titleStyle);
            if (GUI.Button(new Rect(430,355,420,52),"R — Tải checkpoint")) ReloadCheckpoint(); return;
        }
        if (State.stage == 4)
        {
            GUI.Box(new Rect(250,150,780,420),""); GUI.Label(new Rect(285,180,710,65),Objective(),titleStyle);
            GUI.Label(new Rect(285,265,710,160),"Đã mang sơ đồ G-07 về trạm gác.\nJonah vẫn mất tích. Đội tìm kiếm sẽ tiếp tục từ những gì Elias tìm được.",textStyle);
            if (GUI.Button(new Rect(290,470,330,48),"Chơi lại demo")) NewGame();
            if (GUI.Button(new Rect(655,470,330,48),"Về màn hình chính")) ReturnToMenu();
            return;
        }
        if (inspection)
        {
            GUI.Box(new Rect(220,120,840,480),""); GUI.Label(new Rect(250,150,780,50),paperTitle,titleStyle);
            GUI.Label(new Rect(250,225,780,270),paperText,textStyle);
            if (GUI.Button(new Rect(400,525,480,48),"Cất sơ đồ và tiếp tục")) CloseInspection();
            return;
        }
        if (mapOpen) DrawMap();

    }
    private void DrawMap()
    {
        GUI.Box(new Rect(110,105,1060,550),""); GUI.Label(new Rect(140,123,990,35),"BLACKPINE — Bản đồ và nhật ký",titleStyle);
        if(GUI.Button(new Rect(900,122,230,37),"Về menu (giữ checkpoint)")) ReturnToMenu();
        Rect map = new Rect(145,185,530,360); GUI.Box(map,"");
        var all = new List<Vector3>(outward); all.AddRange(homeward); all.AddRange(optional);
        if (all.Count > 0)
        {
            Bounds bounds = new Bounds(all[0],Vector3.zero); foreach (var p in all) bounds.Encapsulate(p); bounds.Expand(10);
            DrawRoute(outward,map,bounds,new Color(.85f,.7f,.3f));
            if (Has("blockage")) DrawRoute(homeward,map,bounds,new Color(.3f,.8f,.9f));
            foreach (string id in new[] { "briggs","power","diagram" })
            {
                var item=Item(id); if(item==null) continue;
                Vector2 p=MapPoint(item.transform.position,map,bounds);
                GUI.Label(new Rect(p.x-5,p.y-15,180,42),"■ "+(id=="diagram" ? "Lán" : id=="power" ? "Trạm máy" : item.title),textStyle);
            }
            Vector2 actor=MapPoint(player.transform.position,map,bounds); GUI.Label(new Rect(actor.x-6,actor.y-12,100,25),"● Elias",textStyle);
        }
        string journal=Objective()+"\n\n";
        if(Has("power")) journal+="Cổng công vụ đã mở.\n";
        if(Has("diagram")) journal+="Đang mang sơ đồ G-07 về trạm.\n";
        GUI.Label(new Rect(715,185,420,330),journal,textStyle);
        if (GUI.Button(new Rect(145,578,270,45),"Trở lại (M / Esc)")) { mapOpen=false; SetModal(false); }
        if (GUI.Button(new Rect(435,578,300,45),"Tải checkpoint trong phiên")) ReloadCheckpoint();
        if (File.Exists(SavePath) && GUI.Button(new Rect(755,578,380,45),"Tải checkpoint đã lưu trên máy")) ReloadCheckpoint(true);
    }
    private static Vector2 MapPoint(Vector3 p,Rect r,Bounds b) => new Vector2(r.x+(p.x-b.min.x)/b.size.x*r.width,r.yMax-(p.z-b.min.z)/b.size.z*r.height);
    private static void DrawRoute(List<Vector3> route,Rect r,Bounds b,Color color)
    {
        Color previous=GUI.color; GUI.color=color;
        foreach(var p in route) { Vector2 q=MapPoint(p,r,b); GUI.DrawTexture(new Rect(q.x-2,q.y-2,4,4),Texture2D.whiteTexture); }
        GUI.color=previous;
    }
    private void Play(AudioClip clip) { if (sound != null) sound.PlayOneShot(clip,.5f); }
    private static AudioClip Tone(float frequency,float seconds,float volume)
    {
        const int rate=22050; float[] data=new float[(int)(rate*seconds)];
        for(int i=0;i<data.Length;i++) { float t=i/(float)rate; data[i]=Mathf.Sin(2*Mathf.PI*frequency*t+3*Mathf.Sin(t*12))*Mathf.Sin(Mathf.PI*i/data.Length)*volume; }
        var clip=AudioClip.Create("Forest temporary cue",data.Length,1,rate,false); clip.SetData(data,0); return clip;
    }
}
