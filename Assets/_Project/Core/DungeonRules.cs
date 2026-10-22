using System;
using System.Linq;

namespace HGame.Core
{
    public static class DungeonRules
    {
        public static ChapterProgress Progress(RunState run, int chapter)
        {
            var p = run.Chapters.FirstOrDefault(x => x.Chapter == chapter);
            if (p == null) { p = new ChapterProgress { Chapter = chapter }; run.Chapters.Add(p); }
            return p;
        }
        public static double Exploration(RunState run, GameData data, int chapter)
        {
            var p = run.Chapters.FirstOrDefault(x => x.Chapter == chapter);
            int count = data.Nodes.Count(x => x.Chapter == chapter);
            return count == 0 || p == null ? 0 : (double)p.ClearedNodeIds.Count / count;
        }
        public static string NodeReason(RunState run, GameData data, NodeDefinition node)
        {
            if (run.Location != GameLocation.Dungeon) return "請先遠征。";
            if (node == null || node.Chapter != run.Chapter) return "這個節點尚未開放。";
            if (run.Hp <= 0) return "HP 為 0，請先恢復或安全返城。";
            if (!string.IsNullOrEmpty(run.PendingSupplyNodeId)) return "請先結算眼前的補給選項。";
            int day = node.Stage > 0 && node.Stage <= data.Balance.StageOpenDays.Count ? data.Balance.StageOpenDays[node.Stage - 1] : 1;
            if (run.Day < day) return "這個階段將在第 " + day + " 天開放。";
            var p = Progress(run, run.Chapter);
            if (p.ClearedNodeIds.Contains(node.Id)) return "";
            if (run.ExpeditionNewNodes >= data.Balance.MaxNewNodes) return "這趟遠征已到新增進度上限。";
            var previous = data.Nodes.FirstOrDefault(x => x.Chapter == node.Chapter && x.Index == node.Index - 1);
            if (previous != null && !p.ClearedNodeIds.Contains(previous.Id)) return "請先完成前一個節點。";
            return "";
        }
        public static CommandResult Depart(RunState run)
        {
            if (run.Location != GameLocation.Town || run.Slot != TimeSlot.Morning) return CommandResult.Reject("只有早上能決定遠征。");
            if (run.Hp <= 0) return CommandResult.Reject("請先恢復 HP 再遠征。");
            run.Location = GameLocation.Dungeon; run.ExpeditionNewNodes = 0;
            run.CurrentNodeId = run.LastSaveNodeId; run.Battle = null;
            return CommandResult.Ok("早上出發，這趟最多新增一階段進度；返回時為晚上。");
        }
        public static void Return(RunState run)
        {
            run.Location = GameLocation.Town; run.Slot = TimeSlot.Evening; run.PendingSupplyNodeId = "";
        }
        public static void Clear(RunState run, GameData data, NodeDefinition node)
        {
            var p = Progress(run, node.Chapter);
            if (!p.ClearedNodeIds.Contains(node.Id)) { p.ClearedNodeIds.Add(node.Id); run.ExpeditionNewNodes++; }
            if (node.IsBoss) p.BossCleared = true;
            if (IsSave(node)) run.LastSaveNodeId = node.Id;
        }
        public static bool IsSave(NodeDefinition node)
        { return string.Equals(node.Kind, "Save", StringComparison.OrdinalIgnoreCase) || string.Equals(node.Kind, "SavePoint", StringComparison.OrdinalIgnoreCase); }
        public static bool IsSupply(NodeDefinition node) { return string.Equals(node.Kind, "Supply", StringComparison.OrdinalIgnoreCase); }
        public static CommandResult Enter(RunState run, GameData data, NodeDefinition node)
        {
            string reason = NodeReason(run, data, node); if (reason != "") return CommandResult.Reject(reason);
            if (run.CurrentNodeId == node.Id && node.EnemyIds.Count == 0 && Progress(run, node.Chapter).ClearedNodeIds.Contains(node.Id))
                return CommandResult.Ok("已在這個節點。", false);
            run.CurrentNodeId = node.Id;
            if (node.EnemyIds.Count > 0)
            {
                BattleRules.Start(run, data, node); return CommandResult.Ok(node.Name + "：戰鬥開始。");
            }
            if (IsSupply(node) && !Progress(run, node.Chapter).ClearedNodeIds.Contains(node.Id))
            {
                run.PendingSupplyNodeId = node.Id; return CommandResult.Ok("補給取捨：請選擇如何處理物資。");
            }
            Clear(run, data, node);
            return CommandResult.Ok(IsSave(node) ? "抵達安全存檔點。這裡不免費恢復。" : "抵達 " + node.Name + "。");
        }
    }
}
