using System;
using System.Linq;

namespace HGame.Core
{
    public static class GrowthRules
    {
        public static int Level(int exp, BalanceConfig balance) { return 1 + Math.Max(0, exp) / Math.Max(1, balance.ExpPerLevel); }
        public static int HLevel(RunState run, GameData data) { return Level(run.HExp, data.Balance); }
        public static int DailyHUses(RunState run, GameData data) { return data.Balance.HUseBase + HLevel(run, data) / 2; }
        public static int ItemCount(RunState run, string id) { var item = run.Inventory.FirstOrDefault(x => x.Id == id); return item == null ? 0 : item.Count; }
        public static void AddItem(RunState run, string id, int count)
        {
            var item = run.Inventory.FirstOrDefault(x => x.Id == id);
            if (item == null) { item = new InventoryEntry { Id = id }; run.Inventory.Add(item); }
            item.Count += count;
        }
        public static string ValidateItem(RunState run, GameData data, string id)
        {
            var item = data.Item(id);
            if (item == null) return "找不到這個物品。";
            if (ItemCount(run, id) < 1) return "背包沒有這個物品。";
            if (item.RestoreHp > 0 && run.Hp >= run.MaxHp && item.RestoreMp <= 0) return "HP 已滿。";
            if (item.RestoreMp > 0 && run.Mp >= run.MaxMp && item.RestoreHp <= 0) return "MP 已滿。";
            return "";
        }
        public static void UseItem(RunState run, GameData data, string id)
        {
            var item = data.Item(id); AddItem(run, id, -1);
            run.Hp = Math.Min(run.MaxHp, run.Hp + item.RestoreHp);
            run.Mp = Math.Min(run.MaxMp, run.Mp + item.RestoreMp);
        }
    }
}
