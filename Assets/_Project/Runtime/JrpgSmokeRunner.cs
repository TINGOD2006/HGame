using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
namespace HGame.Unity
{
    public sealed class JrpgSmokeRunner : MonoBehaviour
    {
        [Serializable] private class Report
        { public string Build="0.2.0",Status="running",Error="",SaveRoot,CampaignId,StartedUtc,FinishedUtc; public int Width,Height; public List<string> Checks=new List<string>(); public string Method="Windows Player：合成鍵盤／指標事件與正式命令；未宣稱人工操作。"; }
        private JrpgPresenter p; private Report report; private string directory;
        private IEnumerator Start()
        {
            yield return null;p=GetComponent<JrpgPresenter>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--verification-dir");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            if(Keyboard.current!=null) InputSystem.EnableDevice(Keyboard.current);
            directory=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"JrpgVerification");Directory.CreateDirectory(Path.Combine(directory,"screenshots"));
            report=new Report{SaveRoot=p.SaveRoot,Width=Screen.width,Height=Screen.height,StartedUtc=DateTime.UtcNow.ToString("o")};
            var stack=new Stack<IEnumerator>();stack.Push(args.Contains("--jrpg-resume")?Resume():Scenario());
            while(stack.Count>0)
            {
                object current;try{var r=stack.Peek();if(!r.MoveNext()){stack.Pop();continue;}current=r.Current;}catch(Exception e){report.Error=e.ToString();Debug.LogException(e);break;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
            report.Status=report.Error==""?"passed":"failed";report.FinishedUtc=DateTime.UtcNow.ToString("o");report.CampaignId=p.Session?.CaptureRun().CampaignId??"";
            File.WriteAllText(Path.Combine(directory,"player-smoke.json"),JsonUtility.ToJson(report,true));Debug.Log("JRPG_SMOKE_"+report.Status);
            yield return new WaitForSeconds(.2f);Application.Quit(report.Error==""?0:1);
        }
        private void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);report.Checks.Add(message);}
        private IEnumerator Shot(string name){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"screenshots/"+name+".png"));yield return new WaitForSeconds(.16f);}
        private IEnumerator Click(string name)
        {
            yield return null;Canvas.ForceUpdateCanvases();var button=FindObjectsByType<Button>(FindObjectsSortMode.None).First(x=>x.name==name);
            Check(button.interactable,"UI 可操作："+name);var r=(RectTransform)button.transform;var pos=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            InputSystem.EnableDevice(mouse);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos});yield return new WaitForSeconds(.05f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return new WaitForSeconds(.05f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pos});yield return new WaitForSeconds(.05f);
        }
        private IEnumerator KeyPress(Key key)
        {var k=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(k,new KeyboardState(key));yield return new WaitForSeconds(.05f);InputSystem.QueueStateEvent(k,new KeyboardState());yield return new WaitForSeconds(.05f);}
        private IEnumerator Step(Vector2Int target)
        {
            var delta=target-p.World.Position;var key=delta.x>0?Key.D:delta.x<0?Key.A:delta.y>0?Key.S:Key.W;var k=Keyboard.current;
            InputSystem.QueueStateEvent(k,new KeyboardState(key));float end=Time.realtimeSinceStartup+3;
            while(p.World.Position!=target&&Time.realtimeSinceStartup<end)yield return null;
            InputSystem.QueueStateEvent(k,new KeyboardState());yield return null;
            Check(p.World.Position==target,"鍵盤移動至 "+target+"（實際 "+p.World.Position+"，鎖 "+p.World.InputLocked+"，移動 "+p.World.IsMoving+"，焦點 "+Application.isFocused+"，提示 "+p.World.Hint+"）");yield return null;
        }
        private IEnumerator WalkTo(string id)
        {
            var map=p.World.Map;var item=map.Interactions.First(x=>x.Id==id);var start=p.World.Position;var run=p.Session.CaptureRun();
            var queue=new Queue<Vector2Int>();var prev=new Dictionary<Vector2Int,Vector2Int>();queue.Enqueue(start);prev[start]=start;Vector2Int? goal=null;
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();if(GridRules.Adjacent(cell,item)){goal=cell;break;}
                for(int face=0;face<4;face++){var next=cell+GridRules.FacingDelta(face);if(!prev.ContainsKey(next)&&GridRules.CanEnter(map,next.x,next.y,run,p.Data)){prev[next]=cell;queue.Enqueue(next);}}
            }
            if(!goal.HasValue)throw new InvalidOperationException("互動無可達路徑："+id);
            var path=new List<Vector2Int>();var node=goal.Value;while(node!=start){path.Add(node);node=prev[node];}path.Reverse();foreach(var cell in path)yield return Step(cell);
            var direction=new Vector2Int(item.X,item.Y)-p.World.Position;yield return KeyPress(direction.x>0?Key.D:direction.x<0?Key.A:direction.y>0?Key.S:Key.W);
            Check(p.World.Nearby?.Id==id,"面向互動："+id);
        }
        private void Do(CommandKind kind,string target="",string choice="")
        {var r=p.RunCommand(new GameCommand(kind,target,choice));if(!r.Success)throw new InvalidOperationException(kind+": "+r.Message);if(p.ResultVisible)p.DismissResult();}
        private void Morning(){for(int i=0;i<4&&p.Session.CaptureRun().Slot!=TimeSlot.Morning;i++)Do(p.Session.CaptureRun().Slot==TimeSlot.LateNight?CommandKind.Rest:CommandKind.Wait);}
        private void NormalWin(){for(int i=0;i<30&&p.Session.CaptureRun().Location==GameLocation.Battle;i++)Do(CommandKind.Attack,p.Session.CaptureRun().Battle.Enemies.First(x=>x.Hp>0).InstanceId);Check(p.Session.CaptureRun().Location!=GameLocation.Battle,"普通勝利返回地圖");}
        private void HWin(){for(int i=0;i<30&&p.Session.CaptureRun().Location==GameLocation.Battle;i++)Do(CommandKind.Skill,p.Session.CaptureRun().Battle.Enemies.First(x=>x.Stamina>0).InstanceId,"h_touch");Check(p.Session.CaptureRun().Location!=GameLocation.Battle,"H 勝利返回地圖");}
        private void DanceWin(){for(int i=0;i<15&&p.Session.CaptureRun().Location==GameLocation.Battle;i++)Do(CommandKind.Skill,"","dance");Check(p.Session.CaptureRun().Battle.Outcome==BattleOutcome.StunVictory,"舞者全體暈眩勝利");}
        private IEnumerator Scenario()
        {
            yield return Shot("01-menu");yield return Click("NewGame");Check(p.Session!=null,"建立新旅程");yield return Shot("02-town");
            string head=p.Session.CaptureRun().TimelineHeadId;var k=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            yield return WalkTo("town_shop");Check(p.Session.CaptureRun().TimelineHeadId==head,"行走與轉向不建立時間線");yield return KeyPress(Key.E);Check(p.WindowVisible,"E 開啟商店");yield return Shot("03-shop");
            var blocked=p.World.Position;yield return KeyPress(Key.D);Check(p.World.Position==blocked,"視窗開啟時移動鎖定");yield return KeyPress(Key.Escape);
            yield return KeyPress(Key.Escape);Check(p.WindowVisible,"Esc 開選單");yield return Shot("04-menu");yield return KeyPress(Key.Escape);
            // The rest uses formal commands for rule reachability; map movement was exercised above.
            Do(CommandKind.Wait);Do(CommandKind.Wait);Do(CommandKind.Wait);Check(p.World.Map.Id=="inn","深夜轉入室內");yield return Shot("05-inn");Do(CommandKind.Rest);
            yield return WalkTo("town_depart");yield return KeyPress(Key.E);yield return Click("ModalAccept");Check(p.Session.CaptureRun().Location==GameLocation.Dungeon,"地圖入口遠征");yield return Shot("06-dungeon");
            yield return WalkTo("map_c1_n01");yield return KeyPress(Key.E);yield return Click("ModalAccept");Check(p.Session.CaptureRun().Location==GameLocation.Battle,"地圖接敵進入戰鬥");yield return Shot("07-battle");
            var attack=p.Session.GetAvailableActions().First(x=>x.Enabled&&x.Command.Kind==CommandKind.Skill&&x.Command.ChoiceId=="attack");
            yield return Click("Action_"+attack.Id);yield return new WaitForSeconds(.6f);Check(!p.Busy,"戰鬥動畫後解除輸入鎖");NormalWin();yield return Shot("08-after-battle");
            Do(CommandKind.EnterNode,"c1_n02");p.SaveManual();Check(p.Repository.CanSave(p.Session.Snapshot),"存檔錨點可存");
            Do(CommandKind.ReturnTown);Morning();Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n01");Do(CommandKind.EnterDeep);yield return Shot("09-deep");HWin();Do(CommandKind.ReturnTown);Morning();
            Do(CommandKind.SwitchJob,"Dancer");Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n03");Do(CommandKind.ChooseSupply,"","take");Do(CommandKind.EnterNode,"c1_n04");Do(CommandKind.ReturnTown);Morning();
            Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n05");DanceWin();Do(CommandKind.EnterNode,"c1_n06");Do(CommandKind.EnterNode,"c1_n07");Do(CommandKind.ChooseSupply,"","take");Do(CommandKind.EnterNode,"c1_n08");Do(CommandKind.ReturnTown);
            while(p.Session.CaptureRun().Day<6){Morning();if(p.Session.CaptureRun().Day<6)Do(CommandKind.Wait);}Morning();
            Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n09");yield return Shot("10-multiple");DanceWin();Do(CommandKind.EnterNode,"c1_n10");Do(CommandKind.EnterNode,"c1_n11");yield return Shot("11-boss");DanceWin();Do(CommandKind.EnterNode,"c1_n12");Do(CommandKind.ReturnTown);
            // Failure and rewind create an actual memory and gallery unlock, without direct flags.
            Morning();for(int i=0;i<2;i++){Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n01");Do(CommandKind.Abandon);Morning();}
            p.SaveManual();Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n01");Do(CommandKind.Abandon);p.LoadSlot("manual",LoadIntent.ManualLoad);
            Check(p.Session.CaptureMeta().SpecialMemoryIds.Contains("memory_c1"),"地圖版指定失敗回退取得記憶");
            p.OpenWindow("Memory");yield return Shot("12-memory");yield return KeyPress(Key.Escape);
            string before=JsonUtility.ToJson(p.Session.Snapshot);p.OpenGallery();yield return Click("PlayGallery");yield return Shot("13-gallery");p.CloseGallery();Check(JsonUtility.ToJson(p.Session.Snapshot)==before,"畫廊不改旅程");
            for(int i=0;i<2;i++){Do(CommandKind.Work);p.LoadSlot("manual",LoadIntent.ManualLoad);}
            Do(CommandKind.AdultWork);Do(CommandKind.AdultWork);p.OpenWindow("Memory");yield return Shot("13a-branch-entry");
            Check(RouteRules.CanCorrupt(p.Session.CaptureRun(),p.Session.CaptureMeta(),p.Data),"正式遊玩條件達成重大轉變候選");
            p.Execute(new GameCommand(CommandKind.AcceptRoute));yield return Shot("13b-branch-warning");yield return Click("ModalAccept");
            Check(p.Session.CaptureRun().Route==Route.Corrupted&&p.Session.CaptureRun().PlayerAware,"新版重大分歧經確認感知再鎖線");
            Morning();Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n09");Do(CommandKind.EnterDeep);yield return Shot("13c-corrupted-skill");
            for(int i=0;i<8&&p.Session.CaptureRun().Location==GameLocation.Battle;i++)Do(CommandKind.Skill,"","h_corrupt");
            Check(p.Session.CaptureRun().Battle.Outcome==BattleOutcome.HVictory,"魔化群體技能正常勝利");p.LoadSlot("manual",LoadIntent.ManualLoad);
            Check(p.Session.CaptureRun().Route==Route.Unbound&&!p.Session.CaptureRun().PlayerAware,"讀回分歧前不立即重鎖");
            Do(CommandKind.RestToChapterEnd);p.SaveManual();Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c2_n01");Do(CommandKind.Abandon);p.LoadSlot("manual",LoadIntent.ManualLoad);Check(p.Session.CaptureMeta().SpecialMemoryIds.Count==2,"第二章記憶仍正常");
            Do(CommandKind.CompleteDemo);yield return Shot("14-complete");
            // Invalidate an active animation by switching journeys, then wait beyond its lifetime.
            p.NewGame();Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n01");p.Execute(new GameCommand(CommandKind.Attack,p.Session.CaptureRun().Battle.Enemies[0].InstanceId));p.GoMainMenu();p.NewGame();
            var fresh=JsonUtility.ToJson(p.Session.Snapshot);yield return new WaitForSeconds(.7f);Check(JsonUtility.ToJson(p.Session.Snapshot)==fresh&&!p.Busy,"舊動畫不能改寫新旅程");
            Do(CommandKind.Depart);Do(CommandKind.EnterNode,"c1_n01");for(int i=0;i<70&&!p.Session.CaptureRun().Battle.AwaitingKnockdownChoice;i++)Do(CommandKind.Defend);Do(CommandKind.AcceptDeep);HWin();Check(p.Session.CaptureRun().Hp==0,"零 HP H 勝不補血");yield return Shot("15-zero-hp");
            yield return Click("SafeReturn");Check(p.Session.CaptureRun().Location==GameLocation.Town,"零 HP 安全返城按鈕可用");Morning();p.SaveManual();p.Repository.Persist(p.Session);report.Checks.Add("流程含真實鍵盤行走／UI點擊，後段規則走查使用正式命令");
        }
        private IEnumerator Resume()
        {
            var previous=p.Repository.ReadSlot("auto").Run;yield return Shot("01-restart");yield return Click("Continue");
            Check(p.Session.CaptureRun().CampaignId==previous.CampaignId,"獨立程序恢復同旅程");Check(JsonUtility.ToJson(p.Session.CaptureRun().World)==JsonUtility.ToJson(previous.World),"獨立程序恢復安全位置");
            Check(p.Session.CaptureMeta().EffectiveRewinds==0,"正常重啟繼續不增加回退");
            yield return Shot("02-resumed");p.OpenGallery();yield return Click("PlayGallery");Check(p.GalleryVisible,"重啟後畫廊可播");
        }
    }
}
