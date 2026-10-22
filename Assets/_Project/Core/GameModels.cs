using System;
using System.Collections.Generic;

namespace HGame.Core
{
    public enum TimeSlot { Morning, Noon, Evening, LateNight }
    public enum Job { Swordsman, Dancer }
    public enum GameLocation { Town, Dungeon, Battle, GameOver, DemoComplete }
    public enum BattlePhase { Normal, Deep }
    public enum EntryReason { None, Active, Knockdown }
    public enum BattleOutcome { None, NormalVictory, StunVictory, HVictory, Defeat }
    public enum Route { Unbound, General, Pure, Dark, Succubus, Corrupted }
    public enum LoadIntent { Continue, ManualLoad }

    [Serializable] public class RunState
    {
        public string CampaignId = "";
        public string TimelineHeadId = "";
        public int Chapter = 1;
        public int Day = 1;
        public TimeSlot Slot = TimeSlot.Morning;
        public GameLocation Location = GameLocation.Town;
        public Job Job = Job.Swordsman;
        public int Hp = 100, MaxHp = 100, Mp = 20, MaxMp = 20;
        public int Gold = 60, Virtue = 50, NormalExp, HExp, NormalWins, HWins, FinalDefeats;
        public int HDailyUsesRemaining = 2;
        public bool IsVirgin = true, PlayerAware, BranchResolved, DemoSegmentVisited;
        public Route Route = Route.Unbound;
        public string DifficultyId = "normal";
        public string CurrentNodeId = "", LastSaveNodeId = "", PendingSupplyNodeId = "";
        public int ExpeditionNewNodes;
        public BattleState Battle;
        public List<InventoryEntry> Inventory = new List<InventoryEntry>();
        public List<ChapterProgress> Chapters = new List<ChapterProgress>();
        public List<string> ClaimedEvents = new List<string>();
        public List<ExperienceRecord> Experiences = new List<ExperienceRecord>();
        public List<string> Journal = new List<string>();
    }
    [Serializable] public class InventoryEntry { public string Id = ""; public int Count; }
    [Serializable] public class ChapterProgress
    {
        public int Chapter = 1;
        public List<string> ClearedNodeIds = new List<string>();
        public bool BossCleared;
    }
    [Serializable] public class BattleState
    {
        public string NodeId = "";
        public BattlePhase Phase;
        public EntryReason EntryReason;
        public BattleOutcome Outcome;
        public bool AwaitingKnockdownChoice, Defended, IsTerminal, RewardGranted;
        public int HeroH, Turn = 1;
        public List<EnemyState> Enemies = new List<EnemyState>();
    }
    [Serializable] public class EnemyState
    {
        public string InstanceId = "", DefinitionId = "", Name = "";
        public int Hp, MaxHp, Damage, Stamina, InitialStamina, H, HAttack;
        public int StunProgress, StunThreshold, StunTurns;
        public bool Converted;
    }
    [Serializable] public class MetaState
    {
        public string CampaignId = "", LastHeadId = "";
        public int EffectiveRewinds;
        public List<TimelineNode> Timeline = new List<TimelineNode>();
        public List<FailureReceipt> Receipts = new List<FailureReceipt>();
        public List<string> ProcessedReceiptIds = new List<string>();
        public List<string> SpecialMemoryIds = new List<string>();
        public List<string> AppliedTransactionIds = new List<string>();
        public List<ExperienceRecord> Echoes = new List<ExperienceRecord>();
    }
    [Serializable] public class TimelineNode { public string Id = "", ParentId = "", Label = ""; }
    [Serializable] public class FailureReceipt
    {
        public string Id = "", CampaignId = "", TimelineNodeId = "", MemoryId = "", EventId = "";
        public int Chapter;
    }
    [Serializable] public class ExperienceRecord
    {
        public string Id = "", TimelineNodeId = "", EventId = "", Kind = "", Summary = "";
        public int Chapter;
    }
    [Serializable] public class ProfileState
    {
        public int Version = 1;
        public float Volume = 0.6f;
        public List<string> UnlockedGalleryIds = new List<string>();
    }
    [Serializable] public class GameSnapshot
    {
        public RunState Run;
        public MetaState Meta;
        public ProfileState Profile;
    }
}
