using System;
using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using UnityEngine;

namespace HGame.Unity
{
    public static class DefinitionLoader
    {
        public static GameData Load()
        {
            return FromJson(Read("balance"), Read("enemies"), Read("skills"), Read("dungeon"), Read("events"));
        }
        private static string Read(string name)
        {
            var asset = Resources.Load<TextAsset>("Demo/" + name);
            if (asset == null) throw new InvalidOperationException("缺少 Demo 資料：" + name);
            return asset.text;
        }
        public static GameData FromJson(string balance, string enemies, string skills, string dungeon, string events)
        {
            var b = JsonUtility.FromJson<BalanceFile>(balance);
            var data = new GameData {
                Balance = b.Balance, Items = b.Items,
                Enemies = JsonUtility.FromJson<EnemyFile>(enemies).Items,
                Skills = JsonUtility.FromJson<SkillFile>(skills).Items,
                Nodes = JsonUtility.FromJson<DungeonFile>(dungeon).Items,
                Events = JsonUtility.FromJson<EventFile>(events).Items
            };
            var errors = Validate(data);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            return data;
        }
        public static List<string> Validate(GameData data)
        {
            var errors = new List<string>();
            if (data == null || data.Balance == null) { errors.Add("缺少平衡設定。"); return errors; }
            CheckIds(data.Enemies.Select(x => x.Id), "敵人", errors);
            CheckIds(data.Skills.Select(x => x.Id), "技能", errors);
            CheckIds(data.Nodes.Select(x => x.Id), "節點", errors);
            CheckIds(data.Items.Select(x => x.Id), "道具", errors);
            CheckIds(data.Events.Select(x => x.Id), "事件", errors);
            foreach (var node in data.Nodes)
                foreach (var id in node.EnemyIds)
                    if (data.Enemy(id) == null) errors.Add(node.Id + " 引用未知敵人 " + id);
            if (data.Nodes.Count(x => x.Chapter == 1) != 12 || data.Nodes.Count(x => x.Chapter == 1 && x.Kind == "Save") != 6)
                errors.Add("第一章須有十二節點及六個存檔點。");
            foreach (var skill in data.Skills)
                if (skill.HpCost < 0 || skill.MpCost < 0) errors.Add(skill.Id + " 成本不可為負。");
            foreach (var chapter in data.Nodes.GroupBy(x => x.Chapter))
                if (chapter.Count(x => x.IsMemorySite) != 1) errors.Add("每章須有一個特殊記憶位置。");
            return errors;
        }
        private static void CheckIds(IEnumerable<string> ids, string kind, List<string> errors)
        {
            var all = ids.ToList();
            if (all.Any(string.IsNullOrWhiteSpace) || all.Distinct().Count() != all.Count) errors.Add(kind + " ID 空白或重複。");
        }
    }
}
