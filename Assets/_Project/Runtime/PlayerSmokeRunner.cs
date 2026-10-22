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
    public sealed class PlayerSmokeRunner : MonoBehaviour
    {
        [Serializable] private class SmokeReport
        {
            public string Build = "0.1.0", StartedUtc, FinishedUtc, Status = "running", Error = "", SaveRoot;
            public int Width, Height;
            public string Method = "真實 Windows Player 中的正式 UI 事件及遊戲命令自動走查；不等同人工滑鼠點擊。";
            public List<string> Checks = new List<string>();
        }
        private DemoPresenter presenter;
        private string directory;
        private SmokeReport report;
        private IEnumerator Start()
        {
            yield return null;
            presenter = GetComponent<DemoPresenter>();
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--verification-dir");
            directory = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.GetFullPath(Path.Combine(Application.dataPath, "../verification"));
            Directory.CreateDirectory(Path.Combine(directory, "screenshots"));
            report = new SmokeReport { StartedUtc = DateTime.UtcNow.ToString("o"), SaveRoot = presenter.SaveRoot, Width = Screen.width, Height = Screen.height };
            var routines = new Stack<IEnumerator>(); routines.Push(args.Contains("--demo-resume-smoke") ? ResumeScenario() : Scenario());
            while (routines.Count > 0)
            {
                object current;
                try
                {
                    var routine = routines.Peek();
                    if (!routine.MoveNext()) { routines.Pop(); continue; }
                    current = routine.Current;
                }
                catch (Exception e) { report.Error = e.ToString(); Debug.LogException(e); break; }
                if (current is IEnumerator nested) { routines.Push(nested); continue; }
                yield return current;
            }
            report.FinishedUtc = DateTime.UtcNow.ToString("o"); report.Status = report.Error == "" ? "passed" : "failed";
            File.WriteAllText(Path.Combine(directory, "player-smoke.json"), JsonUtility.ToJson(report, true));
            Debug.Log("HGAME_PLAYER_SMOKE_" + report.Status.ToUpperInvariant());
            yield return new WaitForSeconds(0.2f); Application.Quit(report.Error == "" ? 0 : 1);
        }
        private IEnumerator Shot(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "screenshots/" + name + ".png"));
            yield return new WaitForSeconds(0.15f);
        }
        private void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); report.Checks.Add(message); }
        private IEnumerator ClickButton(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var button = FindObjectsByType<Button>(FindObjectsSortMode.None).First(x => x.name == name);
            var rect = (RectTransform)button.transform;
            var position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 0 }); yield return new WaitForSeconds(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); yield return new WaitForSeconds(0.05f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 0 }); yield return new WaitForSeconds(0.05f);
            report.Checks.Add("InputSystem 合成指標點擊 UI：" + name);
        }
        private void Do(CommandKind kind, string target = "", string choice = "")
        {
            var result = presenter.RunCommand(new GameCommand(kind, target, choice));
            if (!result.Success) throw new InvalidOperationException(kind + ": " + result.Message);
        }
        private void Morning()
        {
            for (int i = 0; i < 4 && presenter.Session.Snapshot.Run.Slot != TimeSlot.Morning; i++)
                Do(presenter.Session.Snapshot.Run.Slot == TimeSlot.LateNight ? CommandKind.Rest : CommandKind.Wait);
        }
        private void Lose()
        { Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n01"); Do(CommandKind.Abandon); Morning(); }
        private void HWin()
        {
            for (int i = 0; i < 20 && presenter.Session.Snapshot.Run.Location == GameLocation.Battle; i++)
            {
                var enemy = presenter.Session.Snapshot.Run.Battle.Enemies.First(x => x.Stamina > 0);
                Do(CommandKind.Skill, enemy.InstanceId, "h_touch");
            }
            Check(presenter.Session.Snapshot.Run.Location != GameLocation.Battle, "H 戰鬥完成");
        }
        private void NormalWin()
        {
            for (int i = 0; i < 20 && presenter.Session.Snapshot.Run.Location == GameLocation.Battle; i++)
            {
                var enemy = presenter.Session.Snapshot.Run.Battle.Enemies.First(x => x.Hp > 0);
                Do(CommandKind.Attack, enemy.InstanceId);
            }
            Check(presenter.Session.Snapshot.Run.Location != GameLocation.Battle, "普通戰鬥完成");
        }
        private void DanceWin()
        {
            for (int i = 0; i < 12 && presenter.Session.Snapshot.Run.Location == GameLocation.Battle; i++) Do(CommandKind.Skill, "", "dance");
            Check(presenter.Session.Snapshot.Run.Battle.Outcome == BattleOutcome.StunVictory, "舞者以全體暈眩勝利");
        }
        private IEnumerator Scenario()
        {
            yield return Shot("01-main-menu");
            yield return ClickButton("NewGame"); yield return Shot("02-town");
            Check(presenter.Session != null, "主選單 UI 事件建立旅程");
            Lose(); Lose(); presenter.SaveManual();
            Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n01"); Do(CommandKind.Abandon);
            presenter.LoadSlot("manual", LoadIntent.ManualLoad);
            Check(presenter.Session.Snapshot.Meta.SpecialMemoryIds.Contains("memory_c1"), "指定失敗讀檔接收第一章記憶");
            for (int i = 0; i < 2; i++) { Do(CommandKind.Work); presenter.LoadSlot("manual", LoadIntent.ManualLoad); }
            Check(presenter.Session.Snapshot.Meta.EffectiveRewinds == 3, "三次有效回退持久化");
            presenter.LoadSlot("manual", LoadIntent.ManualLoad);
            Check(presenter.Session.Snapshot.Meta.EffectiveRewinds == 3, "相同節點不重複計數");
            Do(CommandKind.AdultWork); Morning();
            Do(CommandKind.Depart); yield return Shot("03-dungeon");
            Do(CommandKind.EnterNode, "c1_n01"); Do(CommandKind.EnterDeep); yield return Shot("04-deep-battle"); HWin();
            Do(CommandKind.EnterNode, "c1_n02"); Do(CommandKind.EnterNode, "c1_n03"); Do(CommandKind.ChooseSupply, "", "take"); Do(CommandKind.EnterNode, "c1_n04");
            Check(!presenter.Session.Execute(new GameCommand(CommandKind.EnterNode, "c1_n05")).Success, "單趟三分之一限制");
            Do(CommandKind.ReturnTown); Morning(); Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n05"); NormalWin();
            Do(CommandKind.EnterNode, "c1_n06"); Do(CommandKind.EnterNode, "c1_n07"); Do(CommandKind.ChooseSupply, "", "take"); Do(CommandKind.EnterNode, "c1_n08"); Do(CommandKind.ReturnTown);
            presenter.SaveManual();
            presenter.Execute(new GameCommand(CommandKind.AcceptRoute)); yield return Shot("05-branch-warning");
            yield return ClickButton("ModalAccept");
            Check(presenter.Session.Snapshot.Run.Route == Route.Corrupted && presenter.Session.Snapshot.Run.PlayerAware, "正式確認分歧後感知再鎖線");
            yield return Shot("06-route-changed");
            Morning(); Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n09"); Do(CommandKind.EnterDeep);
            Do(CommandKind.Skill, "", "h_corrupt"); yield return Shot("06a-corrupted-skill");
            for (int i = 0; i < 5 && presenter.Session.Snapshot.Run.Location == GameLocation.Battle; i++) Do(CommandKind.Skill, "", "h_corrupt");
            Check(presenter.Session.Snapshot.Run.Battle.Outcome == BattleOutcome.HVictory, "魔化專屬技能以全體 H 效果正常獲勝");
            presenter.LoadSlot("manual", LoadIntent.ManualLoad);
            Check(presenter.Session.Snapshot.Run.Route == Route.Unbound && !presenter.Session.Snapshot.Run.PlayerAware, "讀回分歧前不立即重鎖");
            Morning(); Do(CommandKind.SwitchJob, "Dancer"); Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n09");
            yield return Shot("07-dancer-multiple"); DanceWin(); Do(CommandKind.EnterNode, "c1_n10"); Do(CommandKind.EnterNode, "c1_n11");
            yield return Shot("08-dancer-boss"); DanceWin(); Do(CommandKind.EnterNode, "c1_n12"); Do(CommandKind.ReturnTown);
            Check(presenter.Session.Snapshot.Run.Day == 6, "第六天可完成 Boss");
            var before = JsonUtility.ToJson(presenter.Session.Snapshot);
            yield return ClickButton("Gallery"); yield return ClickButton("PlayGallery");
            yield return Shot("09-gallery"); presenter.CloseGallery();
            Check(JsonUtility.ToJson(presenter.Session.Snapshot) == before, "畫廊前後完整主線與記憶一致");
            Do(CommandKind.RestToChapterEnd); Check(presenter.Session.Snapshot.Run.Chapter == 2, "章末結算進入第二章");
            presenter.SaveManual(); Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c2_n01"); Do(CommandKind.Abandon); presenter.LoadSlot("manual", LoadIntent.ManualLoad);
            Check(presenter.Session.Snapshot.Meta.SpecialMemoryIds.Count == 2, "第二章記憶獨立觸發"); yield return Shot("10-second-chapter-memory");
            Do(CommandKind.CompleteDemo); yield return Shot("11-demo-complete");

            presenter.NewGame(); Do(CommandKind.Depart); Do(CommandKind.EnterNode, "c1_n01");
            for (int i = 0; i < 60 && !presenter.Session.Snapshot.Run.Battle.AwaitingKnockdownChoice; i++) Do(CommandKind.Defend);
            Check(presenter.Session.Snapshot.Run.Battle.AwaitingKnockdownChoice, "敵方真實擊倒觸發續戰選擇");
            yield return Shot("12-knockdown-choice"); Do(CommandKind.AcceptDeep); HWin();
            Check(presenter.Session.Snapshot.Run.Hp == 0, "零 HP H 勝後不免費補血"); yield return Shot("13-zero-hp-recovery");
            Check(!presenter.Session.Execute(new GameCommand(CommandKind.EnterNode, "c1_n02")).Success, "零 HP 不能深入探索");
            Do(CommandKind.ReturnTown); Morning();
            presenter.SaveManual(); string campaign = presenter.Session.Snapshot.Run.CampaignId;
            var restarted = new SaveRepository(presenter.Data, new DiskSaveStore(presenter.SaveRoot)); string msg;
            var resumed = restarted.Load(null, "auto", LoadIntent.Continue, out msg);
            Check(resumed.Snapshot.Run.CampaignId == campaign && resumed.Snapshot.Meta.EffectiveRewinds == 0, "磁碟重新載入 Continue 不增加回退");
            report.Checks.Add("已走查普通/H/舞者/Boss/兩章記憶/分歧/畫廊/零HP；操作為自動命令與UI事件");
        }
        private IEnumerator ResumeScenario()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--expected-campaign");
            if (at < 0 || at + 1 >= args.Length) throw new InvalidOperationException("冷啟動驗證缺少旅程識別。");
            string expected = args[at + 1];
            var previous = presenter.Repository.ReadSlot("auto").Run;
            Check(previous.CampaignId == expected, "冷啟動前磁碟存檔為上一程序的旅程");
            yield return Shot("01-restart-menu"); yield return ClickButton("Continue"); yield return Shot("02-restarted-journey");
            Check(presenter.Session != null && presenter.Session.Snapshot.Run.CampaignId == expected, "另一個 Player 程序以繼續恢復同一旅程");
            Check(presenter.Session.Snapshot.Meta.EffectiveRewinds == 0, "真實關閉重啟的 Continue 不增加回退");
            yield return ClickButton("Gallery"); yield return ClickButton("PlayGallery");
            Check(presenter.GalleryVisible, "重啟後回想解鎖仍可播放");
            presenter.CloseGallery();
        }
    }
}
