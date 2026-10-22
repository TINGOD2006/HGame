using System;
using System.Collections.Generic;
using System.Linq;

namespace HGame.Core
{
    /// <summary>Single command boundary. Rendering, capture and loading never run route events.</summary>
    public sealed class GameSession
    {
        private readonly GameData data;
        private RunState run;
        private MetaState meta;
        private ProfileState profile;
        public GameSnapshot Snapshot { get { return new GameSnapshot { Run = CaptureRun(), Meta = CaptureMeta(), Profile = CaptureProfile() }; } }
        public RunState Run { get { return CaptureRun(); } }
        public MetaState Meta { get { return CaptureMeta(); } }
        public ProfileState Profile { get { return CaptureProfile(); } }
        public GameSession(GameData data, RunState run, MetaState meta, ProfileState profile)
        {
            this.data = data ?? throw new ArgumentNullException("data");
            this.run = StateCloner.Clone(run ?? throw new ArgumentNullException("run"));
            this.meta = StateCloner.Clone(meta ?? throw new ArgumentNullException("meta"));
            this.profile = StateCloner.Clone(profile ?? new ProfileState());
            if (run.CampaignId != meta.CampaignId) throw new ArgumentException("旅程與記憶資料不相符。");
        }
        public static GameSession NewGame(GameData data, string campaignId = null, ProfileState profile = null)
        {
            string id = campaignId ?? Guid.NewGuid().ToString("N");
            string head = Guid.NewGuid().ToString("N"); var b = data.Balance;
            var r = new RunState { CampaignId = id, TimelineHeadId = head, Hp = b.InitialHp, MaxHp = b.InitialHp,
                Mp = b.InitialMp, MaxMp = b.InitialMp, Gold = b.InitialGold, Virtue = b.InitialVirtue };
            r.HDailyUsesRemaining = GrowthRules.DailyHUses(r, data);
            r.Chapters.Add(new ChapterProgress { Chapter = 1 });
            CalendarRules.AddJournal(r, "系統試玩開始。角色及所有參與者皆為成年人。前兩天提供教學提示，仍計入章節期限。");
            var m = new MetaState { CampaignId = id, LastHeadId = head };
            m.Timeline.Add(new TimelineNode { Id = head, Label = "NewGame" });
            return new GameSession(data, r, m, profile ?? new ProfileState());
        }
        public RunState CaptureRun() { return StateCloner.Clone(run); }
        public MetaState CaptureMeta() { return StateCloner.Clone(meta); }
        public ProfileState CaptureProfile() { return StateCloner.Clone(profile); }
        public CommandResult Load(RunState target, LoadIntent intent, string transactionId)
        {
            if (target == null) return CommandResult.Reject("存檔不存在。");
            if (target.CampaignId != run.CampaignId) return CommandResult.Reject("請載入該旅程自己的記憶資料。");
            try
            {
                var plan = RewindRules.PlanLoad(run, meta, target, intent);
                var outcome = RewindRules.ApplyLoad(plan, target, meta, transactionId);
                if (!outcome.AlreadyApplied)
                    foreach (var experience in run.Experiences.Where(x => plan.DiscardedNodeIds.Contains(x.TimelineNodeId)))
                        if (!outcome.Meta.Echoes.Any(x => x.Id == experience.Id)) outcome.Meta.Echoes.Add(StateCloner.Clone(experience));
                run = outcome.Run; meta = outcome.Meta;
                if (run.Chapter == 2 && meta.SpecialMemoryIds.Contains("memory_c2")) run.DemoSegmentVisited = true;
                return CommandResult.Ok(outcome.Message);
            }
            catch (Exception e) { return CommandResult.Reject(e.Message); }
        }
        public CommandResult Execute(GameCommand command)
        {
            if (command == null) return CommandResult.Reject("沒有可執行的動作。");
            var before = Snapshot; int defeatsBefore = run.FinalDefeats;
            string battleNodeBefore = run.Battle == null ? "" : run.Battle.NodeId;
            try
            {
                var result = ExecuteInternal(command);
                if (!result.Success) { run = before.Run; meta = before.Meta; profile = before.Profile; return result; }
                if (result.Changed)
                {
                    string head = Guid.NewGuid().ToString("N");
                    meta.Timeline.Add(new TimelineNode { Id = head, ParentId = run.TimelineHeadId, Label = command.Kind.ToString() });
                    run.TimelineHeadId = head; meta.LastHeadId = head;
                    if (run.FinalDefeats > defeatsBefore)
                    {
                        var node = data.Node(battleNodeBefore);
                        var experience = new ExperienceRecord { Id = Guid.NewGuid().ToString("N"), TimelineNodeId = head,
                            EventId = battleNodeBefore, Kind = "Pain", Chapter = run.Chapter, Summary = "曾經在迷宮中承受敗北，時間與來由卻漸漸模糊。" };
                        run.Experiences.Add(experience);
                        if (node != null && node.IsMemorySite)
                            meta.Receipts.Add(new FailureReceipt { Id = Guid.NewGuid().ToString("N"), CampaignId = run.CampaignId,
                                TimelineNodeId = head, Chapter = node.Chapter, EventId = node.Id, MemoryId = node.MemoryId });
                        UnlockGallery("gallery_reference");
                    }
                    if (command.Kind == CommandKind.AdultWork)
                    {
                        run.Experiences.Add(new ExperienceRecord { Id = Guid.NewGuid().ToString("N"), TimelineNodeId = head,
                            EventId = "activity_adult", Kind = "Intimacy", Chapter = run.Chapter, Summary = "一段親密經歷留下清楚的核心，時間與來由卻無法銜接。" });
                        UnlockGallery("gallery_reference");
                    }
                    CalendarRules.AddJournal(run, result.Message);
                    result.RequestAutosave = IsSafeToSave();
                }
                return result;
            }
            catch (Exception e)
            {
                run = before.Run; meta = before.Meta; profile = before.Profile;
                return CommandResult.Reject("動作未完成：" + e.Message);
            }
        }
        private void UnlockGallery(string id) { if (!profile.UnlockedGalleryIds.Contains(id)) profile.UnlockedGalleryIds.Add(id); }
        public bool IsSafeToSave()
        {
            if (run.Location == GameLocation.Town || run.Location == GameLocation.DemoComplete || run.Location == GameLocation.GameOver) return true;
            if (run.Location != GameLocation.Dungeon || !string.IsNullOrEmpty(run.PendingSupplyNodeId)) return false;
            return (run.Battle != null && run.Battle.IsTerminal) || (data.Node(run.CurrentNodeId) != null && DungeonRules.IsSave(data.Node(run.CurrentNodeId)));
        }
        private CommandResult ExecuteInternal(GameCommand c)
        {
            if (run.Location == GameLocation.GameOver) return CommandResult.Reject("本局已結束，請讀取存檔。");
            switch (c.Kind)
            {
                case CommandKind.SwitchJob:
                    Job job;
                    if (run.Location != GameLocation.Town || !Enum.TryParse<Job>(c.TargetId, out job) || !Enum.IsDefined(typeof(Job), job)) return CommandResult.Reject("只能在城鎮選擇劍士或舞者。");
                    if (run.Job == job) return CommandResult.Ok("已經是這個職業。", false);
                    run.Job = job; return CommandResult.Ok("已切換為" + (job == Job.Dancer ? "舞者。" : "劍士。"));
                case CommandKind.Wait:
                    if (!CalendarRules.CanTownAct(run)) return CommandResult.Reject("深夜請在室內休息；現在無法等待外出時段。");
                    CalendarRules.Advance(run, data); return CommandResult.Ok("度過一個時段，沒有額外收益或免費恢復。");
                case CommandKind.Rest: return CalendarRules.Rest(run, data);
                case CommandKind.RestToChapterEnd: return CalendarRules.RestToEnd(run, data);
                case CommandKind.Work:
                case CommandKind.AdultWork:
                    if (!CalendarRules.CanTownAct(run)) return CommandResult.Reject("現在不能外出工作。");
                    bool adult = c.Kind == CommandKind.AdultWork;
                    run.Gold += adult ? data.Balance.AdultWorkGold : data.Balance.WorkGold;
                    if (adult) run.HExp += data.Balance.AdultWorkHExp;
                    CalendarRules.Advance(run, data);
                    return CommandResult.Ok(adult ? "成人工作占位結算：金錢與 H EXP 增加；善惡與純潔資格維持原值。" : "一般工作完成，獲得穩定收入。");
                case CommandKind.Buy: return Buy(c);
                case CommandKind.UseItem: return UseItem(c.TargetId);
                case CommandKind.Depart: return DungeonRules.Depart(run);
                case CommandKind.EnterNode: return DungeonRules.Enter(run, data, data.Node(c.TargetId));
                case CommandKind.ChooseSupply: return Supply(c.ChoiceId);
                case CommandKind.ReturnTown:
                    if (run.Location != GameLocation.Dungeon && run.Location != GameLocation.DemoComplete) return CommandResult.Reject("目前無法從這裡返城。");
                    DungeonRules.Return(run); return CommandResult.Ok("安全返回城鎮，現在是晚上。");
                case CommandKind.Attack: return BattleRules.Skill(run, data, "attack", c.TargetId);
                case CommandKind.Skill: return BattleRules.Skill(run, data, c.ChoiceId, c.TargetId);
                case CommandKind.EnterDeep:
                    if (run.Location != GameLocation.Battle) return CommandResult.Reject("目前不在戰鬥中。");
                    return BattleRules.EnterDeep(run, data, true);
                case CommandKind.AcceptDeep:
                    if (run.Location != GameLocation.Battle) return CommandResult.Reject("目前不在戰鬥中。");
                    return BattleRules.EnterDeep(run, data, false);
                case CommandKind.RefuseDeep:
                    if (!ActiveBattle() || !run.Battle.AwaitingKnockdownChoice) return CommandResult.Reject("目前沒有被動續戰選項。");
                    BattleRules.Finish(run, data, BattleOutcome.Defeat); return CommandResult.Ok("拒絕續戰，最終敗北並於晚上返城。");
                case CommandKind.Defend:
                    if (!ActiveBattle() || run.Battle.AwaitingKnockdownChoice || run.Battle.Phase != BattlePhase.Normal) return CommandResult.Reject("目前不能防禦。");
                    run.Battle.Defended = true; BattleRules.EnemyTurn(run, data); return CommandResult.Ok("防禦：本次敵方回合 HP 傷害減半。");
                case CommandKind.DeepWait:
                    if (!ActiveBattle() || run.Battle.AwaitingKnockdownChoice || run.Battle.Phase != BattlePhase.Deep) return CommandResult.Reject("目前不在 H 階段。");
                    BattleRules.EnemyTurn(run, data); return CommandResult.Ok("等待一回合，敵方依序行動。");
                case CommandKind.Abandon:
                    if (!ActiveBattle()) return CommandResult.Reject("目前不在戰鬥中。");
                    BattleRules.Finish(run, data, BattleOutcome.Defeat); return CommandResult.Ok("放棄戰鬥，計一次最終敗北並於晚上返城。");
                case CommandKind.AcceptRoute: return RouteRules.Accept(run, meta, data);
                case CommandKind.DeclineRoute:
                    if (run.Location != GameLocation.Town || meta.SpecialMemoryIds.Count == 0) return CommandResult.Reject("還沒有可回想的異樣。");
                    return CommandResult.Ok("她記得一段經歷的核心，卻無法接上時間與來由。這次先維持現狀。", false);
                case CommandKind.CompleteDemo:
                    if (run.Chapter < 2 || run.Location == GameLocation.Battle) return CommandResult.Reject("第二章驗證段才提供完成試玩。");
                    if (run.Location == GameLocation.DemoComplete) return CommandResult.Ok("試玩已完成。", false);
                    run.Location = GameLocation.DemoComplete; return CommandResult.Ok("系統試玩完成。正式結局及完整第二章保留在後續版本；仍可回城繼續驗證。");
                default: return CommandResult.Reject("不支援的動作。");
            }
        }
        private bool ActiveBattle() { return run.Location == GameLocation.Battle && run.Battle != null && !run.Battle.IsTerminal; }
        private CommandResult Buy(GameCommand c)
        {
            if (!CalendarRules.CanTownAct(run)) return CommandResult.Reject("現在不能外出購物。");
            var item = data.Item(c.TargetId);
            if (item == null || c.Quantity < 1 || c.Quantity > 999) return CommandResult.Reject("請選擇有效物品與數量。");
            long price = (long)item.Price * c.Quantity; if (price > run.Gold) return CommandResult.Reject("金錢不足。");
            run.Gold -= (int)price; GrowthRules.AddItem(run, item.Id, c.Quantity);
            CalendarRules.Advance(run, data); return CommandResult.Ok("購入 " + item.Name + " × " + c.Quantity + "，使用一個時段。");
        }
        private CommandResult UseItem(string id)
        {
            if (run.Location != GameLocation.Town && run.Location != GameLocation.Dungeon && run.Location != GameLocation.Battle) return CommandResult.Reject("目前不能使用物品。");
            if (run.Location == GameLocation.Battle && (!ActiveBattle() || run.Battle.AwaitingKnockdownChoice)) return CommandResult.Reject("請先處理續戰選項。");
            string reason = GrowthRules.ValidateItem(run, data, id); if (reason != "") return CommandResult.Reject(reason);
            GrowthRules.UseItem(run, data, id);
            if (ActiveBattle()) BattleRules.EnemyTurn(run, data);
            return CommandResult.Ok("使用恢復品，數值不超過上限。" + (run.Location == GameLocation.Battle ? "已消耗一次戰鬥行動。" : ""));
        }
        private CommandResult Supply(string choice)
        {
            if (run.Location != GameLocation.Dungeon || string.IsNullOrEmpty(run.PendingSupplyNodeId)) return CommandResult.Reject("目前沒有待結算補給。");
            var node = data.Node(run.PendingSupplyNodeId);
            if (node == null || DungeonRules.Progress(run, run.Chapter).ClearedNodeIds.Contains(node.Id)) return CommandResult.Reject("這個補給已領取。");
            if (choice == "support")
            {
                if (run.Gold < data.Balance.SupportGoldCost) return CommandResult.Reject("金錢不足以支援補給。");
                run.Gold -= data.Balance.SupportGoldCost; run.Virtue = Math.Min(100, run.Virtue + data.Balance.SupplyVirtue);
            }
            else if (choice == "take") { run.Gold += data.Balance.SupplyGold; run.Virtue = Math.Max(0, run.Virtue - data.Balance.SupplyVirtue); }
            else return CommandResult.Reject("請選擇支援或取得物資。");
            run.ClaimedEvents.Add(node.Id); run.PendingSupplyNodeId = ""; DungeonRules.Clear(run, data, node);
            return CommandResult.Ok(choice == "support" ? "支援補給完成，善惡上升。" : "優先取得物資，善惡下降。");
        }
        public List<ActionAvailability> GetAvailableActions()
        {
            var list = new List<ActionAvailability>();
            if (run.Location == GameLocation.Town)
            {
                bool day = run.Slot != TimeSlot.LateNight; string night = day ? "" : "深夜不得外出。";
                Add(list, "Town", "一般工作（+" + data.Balance.WorkGold + " 金／一時段）", new GameCommand(CommandKind.Work), night);
                Add(list, "Town", "成人工作（+金錢、H EXP／一時段）", new GameCommand(CommandKind.AdultWork), night);
                foreach (var item in data.Items)
                    Add(list, "Town", "購買 " + item.Name + "（" + item.Price + " 金／一時段）", new GameCommand(CommandKind.Buy, item.Id), day ? (run.Gold >= item.Price ? "" : "金錢不足。") : night);
                Add(list, "Town", "早上遠征", new GameCommand(CommandKind.Depart), run.Slot != TimeSlot.Morning ? "只有早上能遠征。" : run.Hp <= 0 ? "請先恢復 HP。" : "");
                if (day) Add(list, "Town", "等待下一時段", new GameCommand(CommandKind.Wait));
                else Add(list, "Town", "深夜休息並結束一天", new GameCommand(CommandKind.Rest));
                Add(list, "Town", "轉職：劍士", new GameCommand(CommandKind.SwitchJob, "Swordsman"), run.Job == Job.Swordsman ? "目前已是劍士。" : "");
                Add(list, "Town", "轉職：舞者", new GameCommand(CommandKind.SwitchJob, "Dancer"), run.Job == Job.Dancer ? "目前已是舞者。" : "");
                if (meta.SpecialMemoryIds.Count > 0)
                {
                    Add(list, "Town", "查看記憶異樣／維持現狀", new GameCommand(CommandKind.DeclineRoute));
                    if (RouteRules.CanCorrupt(run, meta, data))
                        Add(list, "Town", "接受重大轉變（力量、敵人與難度改變；路線鎖定）", new GameCommand(CommandKind.AcceptRoute));
                }
                var progress = run.Chapters.FirstOrDefault(x => x.Chapter == run.Chapter);
                if (progress != null && progress.BossCleared) Add(list, "Town", "休整至章末（用完剩餘行動，無額外獎勵）", new GameCommand(CommandKind.RestToChapterEnd));
            }
            else if (run.Location == GameLocation.Dungeon)
            {
                if (!string.IsNullOrEmpty(run.PendingSupplyNodeId))
                {
                    Add(list, "Dungeon", "支援補給（−10 金／善惡 +10）", new GameCommand(CommandKind.ChooseSupply, "", "support"), run.Gold < data.Balance.SupportGoldCost ? "金錢不足。" : "");
                    Add(list, "Dungeon", "優先取得物資（+10 金／善惡 −10）", new GameCommand(CommandKind.ChooseSupply, "", "take"));
                }
                else foreach (var node in data.Nodes.Where(x => x.Chapter == run.Chapter).OrderBy(x => x.Index))
                    Add(list, "Dungeon", node.Name, new GameCommand(CommandKind.EnterNode, node.Id), DungeonRules.NodeReason(run, data, node));
                Add(list, "Dungeon", "安全返城（晚上）", new GameCommand(CommandKind.ReturnTown));
            }
            else if (ActiveBattle())
            {
                if (run.Battle.AwaitingKnockdownChoice)
                {
                    Add(list, "Battle", "接受 H 續戰（不恢復 HP）", new GameCommand(CommandKind.AcceptDeep), BattleRules.EntryReasonText(run, false));
                    Add(list, "Battle", "拒絕續戰並敗北返城", new GameCommand(CommandKind.RefuseDeep));
                }
                else
                {
                    foreach (var skill in data.Skills)
                    {
                        if (skill.Id == "defend" || skill.Id == "wait") continue;
                        if (skill.JobId != "Any" && !string.Equals(skill.JobId, run.Job.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.Equals(skill.PhaseId, run.Battle.Phase.ToString(), StringComparison.OrdinalIgnoreCase) && skill.PhaseId != "Any" && skill.PhaseId != "Both") continue;
                        if (skill.MagicOnly && run.Route != Route.Corrupted) continue;
                        if (skill.Id == "enter_deep") { Add(list, "Battle", skill.Name, new GameCommand(CommandKind.EnterDeep), BattleRules.EntryReasonText(run, true)); continue; }
                        if (skill.AllTargets) Add(list, "Battle", skill.Name + "（全體）", new GameCommand(CommandKind.Skill, "", skill.Id), BattleRules.SkillReason(run, data, skill, ""));
                        else foreach (var enemy in run.Battle.Enemies.Where(x => BattleRules.Alive(run.Battle, x)))
                            Add(list, "Battle", skill.Name + " → " + enemy.Name, new GameCommand(CommandKind.Skill, enemy.InstanceId, skill.Id), BattleRules.SkillReason(run, data, skill, enemy.InstanceId));
                    }
                    if (run.Battle.Phase == BattlePhase.Normal) Add(list, "Battle", "防禦", new GameCommand(CommandKind.Defend));
                    else Add(list, "Battle", "等待一回合", new GameCommand(CommandKind.DeepWait));
                    Add(list, "Battle", "放棄並敗北返城", new GameCommand(CommandKind.Abandon));
                }
            }
            if ((run.Location == GameLocation.Town || run.Location == GameLocation.Dungeon || ActiveBattle()) && !(ActiveBattle() && run.Battle.AwaitingKnockdownChoice))
                foreach (var item in data.Items)
                    Add(list, run.Location == GameLocation.Battle ? "Battle" : run.Location == GameLocation.Dungeon ? "Dungeon" : "Town", "使用 " + item.Name + "（剩 " + GrowthRules.ItemCount(run, item.Id) + "）", new GameCommand(CommandKind.UseItem, item.Id), GrowthRules.ValidateItem(run, data, item.Id));
            if (run.Chapter == 2 && run.Location != GameLocation.Battle && run.Location != GameLocation.GameOver && run.Location != GameLocation.DemoComplete)
                Add(list, run.Location == GameLocation.Dungeon ? "Dungeon" : "Town", "完成系統試玩", new GameCommand(CommandKind.CompleteDemo));
            if (run.Location == GameLocation.DemoComplete) Add(list, "Town", "返回第二章繼續驗證", new GameCommand(CommandKind.ReturnTown));
            return list;
        }
        private static void Add(List<ActionAvailability> list, string group, string label, GameCommand command, string reason = "")
        {
            list.Add(new ActionAvailability { Id = command.Kind + ":" + command.TargetId + ":" + command.ChoiceId, Group = group, Label = label,
                Command = command, Enabled = string.IsNullOrEmpty(reason), Reason = reason ?? "" });
        }
    }
}
