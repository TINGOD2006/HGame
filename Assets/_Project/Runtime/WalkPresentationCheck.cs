using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace HGame.Unity
{
    // Opt-in regression for held input across grid boundaries, using the real Player input path.
    public sealed class WalkPresentationCheck : MonoBehaviour
    {
        [Serializable] private class Report
        {
            public string Build="0.2.2",Status="running",Error="",Method="Windows Player 合成鍵盤輸入；每幀量測角色位置／角度／步態。",SaveRoot;
            public int Width,Height; public List<string> Checks=new List<string>(); public List<Sample> Samples=new List<Sample>();
        }
        [Serializable] private class Sample
        { public float Time,Dt,X,Y,Phase,Angle;public int Frame,GridX,GridY;public bool Moving; }
        private Report report;private JrpgPresenter p;private string directory;private Keyboard keyboard;
        private IEnumerator Start()
        {
            yield return null;p=GetComponent<JrpgPresenter>();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--verification-dir");
            directory=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"WalkCheck");Directory.CreateDirectory(directory);
            report=new Report{Width=Screen.width,Height=Screen.height,SaveRoot=p.SaveRoot};
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);
            var routines=new Stack<IEnumerator>();routines.Push(Scenario());
            while(routines.Count>0)
            {
                object value;try{var routine=routines.Peek();if(!routine.MoveNext()){routines.Pop();continue;}value=routine.Current;}catch(Exception e){report.Error=e.ToString();Debug.LogException(e);break;}
                if(value is IEnumerator nested){routines.Push(nested);continue;}yield return value;
            }
            Keys();report.Status=report.Error==""?"passed":"failed";File.WriteAllText(Path.Combine(directory,"walk-check.json"),JsonUtility.ToJson(report,true));
            yield return new WaitForSeconds(.2f);Application.Quit(report.Error==""?0:1);
        }
        private void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);report.Checks.Add(message);}
        private void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
        private IEnumerator Shot(string name)
        {yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSeconds(.15f);}
        private IEnumerator Release()
        {Keys();for(int i=0;i<45;i++){yield return null;if(!p.World.IsMoving)break;}yield return new WaitForEndOfFrame();}
        private IEnumerator Hold(Key key,float seconds,bool record=false)
        {
            Keys(key);float elapsed=0;
            while(elapsed<seconds)
            {
                yield return new WaitForEndOfFrame();elapsed+=Time.deltaTime;
                if(record)
                {
                    var pos=p.World.HeroWorldPosition;
                    report.Samples.Add(new Sample{Time=elapsed,Dt=Time.deltaTime,X=pos.x,Y=pos.y,Phase=p.World.WalkPhase,Angle=Quaternion.Angle(Quaternion.identity,p.World.HeroRotation),Frame=p.World.CurrentWalkFrame,GridX=p.World.Position.x,GridY=p.World.Position.y,Moving=p.World.IsMoving});
                }
                yield return null;
            }
        }
        private IEnumerator Scenario()
        {
            p.NewGame();yield return null;yield return Shot("01-town-transparent-portrait");
            p.World.SetGridPosition(18,13,1);string head=p.Session.CaptureRun().TimelineHeadId;string meta=JsonUtility.ToJson(p.Session.CaptureMeta());
            yield return Hold(Key.D,.9f,true);yield return Release();
            var active=report.Samples.Where(s=>s.Moving).ToList();Check(active.Count>=8,"持續輸入取得足夠跨格樣本");
            Check(active.Max(s=>Mathf.Abs(s.Y+13))<.0001f,"橫向行走沒有上下晃動");Check(active.Max(s=>s.Angle)<.0001f,"行走角度保持 0，沒有旋轉搖擺");
            Check(active.Max(s=>s.GridX)-active.Min(s=>s.GridX)>=3,"持續按住跨越至少三個格子");
            for(int i=1;i<active.Count;i++)CheckMotion(active[i-1],active[i]);
            Check(true,"跨格沒有停頓、倒退或速度重置");
            var frames=active.Select(s=>s.Frame).Distinct().ToList();Check(frames.Contains(0)&&frames.Contains(1)&&frames.Contains(2),"步態包含左踏步、經過站姿、右踏步");
            bool continuousBoundary=false;for(int i=1;i<active.Count;i++)if(active[i].GridX!=active[i-1].GridX&&active[i].Phase>.3f)continuousBoundary=true;
            Check(continuousBoundary,"步態相位跨越格子邊界持續累積");
            Check(!p.World.IsMoving&&p.World.CurrentWalkFrame==1,"放開方向後在完整格子站定");
            Check(Vector3.Distance(p.World.HeroWorldPosition,new Vector3(p.World.Position.x,-p.World.Position.y,0))<.0001f,"停步位置與存檔格子一致");
            Check(p.Session.CaptureRun().TimelineHeadId==head&&JsonUtility.ToJson(p.Session.CaptureMeta())==meta,"連續行走不增加時間線或回退");
            p.World.SetGridPosition(18,13,1);yield return Hold(Key.D,.21f);Keys(Key.W);yield return Hold(Key.W,.25f);yield return Release();
            Check(p.World.Position.x>=19&&p.World.Position.y<=12,"連續輸入轉向正常銜接");
            p.World.SetGridPosition(18,13,1);Keys(Key.D);yield return null;yield return new WaitForEndOfFrame();
            Check(p.World.IsMoving&&!p.Repository.CanSave(p.Session.Snapshot),"半格移動期間不能存檔");
            p.OpenWindow("Menu");var frozen=p.World.HeroWorldPosition;yield return new WaitForSeconds(.18f);
            Check(Vector3.Distance(p.World.HeroWorldPosition,frozen)<.0001f,"開啟選單時移動暫停且不跳格");
            p.OpenWindow("");yield return Hold(Key.D,.18f);yield return Release();Check(!p.World.IsMoving,"關閉選單後可繼續並停步");
            p.World.SetGridPosition(1,13,3);yield return Hold(Key.A,.3f);yield return Release();Check(p.World.Position.x==1,"持續輸入撞牆不能穿越邊界");
            p.World.SetGridPosition(18,13,1);Keys(Key.D);yield return null;yield return new WaitForEndOfFrame();
            p.GoMainMenu();p.NewGame();Keys();yield return new WaitForSeconds(.2f);Check(p.World.Position==new Vector2Int(18,13)&&!p.World.IsMoving,"另開旅程不延續舊位移");
            Check(GameObject.Find("PortraitBackground")==null&&GameObject.Find("PortraitAccent")==null,"立繪後方沒有獨立底板與分隔框");
            Check(Mathf.Abs(Camera.main.rect.width-1)<.001f,"世界場景延伸至整個視窗");
            var screenPoint=Camera.main.WorldToViewportPoint(p.World.HeroWorldPosition);Check(Mathf.Abs(screenPoint.x-.375f)<.02f,"玩家保持在左側操作區中央");
            var args=Environment.GetCommandLineArgs();int clipAt=Array.IndexOf(args,"--walk-clip");
            if(clipAt>=0&&clipAt+1<args.Length)
            {
                string framesPath=args[clipAt+1];Directory.CreateDirectory(framesPath);Time.captureFramerate=30;
                for(int frame=0;frame<64;frame++)
                {
                    Keys(frame<28?Key.D:frame<56?Key.A:Key.None);
                    yield return null;yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(framesPath,"walk-"+frame.ToString("D3")+".png"));
                }
                Time.captureFramerate=0;yield return Release();
            }
            p.OpenWindow("Shop");yield return Shot("02-shop-transparent-portrait");p.OpenWindow("");
            p.RunCommand(new GameCommand(CommandKind.Depart));yield return Shot("03-dungeon-transparent-portrait");
            p.RunCommand(new GameCommand(CommandKind.EnterNode,"c1_n01"));yield return Shot("04-battle-transparent-portrait");
        }
        private void CheckMotion(Sample before,Sample after)
        {
            float distance=after.X-before.X,dt=after.Time-before.Time;
            if(distance<.0001f || Mathf.Abs(distance/dt-p.World.GridSpeed)>.15f)throw new InvalidOperationException("持續行走速度異常："+distance+" / "+dt);
        }
    }
}
