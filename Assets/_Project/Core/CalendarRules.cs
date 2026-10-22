using System;
using System.Linq;

namespace HGame.Core
{
    public static class CalendarRules
    {
        public static bool CanTownAct(RunState run) { return run.Location == GameLocation.Town && run.Slot != TimeSlot.LateNight; }
        public static void Advance(RunState run, GameData data)
        {
            if (run.Slot != TimeSlot.LateNight) { run.Slot = (TimeSlot)((int)run.Slot + 1); return; }
            EndDay(run, data);
        }
        public static void EndDay(RunState run, GameData data)
        {
            if (run.Day >= data.Balance.MaxDays)
            {
                var progress = DungeonRules.Progress(run, run.Chapter);
                if (!progress.BossCleared && run.Chapter == 1)
                { run.Location = GameLocation.GameOver; AddJournal(run, "期限已到，迷宮尚未攻略。請讀取存檔重新安排。"); return; }
                if (run.Chapter == 1)
                {
                    run.Chapter = 2; run.Day = 1; run.CurrentNodeId = ""; run.LastSaveNodeId = "";
                    DungeonRules.Progress(run, 2);
                    AddJournal(run, "第二章驗證段開放：另一段不連續的記憶。");
                }
                else { run.Location = GameLocation.DemoComplete; return; }
            }
            else run.Day++;
            run.Slot = TimeSlot.Morning;
            run.HDailyUsesRemaining = GrowthRules.DailyHUses(run, data);
            run.Location = GameLocation.Town;
        }
        public static CommandResult Rest(RunState run, GameData data)
        {
            if (run.Location != GameLocation.Town || run.Slot != TimeSlot.LateNight) return CommandResult.Reject("只有深夜能結束一天並休息。");
            run.Hp = run.MaxHp; run.Mp = run.MaxMp; EndDay(run, data);
            return CommandResult.Ok(run.Location == GameLocation.GameOver ? "期限失敗。請讀檔重新安排。" : "深夜休息結束，HP／MP 恢復，迎接下一天。");
        }
        public static CommandResult RestToEnd(RunState run, GameData data)
        {
            if (run.Location != GameLocation.Town || !DungeonRules.Progress(run, run.Chapter).BossCleared)
                return CommandResult.Reject("攻略完成後才可以休整至章末。");
            run.Day = data.Balance.MaxDays; run.Slot = TimeSlot.LateNight;
            return Rest(run, data);
        }
        public static void AddJournal(RunState run, string text)
        {
            run.Journal.Add(text); if (run.Journal.Count > 80) run.Journal.RemoveAt(0);
        }
    }
}
