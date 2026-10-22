using System;
using System.Linq;

namespace HGame.Core
{
    public static class BattleRules
    {
        public static void Start(RunState run, GameData data, NodeDefinition node)
        {
            var battle = new BattleState { NodeId = node.Id, Phase = BattlePhase.Normal };
            int i = 0;
            foreach (string id in node.EnemyIds)
            {
                var e = data.Enemy(id); if (e == null) throw new InvalidOperationException("敵人資料不存在：" + id);
                bool corrupt = run.Route == Route.Corrupted;
                battle.Enemies.Add(new EnemyState { InstanceId = id + "_" + i++, DefinitionId = id, Name = e.Name,
                    Hp = e.Hp, MaxHp = e.Hp, Damage = e.Damage + (corrupt ? e.CorruptedDamageBonus : 0),
                    Stamina = e.Stamina, InitialStamina = e.Stamina, HAttack = e.HAttack + (corrupt ? e.CorruptedHAttackBonus : 0), StunThreshold = e.StunThreshold });
            }
            run.Battle = battle; run.Location = GameLocation.Battle;
        }
        public static string EntryReasonText(RunState run, bool active)
        {
            if (run.Battle == null || run.Battle.IsTerminal || run.Battle.Phase != BattlePhase.Normal) return "目前無法進入深度模式。";
            if (run.HDailyUsesRemaining <= 0) return "今天的 H 戰鬥額度已用完。";
            if (active && run.Mp <= 1) return "支付入場成本後須至少保留 1 MP。";
            if (!active && run.Mp < 1) return "沒有 MP，無法繼續 H 戰鬥。";
            return "";
        }
        public static void ConvertEnemy(EnemyState e)
        {
            if (e.Converted || e.Hp <= 0) return;
            int total = (int)(((long)(e.MaxHp - e.Hp) * e.InitialStamina * 100) / Math.Max(1, e.MaxHp));
            e.Stamina = e.InitialStamina - total / 100; e.H = total % 100; e.Converted = true;
        }
        public static CommandResult EnterDeep(RunState run, GameData data, bool active)
        {
            string reason = EntryReasonText(run, active); if (reason != "") return CommandResult.Reject(reason);
            var b = run.Battle;
            if (active && b.AwaitingKnockdownChoice) return CommandResult.Reject("請決定是否接受續戰。");
            if (!active && !b.AwaitingKnockdownChoice) return CommandResult.Reject("目前不需要被動續戰。");
            if (active) run.Mp--;
            run.HDailyUsesRemaining--; b.Phase = BattlePhase.Deep;
            b.EntryReason = active ? EntryReason.Active : EntryReason.Knockdown; b.AwaitingKnockdownChoice = false;
            b.Enemies.RemoveAll(x => x.Hp <= 0);
            foreach (var e in b.Enemies) ConvertEnemy(e);
            CheckVictory(run, data);
            return CommandResult.Ok(active ? "主動進入 H 階段，保留目前 HP。" : "接受被擊倒後的 H 續戰，HP 不會自動恢復。");
        }
        public static string SkillReason(RunState run, GameData data, SkillDefinition skill, string targetId)
        {
            if (run.Location != GameLocation.Battle || run.Battle == null || run.Battle.IsTerminal) return "目前不在戰鬥中。";
            if (run.Battle.AwaitingKnockdownChoice) return "請先決定是否續戰。";
            if (skill == null) return "找不到技能。";
            if (skill.JobId != "Any" && !string.Equals(skill.JobId, run.Job.ToString(), StringComparison.OrdinalIgnoreCase)) return "這個職業無法使用。";
            if (!string.Equals(skill.PhaseId, run.Battle.Phase.ToString(), StringComparison.OrdinalIgnoreCase) && skill.PhaseId != "Any" && skill.PhaseId != "Both") return "目前戰鬥階段不能使用。";
            if (skill.MagicOnly && run.Route != Route.Corrupted) return "尚未解鎖這個技能。";
            if (skill.HpCost > 0 && run.Hp - skill.HpCost < 1) return "支付 HP 後須至少保留 1 HP。";
            if (skill.MpCost > 0 && run.Mp - skill.MpCost < 1) return "支付 MP 後須至少保留 1 MP。";
            if (skill.Id == "enter_deep") return EntryReasonText(run, true);
            if (skill.Effect == "Defend" || skill.Effect == "Wait") return "";
            if (!skill.AllTargets && !run.Battle.Enemies.Any(x => x.InstanceId == targetId && Alive(run.Battle, x))) return "請選擇仍在場的敵人。";
            return "";
        }
        public static bool Alive(BattleState b, EnemyState e) { return b.Phase == BattlePhase.Normal ? e.Hp > 0 : e.Stamina > 0; }
        public static CommandResult Skill(RunState run, GameData data, string skillId, string targetId)
        {
            var skill = data.Skill(skillId); string reason = SkillReason(run, data, skill, targetId);
            if (reason != "") return CommandResult.Reject(reason);
            if (skill.Id == "enter_deep" || skill.Effect == "EnterDeep") return EnterDeep(run, data, true);
            if (skill.Effect == "Defend") { run.Battle.Defended = true; EnemyTurn(run, data); return CommandResult.Ok("防禦已結算。"); }
            if (skill.Effect == "Wait") { EnemyTurn(run, data); return CommandResult.Ok("等待一回合。"); }
            run.Hp -= skill.HpCost; run.Mp -= skill.MpCost;
            var b = run.Battle;
            var targets = b.Enemies.Where(x => Alive(b, x) && (skill.AllTargets || x.InstanceId == targetId)).ToList();
            foreach (var e in targets)
            {
                if (skill.Damage > 0 && b.Phase == BattlePhase.Normal) e.Hp = Math.Max(0, e.Hp - skill.Damage);
                if (skill.StunIncrease > 0 && b.Phase == BattlePhase.Normal)
                {
                    e.StunProgress += skill.StunIncrease;
                    if (e.StunProgress >= Math.Max(1, e.StunThreshold))
                    { e.StunProgress -= Math.Max(1, e.StunThreshold); e.StunTurns = data.Balance.StunDuration; }
                }
                if (skill.HIncrease > 0 && b.Phase == BattlePhase.Deep)
                {
                    e.H += skill.HIncrease;
                    while (e.H >= 100 && e.Stamina > 0)
                    { e.H -= 100; e.Stamina--; if (CheckVictory(run, data)) break; }
                }
                if (CheckVictory(run, data)) return CommandResult.Ok("戰鬥勝利。");
            }
            if (!CheckVictory(run, data)) EnemyTurn(run, data);
            return CommandResult.Ok(skill.Name + " 已結算。");
        }
        public static bool CheckVictory(RunState run, GameData data)
        {
            var b = run.Battle; if (b == null) return false; if (b.IsTerminal) return true;
            if (b.Phase == BattlePhase.Deep)
            { if (b.Enemies.All(x => x.Stamina <= 0)) { Finish(run, data, BattleOutcome.HVictory); return true; } }
            else
            {
                var alive = b.Enemies.Where(x => x.Hp > 0).ToList();
                if (alive.Count == 0) { Finish(run, data, BattleOutcome.NormalVictory); return true; }
                if (run.Job == Job.Dancer && alive.All(x => x.StunTurns > 0)) { Finish(run, data, BattleOutcome.StunVictory); return true; }
            }
            return false;
        }
        public static void EnemyTurn(RunState run, GameData data)
        {
            var b = run.Battle; if (b == null || b.IsTerminal || b.AwaitingKnockdownChoice) return;
            foreach (var e in b.Enemies)
            {
                if (!Alive(b, e)) continue;
                if (b.Phase == BattlePhase.Normal)
                {
                    if (e.StunTurns > 0) { e.StunTurns--; continue; }
                    int damage = b.Defended ? Math.Max(1, e.Damage / 2) : e.Damage;
                    run.Hp = Math.Max(0, run.Hp - damage);
                    if (run.Hp == 0)
                    {
                        if (run.Mp > 0 && run.HDailyUsesRemaining > 0) b.AwaitingKnockdownChoice = true;
                        else Finish(run, data, BattleOutcome.Defeat);
                        b.Defended = false; return;
                    }
                }
                else
                {
                    b.HeroH += e.HAttack;
                    while (b.HeroH >= 100)
                    {
                        b.HeroH -= 100; run.Mp = Math.Max(0, run.Mp - data.Balance.HeroHThresholdMpLoss);
                        if (run.Mp == 0) { Finish(run, data, BattleOutcome.Defeat); return; }
                    }
                }
            }
            b.Defended = false; b.Turn++;
        }
        public static void Finish(RunState run, GameData data, BattleOutcome outcome)
        {
            var b = run.Battle; if (b.IsTerminal) return;
            b.IsTerminal = true; b.Outcome = outcome; b.AwaitingKnockdownChoice = false;
            if (outcome == BattleOutcome.Defeat)
            {
                run.FinalDefeats++; DungeonRules.Return(run);
                CalendarRules.AddJournal(run, "最終敗北，晚上返回城鎮。實際 HP／MP 保留。"); return;
            }
            var node = data.Node(b.NodeId);
            bool newlyCleared = node == null || !DungeonRules.Progress(run, node.Chapter).ClearedNodeIds.Contains(node.Id);
            if (!b.RewardGranted)
            {
                if (outcome == BattleOutcome.HVictory) { run.HWins++; if (newlyCleared) run.HExp += data.Balance.VictoryExp; }
                else { run.NormalWins++; if (newlyCleared) run.NormalExp += data.Balance.VictoryExp; }
                b.RewardGranted = true;
            }
            if (node != null) DungeonRules.Clear(run, data, node);
            run.Location = GameLocation.Dungeon;
            CalendarRules.AddJournal(run, outcome == BattleOutcome.HVictory ? "H 勝利，HP／MP 保留實際值。" : "普通戰鬥勝利。");
        }
    }
}
