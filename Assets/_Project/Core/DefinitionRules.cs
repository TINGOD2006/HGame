using System;
using System.Collections.Generic;
using System.Linq;

namespace HGame.Core
{
    public static class DefinitionRules
    {
        public static List<string> Validate(GameData data)
        {
            var errors = new List<string>();
            if (data == null) { errors.Add("缺少遊戲資料。"); return errors; }
            Unique(data.Items.Select(x => x.Id), "物品", errors);
            Unique(data.Enemies.Select(x => x.Id), "敵人", errors);
            Unique(data.Skills.Select(x => x.Id), "技能", errors);
            Unique(data.Nodes.Select(x => x.Id), "節點", errors);
            Unique(data.Events.Select(x => x.Id), "事件", errors);
            if (data.Balance.ExpPerLevel <= 0 || data.Balance.MaxDays <= 0 || data.Balance.MaxNewNodes <= 0) errors.Add("成長與日程參數必須為正數。");
            if (data.Balance.StageOpenDays.Count != 3 || data.Balance.StageOpenDays.Any(x => x < 1)) errors.Add("迷宮需配置三個合法開放日期。");
            foreach (var item in data.Items) if (item.Price < 0 || item.RestoreHp < 0 || item.RestoreMp < 0) errors.Add("物品不能有負成本或負恢復：" + item.Id);
            foreach (var skill in data.Skills)
            {
                if (skill.HpCost < 0 || skill.MpCost < 0 || skill.Damage < 0 || skill.HIncrease < 0 || skill.StunIncrease < 0) errors.Add("技能效果或成本不能為負：" + skill.Id);
                if (skill.PhaseId != "Normal" && skill.PhaseId != "Deep" && skill.PhaseId != "Any" && skill.PhaseId != "Both") errors.Add("非法技能階段：" + skill.Id);
                if (skill.JobId != "Any" && skill.JobId != "Swordsman" && skill.JobId != "Dancer") errors.Add("非法技能職業：" + skill.Id);
            }
            foreach (var enemy in data.Enemies) if (enemy.Hp <= 0 || enemy.Stamina <= 0 || enemy.StunThreshold <= 0 || enemy.Damage < 0 || enemy.HAttack < 0) errors.Add("非法敵人資源：" + enemy.Id);
            foreach (var node in data.Nodes)
            {
                if (node.Stage < 1 || node.Stage > 3 || node.Index < 1) errors.Add("非法節點位置：" + node.Id);
                foreach (string id in node.EnemyIds) if (data.Enemy(id) == null) errors.Add("節點引用不存在的敵人：" + node.Id + " / " + id);
                if (node.Index > 1 && !data.Nodes.Any(x => x.Chapter == node.Chapter && x.Index == node.Index - 1)) errors.Add("節點前置斷鏈：" + node.Id);
                if (node.IsMemorySite && (string.IsNullOrEmpty(node.MemoryId) || node.EnemyIds.Count == 0)) errors.Add("特殊記憶點缺少識別或戰鬥：" + node.Id);
            }
            foreach (var chapter in data.Nodes.GroupBy(x => x.Chapter))
            {
                if (chapter.GroupBy(x => x.Index).Any(x => x.Count() > 1)) errors.Add("章節節點索引重複：" + chapter.Key);
                if (chapter.Count(x => x.IsMemorySite) != 1) errors.Add("每章必須有一個指定記憶點：" + chapter.Key);
            }
            if (data.Nodes.Count(x => x.Chapter == 1) != 12 || data.Nodes.Count(x => x.Chapter == 1 && DungeonRules.IsSave(x)) != 6) errors.Add("第一章須有十二節點及六存檔點。");
            foreach (string id in new[] { "attack", "slash", "dance", "enter_deep", "h_touch", "h_corrupt" }) if (data.Skill(id) == null) errors.Add("缺少必要技能：" + id);
            return errors;
        }
        private static void Unique(IEnumerable<string> ids, string label, List<string> errors)
        {
            var list = ids.ToList(); if (list.Any(string.IsNullOrEmpty)) errors.Add(label + "有空白識別。");
            foreach (var group in list.GroupBy(x => x).Where(x => x.Count() > 1)) errors.Add(label + "識別重複：" + group.Key);
        }
    }
}
