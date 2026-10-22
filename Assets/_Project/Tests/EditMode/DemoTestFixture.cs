using System;
using System.IO;
using System.Linq;
using HGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace HGame.Tests
{
    // Boundary fixtures may set initial resources; the reachability scenario starts from NewGame.
    internal static class DemoTestFixture
    {
        public static GameData Data()
        {
            string root = Path.Combine(Application.dataPath, "_Project/Resources/Demo");
            return new GameData
            {
                Balance = Read<BalanceFile>(root, "balance.json").Balance,
                Items = Read<BalanceFile>(root, "balance.json").Items,
                Enemies = Read<EnemyFile>(root, "enemies.json").Items,
                Skills = Read<SkillFile>(root, "skills.json").Items,
                Nodes = Read<DungeonFile>(root, "dungeon.json").Items,
                Events = Read<EventFile>(root, "events.json").Items
            };
        }
        private static T Read<T>(string root, string name)
        {
            string path = Path.Combine(root, name);
            Assert.That(File.Exists(path), Is.True, "Missing approved Demo data: " + path);
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        public static GameSession Battle(GameData data, int hp = 100, int mp = 20,
            Job job = Job.Swordsman, BattlePhase phase = BattlePhase.Normal, params EnemyState[] enemies)
        {
            var fresh = GameSession.NewGame(data, "qa-boundary");
            RunState run = fresh.CaptureRun();
            NodeDefinition node = data.Nodes.First(n => n.Chapter == 1 && n.Kind == "Battle");
            run.Hp = hp; run.Mp = mp; run.Job = job;
            run.Location = GameLocation.Battle; run.CurrentNodeId = node.Id;
            run.Battle = new BattleState { NodeId = node.Id, Phase = phase };
            run.Battle.Enemies.AddRange(enemies.Length == 0 ? new[] { Enemy() } : enemies);
            return new GameSession(data, run, fresh.CaptureMeta(), fresh.CaptureProfile());
        }
        public static EnemyState Enemy(string id = "qa-enemy", int hp = 100, int maxHp = 100,
            int stamina = 3, int damage = 0, int hAttack = 0, int stunThreshold = 60)
        {
            return new EnemyState
            {
                InstanceId = id, DefinitionId = "qa", Name = "QA enemy", Hp = hp, MaxHp = maxHp,
                Stamina = stamina, InitialStamina = stamina, Damage = damage, HAttack = hAttack,
                StunThreshold = stunThreshold
            };
        }
        public static CommandResult Do(GameSession session, CommandKind kind,
            string target = "", string choice = "", int quantity = 1)
        {
            CommandResult result = session.Execute(new GameCommand(kind, target, choice, quantity));
            Assert.That(result.Success, Is.True, kind + " rejected: " + result.Message);
            return result;
        }
        public static void ToNextMorning(GameSession session)
        {
            for (int i = 0; i < 4; i++)
            {
                if (session.Snapshot.Run.Slot == TimeSlot.Morning) return;
                Do(session, session.Snapshot.Run.Slot == TimeSlot.LateNight ? CommandKind.Rest : CommandKind.Wait);
            }
            Assert.That(session.Snapshot.Run.Slot, Is.EqualTo(TimeSlot.Morning));
        }
        public static void Skill(GameSession session, string id, string target = "qa-enemy")
        { Do(session, CommandKind.Skill, target, id); }
        public static string Fingerprint(object state) { return JsonUtility.ToJson(state); }
    }
}
