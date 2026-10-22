using System.Collections.Generic;

namespace HGame.Core
{
    public static class RouteRules
    {
        public static bool CanCorrupt(RunState run, MetaState meta, GameData data)
        {
            var b = data.Balance;
            return run.Route == Route.Unbound && meta.EffectiveRewinds >= b.RequiredRewinds && meta.SpecialMemoryIds.Count >= b.RequiredMemories
                && run.FinalDefeats >= b.RequiredDefeats && run.Virtue <= b.LowVirtue && run.HWins > 0
                && GrowthRules.HLevel(run, data) >= b.HLevelRequired;
        }
        public static List<Route> Candidates(RunState run, MetaState meta, GameData data)
        {
            var result = new List<Route>();
            if (run.Route != Route.Unbound) { result.Add(run.Route); return result; }
            result.Add(Route.General); var b = data.Balance;
            bool normal = GrowthRules.Level(run.NormalExp, b) >= 2 && run.NormalWins > 0;
            bool h = GrowthRules.HLevel(run, data) >= b.HLevelRequired && run.HWins > 0;
            if (run.Virtue >= b.HighVirtue)
            {
                if (normal && run.IsVirgin && DungeonRules.Exploration(run, data, 1) >= .8) result.Add(Route.Pure);
                if (h) result.Add(Route.Succubus);
            }
            if (run.Virtue <= b.LowVirtue && normal && run.FinalDefeats >= b.RequiredDefeats) result.Add(Route.Dark);
            if (CanCorrupt(run, meta, data)) result.Add(Route.Corrupted);
            return result;
        }
        public static CommandResult Accept(RunState run, MetaState meta, GameData data)
        {
            if (run.Location != GameLocation.Town) return CommandResult.Reject("請回到城鎮室內再作決定。");
            if (!CanCorrupt(run, meta, data)) return CommandResult.Reject("現在還沒有出現新的重大轉變。");
            run.PlayerAware = true;
            if (!run.PlayerAware) return CommandResult.Reject("必要事件尚未完成。");
            run.Route = Route.Corrupted; run.DifficultyId = "corrupted"; run.BranchResolved = true;
            return CommandResult.Ok("她接受了改變。解鎖特殊 H 技能；後續敵人的力量也將改變。這條路線已鎖定，只有讀檔能回退。");
        }
    }
}
