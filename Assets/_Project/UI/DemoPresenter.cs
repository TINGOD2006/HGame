using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HGame.Unity
{
    public sealed class DemoPresenter : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public GameData Data { get; private set; }
        public SaveRepository Repository { get; private set; }
        public string SaveRoot { get; private set; }
        public bool MainMenuVisible { get { return mainMenu; } }
        public bool GalleryVisible { get { return galleryVisible; } }
        private RectTransform canvasRoot, hero, main, journal, footer, modal;
        private TextMeshProUGUI dateText, statusText;
        private bool mainMenu = true, galleryVisible;
        private string status = "準備出發，或在城鎮停留一會。", selectedEnemy = "";
        private GallerySession gallery;
        private ProfileState profile;

        private void Start()
        {
            try
            {
                UiKit.Font = Resources.Load<TMP_FontAsset>("Fonts/DemoFont");
                if (UiKit.Font == null) throw new InvalidOperationException("缺少 Demo 繁中字型，請先執行 Editor 設定。");
                Data = DefinitionLoader.Load(); SaveRoot = ResolveSaveRoot();
                Repository = new SaveRepository(Data, new DiskSaveStore(SaveRoot)); profile = Repository.LoadProfile();
                BuildShell(); Refresh();
                if (Environment.GetCommandLineArgs().Contains("--demo-smoke") || Environment.GetCommandLineArgs().Contains("--demo-resume-smoke")) gameObject.AddComponent<PlayerSmokeRunner>();
            }
            catch (Exception e) { Debug.LogException(e); BuildShell(); ShowModal("啟動失敗", e.Message, "離開", () => Application.Quit(1)); }
        }
        private static string ResolveSaveRoot()
        {
            string root = Path.Combine(Application.persistentDataPath, "HGameDemo");
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--demo-profile");
            if (at >= 0 && at + 1 < args.Length)
            {
                string name = args[at + 1];
                if (name.Length == 0 || name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) throw new ArgumentException("驗證資料名稱不合法。");
                root = Path.Combine(root, "Verification", name);
            }
            return root;
        }
        private void BuildShell()
        {
            if (canvasRoot != null) return;
            var canvasObject = new GameObject("DemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = (RectTransform)canvasObject.transform;
            var bg = UiKit.PanelAt("Background", canvasRoot, 0, 0, 1280, 720, UiKit.Background); UiKit.Stretch(bg.rectTransform);
            UiKit.PanelAt("AccentLine", canvasRoot, 0, 0, 1280, 4, UiKit.Teal);
            UiKit.Text("迷宮與她的夜晚", canvasRoot, 24, 15, 530, 38, 27);
            UiKit.Text("SYSTEM DEMO  /  一段尚未接起的記憶", canvasRoot, 26, 52, 650, 24, 14, UiKit.Muted);
            dateText = UiKit.Text("UNITY 6.3 · WINDOWS", canvasRoot, 910, 28, 345, 30, 17, UiKit.Gold); dateText.alignment = TextAlignmentOptions.Right;
            hero = UiKit.PanelAt("HeroPanel", canvasRoot, 20, 92, 246, 512, UiKit.Panel).rectTransform;
            main = UiKit.PanelAt("MainPanel", canvasRoot, 280, 92, 720, 512, UiKit.Panel).rectTransform;
            journal = UiKit.PanelAt("JournalPanel", canvasRoot, 1014, 92, 246, 512, UiKit.Panel).rectTransform;
            footer = UiKit.PanelAt("Toolbar", canvasRoot, 20, 618, 1240, 65, UiKit.Panel).rectTransform;
            statusText = UiKit.Text(status, canvasRoot, 25, 691, 1230, 24, 15, UiKit.Muted);
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        public void NewGame()
        {
            Safe(() => { profile = Repository.LoadProfile(); Session = GameSession.NewGame(Data, null, profile);
                Repository.Persist(Session); mainMenu = false; galleryVisible = false; status = "旅程開始。先選擇職業，再安排今天的行動。"; Refresh(); });
        }
        public void Execute(GameCommand command)
        {
            if (Session == null) return;
            if (command.Kind == CommandKind.AcceptRoute)
            {
                ShowModal("接受重大轉變？", Data.Event("route_branch").Body, "確認轉變", () => RunCommand(command), "維持現狀", () => RunCommand(new GameCommand(CommandKind.DeclineRoute))); return;
            }
            if (command.Kind == CommandKind.AdultWork)
            {
                ShowModal("成人工作 · 狀態結算", Data.Event("activity_adult").Body + "\n\n報酬：25 金、10 H EXP。消耗一個時段。", "確認", () => RunCommand(command), "返回", null); return;
            }
            if (command.Kind == CommandKind.RestToChapterEnd)
            {
                ShowModal("休整至章末？", "將用完本章剩餘行動，沒有額外金錢與 EXP。你也可以返回，繼續逐日安排活動。", "確認休整", () => RunCommand(command), "返回", null); return;
            }
            RunCommand(command);
        }
        public CommandResult RunCommand(GameCommand command)
        {
            if (Session == null) return CommandResult.Reject("尚未開始旅程。");
            var result = Session.Execute(command); status = result.Message;
            Safe(() => { Repository.Persist(Session, result.RequestAutosave); profile = Session.CaptureProfile(); });
            Refresh(); return result;
        }
        public void SaveManual()
        {
            Safe(() => { if (Session == null) return; Repository.Save(Session, "manual"); status = "已寫入手動存檔。"; Refresh(); });
        }
        public void LoadSlot(string slot, LoadIntent intent)
        {
            Safe(() => { string message; Session = Repository.Load(Session, slot, intent, out message);
                profile = Session.CaptureProfile(); mainMenu = false; galleryVisible = false; status = message; Refresh(); });
        }
        public void OpenGallery()
        {
            Safe(() => { var source = Session != null ? Session.CaptureProfile() : profile;
                gallery = new GallerySession(source ?? new ProfileState()); galleryVisible = true; Refresh(); });
        }
        public void CloseGallery() { gallery?.Close(); galleryVisible = false; Refresh(); }
        public void Refresh()
        {
            if (main == null) return;
            UiKit.Clear(hero); UiKit.Clear(main); UiKit.Clear(journal); UiKit.Clear(footer);
            RenderHero(); RenderJournal(); RenderToolbar();
            if (galleryVisible) RenderGallery();
            else if (mainMenu || Session == null) RenderMenu();
            else
            {
                var snapshot = Session.Snapshot;
                dateText.text = "第 " + snapshot.Run.Chapter + " 章 · 第 " + snapshot.Run.Day + " / 20 天 · " + SlotName(snapshot.Run.Slot);
                switch (snapshot.Run.Location)
                {
                    case GameLocation.Town: RenderTown(); break;
                    case GameLocation.Dungeon: RenderDungeon(); break;
                    case GameLocation.Battle: RenderBattle(); break;
                    case GameLocation.GameOver: RenderEnd(false); break;
                    case GameLocation.DemoComplete: RenderEnd(true); break;
                }
            }
            if (statusText != null) statusText.text = status;
        }
        private static string SlotName(TimeSlot slot)
        { return new[] { "早上", "中午", "晚上", "深夜" }[(int)slot]; }
        private void RenderHero()
        {
            UiKit.Text("旅人  /  成年女主", hero, 16, 15, 216, 30, 20, UiKit.Gold);
            var artFrame = UiKit.PanelAt("Portrait", hero, 14, 56, 218, 285, UiKit.PanelLight);
            UiKit.Artwork(artFrame.transform, 0, 0, 218, 285);
            UiKit.Text("暫定概念造型 · 22 歲以上", hero, 16, 349, 216, 22, 13, UiKit.Muted);
            if (Session == null)
            {
                UiKit.Text("迷宮探索 × 日程養成\n回合制戰鬥 × 記憶殘留", hero, 16, 386, 216, 92, 19);
                return;
            }
            var run = Session.Snapshot.Run;
            UiKit.Bar(hero, 16, 413, 214, "HP", run.Hp, run.MaxHp, UiKit.Teal);
            UiKit.Bar(hero, 16, 461, 214, "MP", run.Mp, run.MaxMp, UiKit.Pink);
            UiKit.Text((run.Job == Job.Dancer ? "舞者" : "劍士") + "  ·  普通 Lv." + (1 + run.NormalExp / Data.Balance.ExpPerLevel) + "  H Lv." + (1 + run.HExp / Data.Balance.ExpPerLevel), hero, 16, 477, 218, 24, 14, UiKit.Gold);
        }
        private void RenderJournal()
        {
            UiKit.Text("旅程手帳", journal, 18, 18, 210, 30, 22, UiKit.Gold);
            UiKit.PanelAt("Rule", journal, 18, 59, 210, 2, UiKit.PanelLight);
            string body;
            if (Session == null) body = "每章二十天。\n\n早上決定是否遠征，晚上回到城鎮。\n\n留下來工作、準備補給，或選擇另一種成長。\n\n某些經歷，似乎沒有隨重來而消失。";
            else
            {
                var run = Session.Snapshot.Run;
                body = "金錢  " + run.Gold + "\n善惡  " + run.Virtue + "\n今日 H 額度  " + run.HDailyUsesRemaining + "\n\n";
                body += string.Join("\n\n", run.Journal.Skip(Math.Max(0, run.Journal.Count - 5)).Reverse().ToArray());
            }
            UiKit.Text(body, journal, 18, 80, 210, 404, 16, UiKit.Muted).overflowMode = TextOverflowModes.Ellipsis;
        }
        private void RenderToolbar()
        {
            bool active = Session != null;
            UiKit.Button("SaveManual", "手動存檔", footer, 10, 10, 145, 45, SaveManual, active && Repository != null && Repository.CanSave(Session.Snapshot));
            UiKit.Button("LoadManual", "讀取手動存檔", footer, 165, 10, 170, 45, () => ConfirmLoad("manual"), Repository != null && Repository.HasSlot("manual"));
            UiKit.Button("LoadAuto", "讀取自動存檔", footer, 345, 10, 170, 45, () => ConfirmLoad("auto"), Repository != null && Repository.HasSlot("auto"));
            UiKit.Button("Gallery", "回想畫廊", footer, 525, 10, 140, 45, OpenGallery, Repository != null);
            UiKit.Button("Help", "操作說明", footer, 675, 10, 140, 45, ShowHelp);
            UiKit.Button("MainMenu", "主選單", footer, 825, 10, 140, 45, () => { mainMenu = true; galleryVisible = false; Refresh(); });
            UiKit.Button("Quit", "離開", footer, 975, 10, 140, 45, () => ShowModal("離開試玩？", "將保存最近的安全進度。未結束的戰鬥不寫入可載入存檔。", "離開", () => { if (Session != null) Repository.Persist(Session); Application.Quit(); }, "返回", null));
        }
        private void RenderMenu()
        {
            dateText.text = "UNITY 6.3 · WINDOWS";
            UiKit.Text("A NIGHT IN THE LABYRINTH", main, 30, 24, 650, 30, 16, UiKit.Teal);
            UiKit.Text("每一次出發，\n都會留下些什麼。", main, 30, 77, 650, 126, 37);
            UiKit.Text("探索、養成與戰鬥的系統原型\n第一章可玩循環＋第二章記憶驗證段落", main, 33, 217, 630, 64, 19, UiKit.Muted);
            UiKit.Button("NewGame", "開始新的旅程", main, 32, 313, 310, 57, () =>
            {
                if (Session != null) ShowModal("開始新的旅程？", "新旅程的記憶獨立保存。原有手動存檔仍可載入。", "開始", NewGame, "返回", null);
                else NewGame();
            }, true, UiKit.Hex("38645F"));
            UiKit.Button("Continue", "繼續旅程", main, 32, 385, 310, 57, () =>
            {
                if (Session != null) { mainMenu = false; Refresh(); }
                else LoadSlot("auto", LoadIntent.Continue);
            }, Session != null || Repository != null && Repository.HasSlot("auto"));
            UiKit.Text("所有角色及參與者均為成年人。\n本版以狀態結算與占位素材呈現。", main, 376, 330, 304, 110, 17, UiKit.Muted);
            UiKit.Text("DEMO BUILD  /  暫定數值與玩法驗證", main, 33, 470, 640, 22, 13, UiKit.Gold);
        }
        private void Header(string title, string subtitle)
        { UiKit.Text(title, main, 24, 18, 670, 38, 27); UiKit.Text(subtitle, main, 26, 60, 668, 42, 16, UiKit.Muted); }
        private void RenderTown()
        {
            var run = Session.Snapshot.Run;
            string hint = run.Slot == TimeSlot.LateNight ? "深夜只開放室內休息與記錄。" : "工作、補給或出發——今天的時間由你安排。";
            if (run.Chapter == 1 && run.Day == 1) hint = "教學 1／2：在城鎮選擇職業，早上遠征、晚上返城。深夜休息後進入次日。";
            else if (run.Chapter == 1 && run.Day == 2) hint = "教學 2／2：工作和購物會使用時段，請安排補給。第二階段在第 3 天開放。";
            Header("城鎮 · 探索前的片刻", hint);
            RenderActions(Session.GetAvailableActions(), 24, 113, 675, 375, 325, 70);
        }
        private void RenderActions(IEnumerable<ActionAvailability> actions, float x, float y, float w, float h, float cellW = 325, float cellH = 70)
        {
            var content = UiKit.GridScroll(main, x, y, w, h, cellW, cellH);
            foreach (var action in actions)
            {
                var entry = action;
                string label = entry.Label + (entry.Enabled || string.IsNullOrEmpty(entry.Reason) ? "" : "\n<size=12>" + entry.Reason + "</size>");
                UiKit.Button("Action_" + entry.Id, label, content, 0, 0, cellW, cellH, () => Execute(entry.Command), entry.Enabled,
                    entry.Command.Kind == CommandKind.AcceptRoute ? UiKit.Hex("69435E") : (Color?)null);
            }
        }
        private void RenderDungeon()
        {
            var snap = Session.Snapshot; var run = snap.Run; var actions = Session.GetAvailableActions();
            Header("迷宮 · 第 " + run.Chapter + " 章", "第 1／3／6 天開放各階段。單趟新增進度 " + run.ExpeditionNewNodes + " / " + Data.Balance.MaxNewNodes + "。");
            var progress = run.Chapters.FirstOrDefault(x => x.Chapter == run.Chapter);
            var nodes = Data.Nodes.Where(x => x.Chapter == run.Chapter).OrderBy(x => x.Index).ToList();
            foreach (var node in nodes)
            {
                int index = node.Index - 1, row = index / 4, col = index % 4;
                var action = actions.FirstOrDefault(x => x.Command.Kind == CommandKind.EnterNode && x.Command.TargetId == node.Id);
                bool cleared = progress != null && progress.ClearedNodeIds.Contains(node.Id);
                var item = node;
                string type = node.Kind == "Save" ? "存檔點" : node.Kind == "Supply" ? "補給" : node.IsBoss ? "BOSS" : "戰鬥";
                var button = UiKit.Button("Node_" + node.Id, node.Index.ToString("00") + "  " + type + (cleared ? "  ✓" : "") + "\n<size=14>" + node.Name + "</size>",
                    main, 24 + col * 169, 119 + row * 94, 157, 78,
                    () => Execute(new GameCommand(CommandKind.EnterNode, item.Id)), action != null && action.Enabled,
                    run.CurrentNodeId == node.Id ? UiKit.Hex("38645F") : cleared ? UiKit.Hex("2B3E48") : (Color?)null);
            }
            if (run.Hp == 0) UiKit.Text("HP 為 0：請使用恢復品，或選擇安全返城。", main, 25, 392, 660, 30, 16, UiKit.Pink);
            RenderActions(actions.Where(x => x.Command.Kind != CommandKind.EnterNode), 24, 426, 676, 67, 325, 58);
        }
        private void RenderBattle()
        {
            var run = Session.Snapshot.Run; var battle = run.Battle;
            if (battle == null) { Header("戰鬥", "戰鬥資料尚未就緒。"); return; }
            bool deep = battle.Phase == BattlePhase.Deep;
            Header(deep ? "深度戰鬥" : "普通戰鬥", deep ? "H 槽達標扣除精力；MP 耗盡敗北。" : run.Job == Job.Dancer ? "舞者：所有在場敵人同時暈眩即可獲勝。" : "選擇目標與行動，技能消耗生命或魔力。");
            var alive = battle.Enemies.Where(x => deep ? x.Stamina > 0 : x.Hp > 0).ToList();
            if (!alive.Any(x => x.InstanceId == selectedEnemy)) selectedEnemy = alive.FirstOrDefault()?.InstanceId ?? "";
            float width = 658f / Math.Max(1, battle.Enemies.Count) - 10;
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                var enemy = battle.Enemies[i]; bool selected = enemy.InstanceId == selectedEnemy;
                var card = UiKit.PanelAt("Enemy_" + enemy.InstanceId, main, 24 + i * (width + 10), 112, width, 176, selected ? UiKit.Hex("344D5A") : UiKit.PanelLight).rectTransform;
                UiKit.Text(enemy.Name, card, 13, 12, width - 26, 30, 21, selected ? UiKit.Gold : UiKit.Ink);
                UiKit.Bar(card, 13, 76, width - 26, deep ? "精力" : "HP", deep ? enemy.Stamina : enemy.Hp, deep ? enemy.InitialStamina : enemy.MaxHp, deep ? UiKit.Pink : UiKit.Teal);
                UiKit.Text(deep ? "H  " + enemy.H + " / 100" : "暈眩  " + enemy.StunProgress + " / " + enemy.StunThreshold + "  ·  " + enemy.StunTurns + " 回合", card, 13, 94, width - 26, 26, 15, UiKit.Muted);
                string target = enemy.InstanceId;
                UiKit.Button("Target_" + target, selected ? "已選擇目標" : "選擇目標", card, 12, 130, width - 24, 36, () => { selectedEnemy = target; Refresh(); }, deep ? enemy.Stamina > 0 : enemy.Hp > 0);
            }
            if (deep) UiKit.Text("女主 H  " + battle.HeroH + " / 100    ·    MP " + run.Mp + "    ·    第 " + battle.Turn + " 回合", main, 25, 302, 665, 30, 18, UiKit.Pink);
            else UiKit.Text(battle.AwaitingKnockdownChoice ? "HP 已歸零，請決定是否使用 H 額度續戰。" : "第 " + battle.Turn + " 回合    ·    選定目標後執行行動", main, 25, 302, 665, 30, 18, battle.AwaitingKnockdownChoice ? UiKit.Pink : UiKit.Muted);
            var actions = Session.GetAvailableActions().Where(x => x.Command.Kind != CommandKind.Skill || string.IsNullOrEmpty(x.Command.TargetId) || x.Command.TargetId == selectedEnemy);
            RenderActions(actions, 24, 346, 675, 148, 325, 62);
        }
        private void RenderEnd(bool complete)
        {
            Header(complete ? "系統試玩完成" : "本章攻略失敗", complete ? "你已抵達本次 Demo 的驗證終點。" : "期限已到，請載入先前的存檔重新安排。");
            UiKit.Text(complete ? "探索、兩種戰鬥、城鎮活動與記憶殘留，\n都會成為後續正式版本的基礎。\n\n你可以返回第二章繼續驗證，或另開旅程。" : "畫廊中已解鎖的回想仍會保留。\n讀檔不會立即替你選定任何路線。", main, 28, 150, 650, 180, 24);
            RenderActions(Session.GetAvailableActions(), 24, 370, 675, 100);
        }
        private void RenderGallery()
        {
            Header("回想畫廊", "只播放已解鎖片段，主線、經驗與記憶計數不受影響。");
            if (gallery.Items.Count == 0) UiKit.Text("尚無已解鎖的回想。\n在正式旅程留下經歷後，再回來看看。", main, 30, 150, 650, 130, 25, UiKit.Muted);
            else if (gallery.IsPlaying)
            {
                UiKit.Text(Data.Event("gallery_reference").Body, main, 30, 146, 650, 155, 25);
                UiKit.Button("SkipGallery", "結束回想", main, 30, 330, 310, 55, () => { gallery.Skip(); Refresh(); });
            }
            else UiKit.Button("PlayGallery", "回想・記憶片段", main, 30, 150, 440, 60, () => { gallery.Play(gallery.Items[0]); Refresh(); });
            UiKit.Button("CloseGallery", "返回", main, 30, 440, 210, 48, CloseGallery);
        }
        private void ConfirmLoad(string slot)
        {
            ShowModal("讀取存檔？", "會恢復該存檔的金錢、成長、日程與路線。尚未保存的進度將回退。", "讀取", () => LoadSlot(slot, LoadIntent.ManualLoad), "返回", null);
        }
        private void ShowHelp()
        {
            ShowModal("操作說明", "早上決定遠征，晚上返城；深夜休息恢復並換日。\n劍士以 HP 傷害戰鬥，舞者可令全體同時暈眩獲勝。\n深度戰鬥使用 H 與精力結算；零 HP 續戰勝利後先恢復或返城。\n城鎮與安全存檔點可記錄。畫廊重播不影響主線。", "返回", null);
        }
        private void ShowModal(string title, string body, string accept, Action onAccept, string cancel = null, Action onCancel = null)
        {
            if (modal != null) Destroy(modal.gameObject);
            modal = UiKit.Rect("ModalOverlay", canvasRoot, 0, 0, 1280, 720); var shade = modal.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, 0.8f);
            var panel = UiKit.PanelAt("Modal", modal, 295, 145, 690, 430, UiKit.PanelLight).rectTransform;
            UiKit.Text(title, panel, 28, 25, 634, 48, 28, UiKit.Gold);
            UiKit.Text(body, panel, 28, 95, 634, 230, 21);
            UiKit.Button("ModalAccept", accept, panel, 28, 350, cancel == null ? 634 : 304, 52, () => { CloseModal(); onAccept?.Invoke(); }, true, UiKit.Hex("38645F"));
            if (cancel != null) UiKit.Button("ModalCancel", cancel, panel, 358, 350, 304, 52, () => { CloseModal(); onCancel?.Invoke(); });
        }
        private void CloseModal() { if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; } }
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); status = e.Message; if (statusText != null) statusText.text = status; }
        }
    }
}
