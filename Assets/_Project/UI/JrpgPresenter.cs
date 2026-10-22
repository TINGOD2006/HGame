using System;
using System.Collections;
using System.IO;
using System.Linq;
using HGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace HGame.Unity
{
    public sealed partial class JrpgPresenter : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public GameData Data { get; private set; }
        public SaveRepository Repository { get; private set; }
        public string SaveRoot { get; private set; }
        public WorldController World { get; private set; }
        public bool MainMenuVisible { get; private set; } = true;
        public bool Busy { get; private set; }
        public bool ResultVisible { get; private set; }
        public bool GalleryVisible { get; private set; }
        public bool WindowVisible { get { return window != "" || modal != null || GalleryVisible; } }
        private RectTransform canvas, content, modal, portraitRoot;
        private TextMeshProUGUI hintText;
        private string status = "一座城鎮，一段新的旅程。", window = "", selected = "";
        private GallerySession gallery;
        private ProfileState profile;
        private JrpgArt art;
        private int generation;
        private GameSnapshot battleBefore, battleAfter;
        private void Start()
        {
            try
            {
                UiKit.Font = Resources.Load<TMP_FontAsset>("Fonts/DemoFont"); Data = DefinitionLoader.Load(); art = new JrpgArt();
                SaveRoot = Path.Combine(Application.persistentDataPath,"HGameDemo");
                var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args,"--demo-profile");
                if(at>=0 && at+1<args.Length)
                {
                    string name=args[at+1]; if(name.Length==0 || name.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_')) throw new ArgumentException("資料名稱不合法。");
                    SaveRoot=Path.Combine(SaveRoot,"Verification",name);
                }
                Repository = new SaveRepository(Data,new DiskSaveStore(SaveRoot),s=>(World==null || World.CanSave(s))&&WorldSaveRules.CanSave(s),WorldSaveRules.Resolve);
                profile=Repository.LoadProfile(); BuildCanvas(); Refresh();
                if(args.Contains("--jrpg-smoke") || args.Contains("--jrpg-resume")) gameObject.AddComponent<JrpgSmokeRunner>();
                if(args.Contains("--heroine-preview")) gameObject.AddComponent<HeroinePreviewCapture>();
            }
            catch(Exception e) { Debug.LogException(e); BuildCanvas(); Confirm("啟動失敗",e.Message,()=>Application.Quit(1),false); }
        }
        private void BuildCanvas()
        {
            if(canvas!=null) return;
            var go=new GameObject("JRPG Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=(RectTransform)go.transform; go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scale=go.GetComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1280,720); scale.matchWidthOrHeight=.5f;
            content=UiKit.Rect("Content",canvas,0,0,960,720); content.gameObject.AddComponent<RectMask2D>();
            portraitRoot=UiKit.Rect("HeroineSidebar",canvas,960,0,320,720);
            if(FindFirstObjectByType<EventSystem>()==null)
            { var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
        }
        private void Update()
        {
            if(hintText!=null && World!=null) hintText.text=World.Hint;
            var k=Keyboard.current;
            if(k!=null && k.escapeKey.wasPressedThisFrame && Session!=null && !MainMenuVisible)
            {
                if(modal!=null) CloseModal(); else if(GalleryVisible) CloseGallery(); else if(!Busy&&!ResultVisible)
                { window=window==""?"Menu":""; Refresh(); }
            }
            ApplyLock();
        }
        private void ApplyLock()
        { if(World!=null) World.InputLocked=MainMenuVisible||WindowVisible||Busy||ResultVisible||Session==null||Session.Snapshot.Run.Location==GameLocation.Battle; }
        private void EnsureWorld()
        {
            if(World!=null) return;
            World=new GameObject("Exploration").AddComponent<WorldController>();
            World.Initialize(Data,()=>Session.Snapshot,p=>Session.UpdatePresentation(p),Interact,art.Get);
        }
        public void NewGame()
        {
            Safe(()=>{ ResetPresentation(); profile=Repository.LoadProfile(); Session=GameSession.NewGame(Data,null,profile);
                EnsureWorld(); World.Sync(Session.CaptureRun(),true); MainMenuVisible=false; Repository.Persist(Session);
                status="WASD／方向鍵行走，面向物件按 E。右上角選單可查看操作。"; Refresh(); });
        }
        private void ResetPresentation()
        { generation++; StopAllCoroutines(); Busy=false; ResultVisible=false; GalleryVisible=false; window=""; CloseModal(); battleBefore=null; battleAfter=null; selected=""; }
        public void GoMainMenu() { ResetPresentation(); MainMenuVisible=true; Refresh(); }
        public void SaveManual()
        { Safe(()=>{ Repository.Save(Session,"manual"); status="已記錄位置與旅程。"; Refresh(); }); }
        public void LoadSlot(string slot,LoadIntent intent)
        {
            Safe(()=>{ ResetPresentation(); string message; Session=Repository.Load(Session,slot,intent,out message);
                EnsureWorld(); World.Sync(Session.CaptureRun(),true); MainMenuVisible=false; status=message; Refresh(); });
        }
        public CommandResult RunCommand(GameCommand command)
        {
            if(Session==null) return CommandResult.Reject("尚未開始旅程。");
            var before=Session.Snapshot;
            var result=Session.Execute(command); status=result.Message;
            if(result.Success)
            {
                var run=Session.CaptureRun();
                if(run.Location!=GameLocation.Battle) World.Sync(run);
                Safe(()=>Repository.Persist(Session,result.RequestAutosave));
                profile=Session.CaptureProfile();
                if(before.Run.Location==GameLocation.Battle && run.Battle!=null && run.Battle.IsTerminal)
                { battleBefore=before; battleAfter=Session.Snapshot; ResultVisible=true; window=""; }
            }
            Refresh(); return result;
        }
        public void Execute(GameCommand command)
        {
            if(Busy||ResultVisible) return;
            if(command.Kind==CommandKind.AdultWork)
            { Confirm("成人工作",Data.Event("activity_adult").Body+"\n\n25 金、10 H EXP，使用一個時段。",()=>{window="";RunCommand(command);}); return; }
            if(command.Kind==CommandKind.AcceptRoute)
            { Confirm("接受重大轉變？",Data.Event("route_branch").Body,()=>{window="";RunCommand(command);}); return; }
            if(command.Kind==CommandKind.RestToChapterEnd)
            { Confirm("休整至章末？","使用剩餘天數，不提供額外資源。",()=>{window="";RunCommand(command);}); return; }
            if(Session!=null && Session.Snapshot.Run.Location==GameLocation.Battle)
            { StartCoroutine(AnimateCommand(command)); return; }
            window=""; RunCommand(command);
        }
        private IEnumerator AnimateCommand(GameCommand command)
        {
            int token=generation; battleBefore=Session.Snapshot; Busy=true; ApplyLock();
            var result=RunCommand(command); battleAfter=Session.Snapshot;
            if(!result.Success) { Busy=false; Refresh(); yield break; }
            float elapsed=0;
            while(elapsed<.48f && token==generation)
            { elapsed+=Time.deltaTime; AnimateCombat(elapsed/.48f); yield return null; }
            if(token!=generation) yield break;
            Busy=false; Refresh();
        }
        public void DismissResult() { ResultVisible=false; battleBefore=null; battleAfter=null; Refresh(); }
        public void OpenWindow(string kind) { window=kind; Refresh(); }
        public void OpenGallery() { gallery=new GallerySession(Session!=null?Session.CaptureProfile():profile); GalleryVisible=true; window=""; Refresh(); }
        public void CloseGallery() { gallery?.Close(); GalleryVisible=false; Refresh(); }
        public void Interact(WorldInteraction item)
        {
            if(Session==null || MainMenuVisible||WindowVisible||Busy||ResultVisible) return;
            switch(item.Kind)
            {
                case "Node":
                    var action=Session.GetAvailableActions().FirstOrDefault(x=>x.Command.Kind==CommandKind.EnterNode&&x.Command.TargetId==item.NodeId);
                    if(action==null||!action.Enabled) { status=action?.Reason??"這個區域尚未開放。";Refresh();return; }
                    var node=Data.Node(item.NodeId);
                    if(node.EnemyIds.Count>0) Confirm(node.Name,"前方有敵人。要接近並進入戰鬥嗎？",()=>Execute(new GameCommand(CommandKind.EnterNode,item.NodeId)));
                    else { RunCommand(new GameCommand(CommandKind.EnterNode,item.NodeId)); if(Session.Snapshot.Run.PendingSupplyNodeId!="") OpenWindow("Supply"); }
                    break;
                case "Depart": Confirm("出發前往迷宮？","早上出發，返回城鎮時為晚上。",()=>Execute(new GameCommand(CommandKind.Depart))); break;
                case "Return": Execute(new GameCommand(CommandKind.ReturnTown)); break;
                case "Memory": OpenWindow("Memory"); break;
                case "Inn": OpenWindow("Inn"); break;
                case "Rest": OpenWindow("Inn"); break;
                case "ExitInn": status="深夜不外出。先在床邊休息，迎接次日。"; Refresh(); break;
                default: OpenWindow(item.Kind); break;
            }
        }
        private void Confirm(string title,string body,Action accept,bool cancel=true)
        {
            CloseModal(); modal=UiKit.Rect("Confirmation",canvas,0,0,960,720);
            var shade=modal.gameObject.AddComponent<Image>(); shade.color=new Color(0,0,0,.78f);
            var p=UiKit.PanelAt("Dialog",modal,120,165,720,390,UiKit.Panel).rectTransform;
            UiKit.Text(title,p,28,22,664,45,28,UiKit.Gold); UiKit.Text(body,p,28,88,664,205,21);
            UiKit.Button("ModalAccept","確認",p,28,310,cancel?320:664,52,()=>{CloseModal();accept?.Invoke();},true,UiKit.Hex("38645F"));
            if(cancel) UiKit.Button("ModalCancel","返回",p,372,310,320,52,()=>{CloseModal();ApplyLock();}); ApplyLock();
        }
        private void CloseModal() { if(modal!=null) {modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;} }
        private void Safe(Action task) { try{task();}catch(Exception e){Debug.LogException(e);status=e.Message;if(content!=null) Refresh();} }
    }
}
