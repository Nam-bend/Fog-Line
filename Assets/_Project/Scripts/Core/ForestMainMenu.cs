using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ForestMainMenu : MonoBehaviour
{
    private readonly Color cream=new Color(.9f,.9f,.82f), muted=new Color(.55f,.65f,.63f), gold=new Color(.65f,.71f,.68f);
    private Font font;
    private RectTransform canvas, menu, overlay, sheet, loading;
    private Button continueButton, startButton;
    private Text saveLabel, status, loadingLabel;
    private Image progress;
    private bool launching;
    private string checkpoint;

    private void Awake()
    {
        Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cameraObject=new GameObject("Menu Camera",typeof(Camera),typeof(AudioListener));
        cameraObject.transform.SetParent(transform);
        var camera=cameraObject.GetComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.025f,.052f,.06f); camera.cullingMask=0;
        var eventObject=new GameObject("Menu EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        eventObject.transform.SetParent(transform);
        eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        canvas=Rect("Blackpine Main Menu",transform,Vector2.zero,Vector2.zero);
        var c=canvas.gameObject.AddComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1600,900); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        BuildMenu();
        overlay=Panel("Modal backdrop",canvas,Vector2.zero,Vector2.zero,new Color(0,.015f,.02f,.9f)); Stretch(overlay);
        sheet=Panel("Menu sheet",overlay,new Vector2(320,120),new Vector2(960,660),new Color(.035f,.072f,.078f));
        overlay.gameObject.SetActive(false);
        BuildLoading(); RefreshSave();
    }
    private void Start() => Select(startButton);
    private void BuildMenu()
    {
        menu=Rect("Home",canvas,Vector2.zero,new Vector2(1600,900));
        Label(menu,"GREYHAVEN",new Vector2(580,195),new Vector2(520,80),48,cream);
        startButton=ActionButton(menu,"CHƠI MỚI",new Vector2(580,325),new Vector2(440,58),()=>Launch(false),true);
        continueButton=ActionButton(menu,"TIẾP TỤC",new Vector2(580,397),new Vector2(440,58),Continue);
        saveLabel=Label(menu,"",new Vector2(580,461),new Vector2(600,28),15,muted);
        ActionButton(menu,"CÀI ĐẶT",new Vector2(580,509),new Vector2(440,58),ShowSettings);
        ActionButton(menu,"THOÁT",new Vector2(580,581),new Vector2(440,58),ShowQuit);
        status=Label(menu,"",new Vector2(400,682),new Vector2(800,80),20,cream);
    }
    private void RefreshSave()
    {
        bool valid=ForestMenuFlow.TryReadCheckpoint(out checkpoint,out string description);
        continueButton.interactable=valid; saveLabel.text=description;
    }
    private void OpenSheet(string eyebrow,string title)
    {
        foreach(Transform child in sheet) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        menu.gameObject.SetActive(false); overlay.gameObject.SetActive(true);
        Label(sheet,eyebrow,new Vector2(55,40),new Vector2(850,30),16,gold);
        Label(sheet,title,new Vector2(55,94),new Vector2(850,65),39,cream);
        Line(sheet,new Vector2(55,178),new Vector2(850,1),new Color(.2f,.29f,.29f));
    }
    private Button BackButton()
    {
        return ActionButton(sheet,"←  QUAY LẠI",new Vector2(55,554),new Vector2(250,56),CloseSheet);
    }
    private void CloseSheet()
    {
        overlay.gameObject.SetActive(false); menu.gameObject.SetActive(true); RefreshSave(); Select(startButton);
    }
    private void ShowSettings()
    {
        OpenSheet("THIẾT LẬP","Âm thanh và hiển thị");
        Label(sheet,"Âm lượng tổng",new Vector2(55,225),new Vector2(600,38),24,cream);
        var value=Label(sheet,"",new Vector2(768,225),new Vector2(130,38),22,gold);
        var sliderRoot=Rect("Master volume",sheet,new Vector2(55,295),new Vector2(850,40));
        var slider=sliderRoot.gameObject.AddComponent<Slider>(); slider.minValue=0; slider.maxValue=1;
        var track=Panel("Track",sliderRoot,new Vector2(0,16),new Vector2(850,8),new Color(.16f,.23f,.24f));
        var fillArea=Rect("Fill area",sliderRoot,new Vector2(10,16),new Vector2(830,8));
        var fill=Panel("Fill",fillArea,Vector2.zero,Vector2.zero,gold); Stretch(fill);
        var handleArea=Rect("Handle area",sliderRoot,new Vector2(10,0),new Vector2(830,40));
        var handle=Panel("Handle",handleArea,Vector2.zero,new Vector2(18,32),cream);
        handle.pivot=new Vector2(.5f,.5f); handle.anchoredPosition=Vector2.zero; handle.sizeDelta=new Vector2(18,-8);
        slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
        slider.value=Mathf.Clamp01(PlayerPrefs.GetFloat(ForestMenuFlow.VolumeKey,.8f)); value.text=Mathf.RoundToInt(slider.value*100)+" %";
        slider.onValueChanged.AddListener(v=>{ AudioListener.volume=v; PlayerPrefs.SetFloat(ForestMenuFlow.VolumeKey,v); value.text=Mathf.RoundToInt(v*100)+" %"; });
        var fullscreen=ActionButton(sheet,"",new Vector2(55,393),new Vector2(850,62),null);
        var fullLabel=fullscreen.GetComponentInChildren<Text>();
        System.Action refresh=()=>fullLabel.text="TOÀN MÀN HÌNH    "+(Screen.fullScreen ? "BẬT" : "TẮT"); refresh();
        fullscreen.onClick.AddListener(()=>{ Screen.fullScreen=!Screen.fullScreen; StartCoroutine(UpdateFullscreenLabel(refresh)); });
        Label(sheet,"WASD: di chuyển · Chuột: nhìn · Space: nhảy\nE: tương tác · F: đèn pin · M: bản đồ · Q: radio\nChuột trái: bắn · R: nạp đạn · Esc: tạm dừng",new Vector2(55,470),new Vector2(850,78),17,muted);
        var back=BackButton(); back.onClick.AddListener(PlayerPrefs.Save); Select(slider);
    }
    private IEnumerator UpdateFullscreenLabel(System.Action refresh) { yield return null; yield return null; refresh(); }
    private void ShowQuit()
    {
        OpenSheet("GREYHAVEN","Thoát game?");
        Label(sheet,"Checkpoint đã lưu vẫn được giữ cho chuyến tìm kiếm tiếp theo.",new Vector2(55,235),new Vector2(850,100),25,cream);
        Select(BackButton());
        ActionButton(sheet,"THOÁT GAME",new Vector2(525,554),new Vector2(380,56),()=>
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        },true);
    }
    private void Continue()
    {
        RefreshSave(); if(string.IsNullOrEmpty(checkpoint)) { status.text="Không đọc được checkpoint. Bạn có thể bắt đầu chuyến tìm kiếm mới."; return; }
        Launch(true);
    }
    private void Launch(bool resume)
    {
        if(launching) return;
        if(!Application.CanStreamedLevelBeLoaded(ForestMenuFlow.GameScene))
        { CloseSheet(); status.text="Không tải được màn rừng. Vui lòng kiểm tra bản cài đặt."; return; }
        ForestStoryDirector.PrepareMenuStart(resume ? checkpoint : null);
        PlayerPrefs.Save(); launching=true; overlay.gameObject.SetActive(false); menu.gameObject.SetActive(false);
        loading.gameObject.SetActive(true); EventSystem.current.SetSelectedGameObject(null); StartCoroutine(LoadForest());
    }
    private IEnumerator LoadForest()
    {
        yield return null;
        var operation=SceneManager.LoadSceneAsync(ForestMenuFlow.GameScene);
        while(!operation.isDone)
        {
            float amount=Mathf.Clamp01(operation.progress/.9f);
            progress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,800*amount);
            loadingLabel.text="ĐANG VÀO BLACKPINE  ·  "+Mathf.RoundToInt(amount*100)+" %";
            yield return null;
        }
    }
    private void BuildLoading()
    {
        loading=Panel("Loading",canvas,Vector2.zero,Vector2.zero,new Color(.02f,.044f,.05f,.97f)); Stretch(loading);
        Label(loading,"Đang tải",new Vector2(400,320),new Vector2(850,90),55,cream);
        loadingLabel=Label(loading,"ĐANG VÀO RỪNG…",new Vector2(400,435),new Vector2(850,40),20,gold);
        var track=Panel("Loading track",loading,new Vector2(400,504),new Vector2(800,3),muted);
        var fill=Panel("Loading fill",track,Vector2.zero,new Vector2(800,3),gold);
        progress=fill.GetComponent<Image>(); progress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,0);
        Label(loading,"Tiếng động có thể khiến bạn bị phát hiện. Hãy quan sát lối vòng.",new Vector2(400,559),new Vector2(850,80),22,muted);
        loading.gameObject.SetActive(false);
    }
    private void Update()
    {
        if(!launching && Keyboard.current?.escapeKey.wasPressedThisFrame==true)
        { if(overlay.gameObject.activeSelf) CloseSheet(); else ShowQuit(); }
    }
    private RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(position.x,-position.y); rect.sizeDelta=size; return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var rect=Rect(name,parent,position,size); rect.gameObject.AddComponent<Image>().color=color; return rect;
    }
    private void Line(Transform parent,Vector2 position,Vector2 size,Color color) => Panel("Divider",parent,position,size,color).GetComponent<Image>().raycastTarget=false;
    private Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color)
    {
        var rect=Rect(value.Length>30 ? value.Substring(0,30) : value,parent,position,size);
        var text=rect.gameObject.AddComponent<Text>(); text.font=font; text.text=value; text.fontSize=fontSize; text.color=color;
        text.supportRichText=false; text.raycastTarget=false; text.verticalOverflow=VerticalWrapMode.Overflow; return text;
    }
    private Button ActionButton(Transform parent,string title,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,bool primary=false)
    {
        var rect=Panel(title,parent,position,size,primary ? gold : new Color(.07f,.13f,.14f,.9f));
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=rect.GetComponent<Image>();
        var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.15f,1.15f,1.12f);
        colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.65f,.75f,.72f); colors.disabledColor=new Color(.45f,.48f,.48f,.4f); button.colors=colors;
        Label(rect,title,new Vector2(18,(size.y-29)*.5f),new Vector2(size.x-32,34),20,primary ? new Color(.03f,.065f,.069f) : cream);
        if(action!=null) button.onClick.AddListener(action); return button;
    }
    private static void Select(Selectable selectable) { if(EventSystem.current!=null) EventSystem.current.SetSelectedGameObject(selectable.gameObject); }
}
