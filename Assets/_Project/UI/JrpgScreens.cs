using System;
using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HGame.Unity
{
    public sealed partial class JrpgPresenter
    {
        public void Refresh()
        {
            if(content==null) return;
            UiKit.Clear(content); hintText=null;
            var run=Session?.CaptureRun();
            bool fighting=run!=null&&run.Location==GameLocation.Battle;
            if(World!=null) World.SetVisible(!MainMenuVisible&&!fighting&&!ResultVisible&&!GalleryVisible);
            if(MainMenuVisible) RenderMainMenu();
            else if(fighting||ResultVisible) RenderCombat();
            else if(run!=null)
            {
                RenderHud(run);
                if(run.Location==GameLocation.GameOver||run.Location==GameLocation.DemoComplete) RenderEnding(run);
                else if(GalleryVisible) RenderGalleryScreen();
                else if(window!="") RenderWindow(run);
            }
            if(MainMenuVisible&&GalleryVisible) RenderGalleryScreen();
            ApplyLock();
        }
        private void Background()
        { UiKit.PanelAt("Backdrop",content,0,0,1280,720,UiKit.Background); }
        private RectTransform WindowPanel(string title,string subtitle="")
        {
            UiKit.PanelAt("Shade",content,0,0,1280,720,new Color(0,0,0,.65f));
            var p=UiKit.PanelAt("Window",content,210,100,860,520,UiKit.Panel).rectTransform;
            UiKit.PanelAt("Line",p,0,0,860,3,UiKit.Teal);
            UiKit.Text(title,p,26,20,700,40,28,UiKit.Gold);
            UiKit.Text(subtitle,p,28,68,800,58,17,UiKit.Muted);
            UiKit.Button("CloseWindow","返回",p,700,20,130,40,()=>{window="";GalleryVisible=false;Refresh();}); return p;
        }
        private void RenderMainMenu()
        {
            Background();
            UiKit.PanelAt("Cover",content,36,35,1208,650,UiKit.Panel);
            UiKit.PanelAt("CoverLine",content,36,35,1208,4,UiKit.Teal);
            UiKit.Text("A NIGHT IN THE LABYRINTH",content,85,90,800,32,20,UiKit.Teal);
            UiKit.Text("迷宮與她的夜晚",content,80,158,820,84,50);
            UiKit.Text("每一次出發，都會留下些什麼。",content,86,262,760,45,26,UiKit.Gold);
            UiKit.Text("俯視地圖探索  ×  橫向回合制戰鬥\n一座小城鎮，以及尚未解開的迷宮。",content,87,335,720,92,22,UiKit.Muted);
            PixelImage("CoverHero","hero_down",content,966,174,150,210);
            PixelImage("CoverSave","save",content,944,420,72,72); PixelImage("CoverEnemy","enemy",content,1070,430,72,72);
            UiKit.Button("NewGame","開始新的旅程",content,86,476,340,58,()=>{if(Session!=null)Confirm("開始新的旅程？","新的旅程獨立保存記憶；原手動存檔仍可載入。",NewGame);else NewGame();},true,UiKit.Hex("38645F"));
            UiKit.Button("Continue","繼續旅程",content,86,548,340,58,()=>{if(Session!=null){MainMenuVisible=false;Refresh();}else LoadSlot("auto",LoadIntent.Continue);},Session!=null||Repository.HasSlot("auto"));
            UiKit.Button("MainGallery","回想畫廊",content,450,476,220,58,OpenGallery);
            UiKit.Button("MainQuit","離開",content,450,548,220,58,()=>Application.Quit());
            UiKit.Text("SYSTEM DEMO 0.2.0\nWASD／方向鍵移動 · E／Enter 互動 · Esc 選單\n所有角色及參與者均成年，素材與地圖為測試配置。",content,710,553,488,95,16,UiKit.Muted);
        }
        private void RenderHud(RunState run)
        {
            UiKit.PanelAt("Header",content,0,0,1280,80,new Color(.055f,.08f,.13f,.96f));
            UiKit.Text(World?.Map?.Name??"旅程",content,24,13,490,34,26,UiKit.Gold);
            UiKit.Text("第 "+run.Chapter+" 章  ·  第 "+run.Day+" / 20 天  ·  "+new[]{"早上","中午","晚上","深夜"}[(int)run.Slot],content,24,50,550,24,16,UiKit.Muted);
            UiKit.Text("HP  "+run.Hp+" / "+run.MaxHp+"     MP  "+run.Mp+" / "+run.MaxMp,content,620,18,470,30,20);
            UiKit.Text("金錢 "+run.Gold+"　"+(run.Job==Job.Dancer?"舞者":"劍士"),content,755,51,335,24,16,UiKit.Teal);
            UiKit.Button("Menu","選單  Esc",content,1115,18,140,48,()=>OpenWindow("Menu"));
            UiKit.PanelAt("Foot",content,0,638,1280,82,new Color(.055f,.08f,.13f,.96f));
            hintText=UiKit.Text(World?.Hint??"",content,24,648,840,28,20,UiKit.Gold);
            UiKit.Text(status,content,24,683,1200,27,15,UiKit.Muted).overflowMode=TextOverflowModes.Ellipsis;
            UiKit.Text("WASD 移動　E 互動",content,925,651,330,25,17,UiKit.Teal);
            if(run.Hp==0)
            {
                UiKit.PanelAt("Warning",content,18,92,920,42,new Color(.055f,.08f,.13f,.9f));
                UiKit.Text("HP 為 0：開選單恢復，或選安全返城。",content,26,96,890,32,21,UiKit.Pink);
                UiKit.Button("SafeReturn","安全返城",content,1030,94,220,44,()=>Execute(new GameCommand(CommandKind.ReturnTown)));
            }
            else if(run.Chapter==1&&run.Day<3)
            {
                UiKit.PanelAt("Tutorial",content,18,92,920,38,new Color(.055f,.08f,.13f,.83f));
                UiKit.Text(run.Location==GameLocation.Dungeon?"教學：面向敵人或物件按 E。通道在完成節點後開啟，選單可安全返城。":"教學：面向建築窗口或路標按 E，早上可從入口遠征。",content,26,98,900,28,17,UiKit.Ink);
            }
        }
        private void RenderWindow(RunState run)
        {
            string title=window=="Menu"?"旅程選單":window=="Items"?"物品與資源":window=="Shop"?"旅人商店":window=="Work"?"工作窗口":window=="Jobs"?"職業登記":window=="Inn"?"旅店 · 休息與日程":window=="Memory"?"尚未接起的記憶":window=="Supply"?"眼前的補給":"操作說明";
            var panel=WindowPanel(title,window=="Help"?"探索使用鍵盤；戰鬥與選單使用滑鼠。":"行動的成本、條件及結果依目前旅程狀態決定。");
            if(window=="Menu")
            {
                var items=new List<(string,string,Action,bool)>{
                    ("Inventory","物品與資源",()=>OpenWindow("Items"),true),
                    ("SaveManual","記錄旅程",SaveManual,Repository.CanSave(Session.Snapshot)),
                    ("LoadManual","讀取手動存檔",()=>Confirm("讀取手動存檔？","將回復該存檔的進度與位置。",()=>LoadSlot("manual",LoadIntent.ManualLoad)),Repository.HasSlot("manual")),
                    ("LoadAuto","讀取自動存檔",()=>Confirm("讀取自動存檔？","將回復該存檔的進度與位置。",()=>LoadSlot("auto",LoadIntent.ManualLoad)),Repository.HasSlot("auto")),
                    ("Gallery","回想畫廊",OpenGallery,true),("Help","操作說明",()=>OpenWindow("Help"),true),
                    ("MainMenu","返回主選單",GoMainMenu,true),("Quit","離開遊戲",()=>Confirm("離開試玩？","保留最近的安全進度。",()=>{Repository.Persist(Session);Application.Quit();}),true)};
                var list=UiKit.GridScroll(panel,28,140,804,340,390,65);
                foreach(var it in items) UiKit.Button(it.Item1,it.Item2,list,0,0,390,65,it.Item3,it.Item4);
                return;
            }
            if(window=="Help")
            {
                UiKit.Text("WASD／方向鍵：四向行走\nE／Enter：面向物件互動　Esc：選單／返回\n\n早上在迷宮入口遠征，晚上返回；深夜留在旅店休息。\n戰鬥點選目標再選指令；舞者可令全體同時暈眩獲勝。\n深度戰鬥用 H 與精力結算，HP 0 勝利後先恢復或返城。\n地圖與人物為占位素材；正式配置將依後續設計替換。",panel,28,140,800,320,21); return;
            }
            var available=Session.GetAvailableActions();
            IEnumerable<ActionAvailability> actions;
            switch(window)
            {
                case "Shop": actions=available.Where(a=>a.Command.Kind==CommandKind.Buy); break;
                case "Work": actions=available.Where(a=>a.Command.Kind==CommandKind.Work||a.Command.Kind==CommandKind.AdultWork); break;
                case "Jobs": actions=available.Where(a=>a.Command.Kind==CommandKind.SwitchJob); break;
                case "Inn": actions=available.Where(a=>a.Command.Kind==CommandKind.Wait||a.Command.Kind==CommandKind.Rest||a.Command.Kind==CommandKind.RestToChapterEnd); break;
                case "Supply": actions=available.Where(a=>a.Command.Kind==CommandKind.ChooseSupply); break;
                case "Memory":
                    UiKit.Text("有些經歷的核心仍很清楚，時間與來由卻無法銜接。",panel,28,118,800,70,20,UiKit.Muted);
                    actions=available.Where(a=>a.Command.Kind==CommandKind.AcceptRoute||a.Command.Kind==CommandKind.DeclineRoute); break;
                default: actions=available.Where(a=>a.Command.Kind==CommandKind.UseItem||a.Command.Kind==CommandKind.ReturnTown||a.Command.Kind==CommandKind.CompleteDemo); break;
            }
            DrawActions(actions,panel,28,190,804,290,390,76);
            if(!actions.Any()) UiKit.Text("目前沒有可執行的選項。\n可以先返回探索，或等待不同的時機。",panel,28,220,800,140,23,UiKit.Muted);
        }
        private void DrawActions(IEnumerable<ActionAvailability> actions,Transform parent,float x,float y,float w,float h,float cw,float ch)
        {
            var list=UiKit.GridScroll(parent,x,y,w,h,cw,ch);
            foreach(var item in actions)
            {
                var action=item;
                string label=action.Label+(!action.Enabled&&action.Reason!=""?"\n<size=13>"+action.Reason+"</size>":"");
                UiKit.Button("Action_"+action.Id,label,list,0,0,cw,ch,()=>Execute(action.Command),action.Enabled);
            }
        }
        private void RenderEnding(RunState run)
        {
            var panel=WindowPanel(run.Location==GameLocation.GameOver?"期限已到":"系統試玩完成","讀取存檔，或回到主選單開始另一段旅程。");
            UiKit.Text(run.Location==GameLocation.GameOver?"本章尚未完成攻略。\n請載入先前進度重新安排。":"你已走過本次試玩的核心循環。\n正式地圖、劇情與更多玩法將於後續補充。",panel,28,170,800,140,26);
            UiKit.Button("EndMenu","返回主選單",panel,28,360,380,58,GoMainMenu);
            if(run.Location==GameLocation.DemoComplete) UiKit.Button("EndReturn","繼續驗證",panel,430,360,380,58,()=>RunCommand(new GameCommand(CommandKind.ReturnTown)));
        }
        private void RenderGalleryScreen()
        {
            var panel=WindowPanel("回想畫廊","重播與主線旅程完全分開，不取得資源或增加回退。");
            if(gallery.Items.Count==0) UiKit.Text("尚未解鎖回想。",panel,28,180,800,130,26,UiKit.Muted);
            else if(gallery.IsPlaying)
            { UiKit.Text(Data.Event("gallery_reference").Body,panel,28,160,800,200,24);UiKit.Button("SkipGallery","結束回想",panel,28,390,380,58,()=>{gallery.Skip();Refresh();}); }
            else UiKit.Button("PlayGallery","回想 · 記憶片段",panel,28,180,800,72,()=>{gallery.Play(gallery.Items[0]);Refresh();});
            UiKit.Button("CloseGallery","返回旅程",panel,430,390,380,58,CloseGallery);
        }
        private Image PixelImage(string name,string key,Transform parent,float x,float y,float w,float h)
        { var r=UiKit.Rect(name,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=art.Get(key);im.preserveAspect=true;im.raycastTarget=false;return im; }
    }
}
