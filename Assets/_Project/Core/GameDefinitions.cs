using System;
using System.Collections.Generic;
using System.Linq;

namespace HGame.Core
{
    [Serializable] public class BalanceConfig
    {
        public int Version = 1, InitialHp = 100, InitialMp = 20, InitialGold = 60, InitialVirtue = 50;
        public int WorkGold = 45, AdultWorkGold = 25, AdultWorkHExp = 10, ExpPerLevel = 20, VictoryExp = 10;
        public int HpPotionPrice = 20, MpPotionPrice = 30, HpPotionRestore = 40, MpPotionRestore = 6;
        public int HighVirtue = 70, LowVirtue = 30, RequiredRewinds = 3, RequiredMemories = 1, RequiredDefeats = 2;
        public int HLevelRequired = 2, HUseBase = 2, HeroHThresholdMpLoss = 3, StunDuration = 2;
        public int MaxDays = 20, MaxNewNodes = 4, SupportGoldCost = 10, SupplyGold = 10, SupplyVirtue = 10;
        public List<int> StageOpenDays = new List<int> { 1, 3, 6 };
    }
    [Serializable] public class ItemDefinition
    {
        public string Id = "", Name = "", Description = "";
        public int Price, RestoreHp, RestoreMp;
    }
    [Serializable] public class EnemyDefinition
    {
        public string Id = "", Name = "", Description = "";
        public int Hp, Damage, Stamina, HAttack, StunThreshold;
        public int CorruptedDamageBonus, CorruptedHAttackBonus;
        public bool IsBoss;
    }
    [Serializable] public class SkillDefinition
    {
        public string Id = "", Name = "", Description = "", JobId = "Any", PhaseId = "Normal", Effect = "Damage";
        public int HpCost, MpCost, Damage, HIncrease, StunIncrease;
        public bool AllTargets, MagicOnly;
    }
    [Serializable] public class NodeDefinition
    {
        public string Id = "", Name = "", Kind = "Battle", Description = "", MemoryId = "";
        public int Chapter = 1, Index, Stage = 1;
        public bool IsMemorySite, IsBoss;
        public List<string> EnemyIds = new List<string>();
    }
    [Serializable] public class ChoiceDefinition
    {
        public string Id = "", Name = "", Description = "";
        public int GoldDelta, VirtueDelta, HExpDelta;
    }
    [Serializable] public class EventDefinition
    {
        public string Id = "", Name = "", Body = "", Kind = "";
        public List<ChoiceDefinition> Choices = new List<ChoiceDefinition>();
    }
    [Serializable] public class BalanceFile { public BalanceConfig Balance = new BalanceConfig(); public List<ItemDefinition> Items = new List<ItemDefinition>(); }
    [Serializable] public class EnemyFile { public List<EnemyDefinition> Items = new List<EnemyDefinition>(); }
    [Serializable] public class SkillFile { public List<SkillDefinition> Items = new List<SkillDefinition>(); }
    [Serializable] public class DungeonFile { public List<NodeDefinition> Items = new List<NodeDefinition>(); }
    [Serializable] public class EventFile { public List<EventDefinition> Items = new List<EventDefinition>(); }
    [Serializable] public class GameData
    {
        public BalanceConfig Balance = new BalanceConfig();
        public List<ItemDefinition> Items = new List<ItemDefinition>();
        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
        public List<SkillDefinition> Skills = new List<SkillDefinition>();
        public List<NodeDefinition> Nodes = new List<NodeDefinition>();
        public List<EventDefinition> Events = new List<EventDefinition>();
        public EnemyDefinition Enemy(string id) { return Enemies.FirstOrDefault(x => x.Id == id); }
        public SkillDefinition Skill(string id) { return Skills.FirstOrDefault(x => x.Id == id); }
        public NodeDefinition Node(string id) { return Nodes.FirstOrDefault(x => x.Id == id); }
        public ItemDefinition Item(string id) { return Items.FirstOrDefault(x => x.Id == id); }
        public EventDefinition Event(string id) { return Events.FirstOrDefault(x => x.Id == id); }
    }
}
