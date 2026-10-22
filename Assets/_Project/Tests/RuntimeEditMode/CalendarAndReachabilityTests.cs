using System.Linq;
using HGame.Core;
using HGame.Unity;
using NUnit.Framework;

namespace HGame.Tests.Runtime
{
    public class CalendarAndReachabilityTests
    {
        private GameData data;
        [SetUp] public void SetUp() { data = DefinitionLoader.Load(); }
        private static void Do(GameSession session, CommandKind kind, string target = "", string choice = "")
        {
            var result = session.Execute(new GameCommand(kind, target, choice));
            Assert.That(result.Success, Is.True, kind + " " + target + ": " + result.Message);
        }
        private static void Morning(GameSession session)
        {
            for (int i = 0; i < 4 && session.Snapshot.Run.Slot != TimeSlot.Morning; i++)
                Do(session, session.Snapshot.Run.Slot == TimeSlot.LateNight ? CommandKind.Rest : CommandKind.Wait);
            Assert.That(session.Snapshot.Run.Slot, Is.EqualTo(TimeSlot.Morning));
        }
        private static void Win(GameSession session, string skill = null)
        {
            for (int i = 0; i < 30 && session.Snapshot.Run.Location == GameLocation.Battle; i++)
            {
                var run = session.Snapshot.Run;
                var enemy = run.Battle.Enemies.First(e => run.Battle.Phase == BattlePhase.Deep ? e.Stamina > 0 : e.Hp > 0);
                Do(session, skill == null ? CommandKind.Attack : CommandKind.Skill, enemy.InstanceId, skill ?? "");
            }
            Assert.That(session.Snapshot.Run.Location, Is.EqualTo(GameLocation.Dungeon), "Normal actions must reach victory.");
        }

        [Test] public void RevisitingClearedBattleDoesNotGrantExperienceAgain()
        {
            var session = GameSession.NewGame(data, "qa-revisit");
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n01"); Win(session);
            int normalExp = session.Snapshot.Run.NormalExp;
            int newNodes = session.Snapshot.Run.ExpeditionNewNodes;
            Do(session, CommandKind.EnterNode, "c1_n01"); Win(session);
            Assert.That(session.Snapshot.Run.NormalExp, Is.EqualTo(normalExp));
            Assert.That(session.Snapshot.Run.ExpeditionNewNodes, Is.EqualTo(newNodes));
            Do(session, CommandKind.EnterNode, "c1_n01"); Do(session, CommandKind.EnterDeep); Win(session, "h_touch");
            Assert.That(session.Snapshot.Run.HExp, Is.Zero, "Changing the victory method cannot reclaim node EXP.");
            Assert.That(session.Snapshot.Run.NormalExp, Is.EqualTo(normalExp));
        }

        [Test] public void LateNightBlocksEveryOutdoorCommand_ButRestRecoversAndAdvances()
        {
            var fresh = GameSession.NewGame(data, "qa-night"); var run = fresh.CaptureRun();
            run.Slot = TimeSlot.LateNight; run.Hp = 1; run.Mp = 1; run.HDailyUsesRemaining = 0;
            var session = new GameSession(data, run, fresh.CaptureMeta(), fresh.CaptureProfile());
            foreach (var command in new[] { new GameCommand(CommandKind.Work), new GameCommand(CommandKind.AdultWork),
                new GameCommand(CommandKind.Buy, "hp_potion"), new GameCommand(CommandKind.Depart) })
                Assert.That(session.Execute(command).Success, Is.False, command.Kind.ToString());
            Assert.That(session.Snapshot.Run.Gold, Is.EqualTo(60));
            Do(session, CommandKind.Rest);
            Assert.That(session.Snapshot.Run.Day, Is.EqualTo(2));
            Assert.That(session.Snapshot.Run.Slot, Is.EqualTo(TimeSlot.Morning));
            Assert.That(session.Snapshot.Run.Hp, Is.EqualTo(100));
            Assert.That(session.Snapshot.Run.Mp, Is.EqualTo(20));
            Assert.That(session.Snapshot.Run.HDailyUsesRemaining, Is.EqualTo(2));
        }

        [TestCase(2, "c1_n05", false)]
        [TestCase(3, "c1_n05", true)]
        [TestCase(5, "c1_n09", false)]
        [TestCase(6, "c1_n09", true)]
        public void StageOpenDateCannotBeBypassedByHavingEarlierProgress(int day, string target, bool allowed)
        {
            var fresh = GameSession.NewGame(data, "qa-open-date"); var run = fresh.CaptureRun();
            run.Day = day;
            int index = data.Node(target).Index;
            run.Chapters[0].ClearedNodeIds.AddRange(data.Nodes.Where(n => n.Chapter == 1 && n.Index < index).Select(n => n.Id));
            var session = new GameSession(data, run, fresh.CaptureMeta(), fresh.CaptureProfile());
            Do(session, CommandKind.Depart);
            Assert.That(session.Execute(new GameCommand(CommandKind.EnterNode, target)).Success, Is.EqualTo(allowed));
            Assert.That(data.Nodes.Count(n => n.Chapter == 1 && n.Kind == "Save"), Is.EqualTo(6));
        }

        [Test] public void DeadlineWithoutBossClearIsGameOverAndCannotUseGeneralFallback()
        {
            var fresh = GameSession.NewGame(data, "qa-deadline"); var run = fresh.CaptureRun();
            run.Day = 20; run.Slot = TimeSlot.LateNight;
            var session = new GameSession(data, run, fresh.CaptureMeta(), fresh.CaptureProfile());
            Do(session, CommandKind.Rest);
            Assert.That(session.Snapshot.Run.Location, Is.EqualTo(GameLocation.GameOver));
            Assert.That(session.Snapshot.Run.Route, Is.Not.EqualTo(Route.General));
            Assert.That(session.Execute(new GameCommand(CommandKind.Work)).Success, Is.False);
            Assert.That(session.Execute(new GameCommand(CommandKind.AcceptRoute)).Success, Is.False);
        }

        [Test] public void NormalCommandsReachMagicBranchAndDaySixBoss_ThenSecondChapterMemory()
        {
            var session = GameSession.NewGame(data, "qa-normal-path");
            for (int i = 0; i < 2; i++)
            {
                Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n01");
                Do(session, CommandKind.Abandon); Morning(session);
            }
            var baseline = session.CaptureRun();
            Assert.That(baseline.FinalDefeats, Is.EqualTo(2));
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n01"); Do(session, CommandKind.Abandon);
            Assert.That(session.Load(baseline, LoadIntent.ManualLoad, "path-r1").Success, Is.True);
            for (int i = 2; i <= 3; i++)
            {
                Do(session, CommandKind.Work);
                Assert.That(session.Load(baseline, LoadIntent.ManualLoad, "path-r" + i).Success, Is.True);
            }
            Assert.That(session.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(3));
            Assert.That(session.Snapshot.Meta.SpecialMemoryIds, Does.Contain("memory_c1"));
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Unbound));
            Do(session, CommandKind.AdultWork); Morning(session);
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n01");
            Do(session, CommandKind.EnterDeep); Win(session, "h_touch");
            Do(session, CommandKind.EnterNode, "c1_n02"); Do(session, CommandKind.EnterNode, "c1_n03");
            Do(session, CommandKind.ChooseSupply, "", "take"); Do(session, CommandKind.EnterNode, "c1_n04");
            Assert.That(session.Execute(new GameCommand(CommandKind.EnterNode, "c1_n05")).Success, Is.False,
                "A single expedition cannot add more than one stage.");
            Do(session, CommandKind.ReturnTown); Morning(session);
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n05"); Win(session);
            Do(session, CommandKind.EnterNode, "c1_n06"); Do(session, CommandKind.EnterNode, "c1_n07");
            Do(session, CommandKind.ChooseSupply, "", "take"); Do(session, CommandKind.EnterNode, "c1_n08");
            Do(session, CommandKind.ReturnTown);
            Assert.That(session.Snapshot.Run.Virtue, Is.EqualTo(30));
            Assert.That(session.Snapshot.Run.HExp, Is.EqualTo(20));
            Assert.That(session.Snapshot.Run.HWins, Is.EqualTo(1));
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.EqualTo(2));
            var beforeBranch = session.CaptureRun();
            Do(session, CommandKind.AcceptRoute);
            Assert.That(session.Snapshot.Run.PlayerAware, Is.True);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Corrupted));
            Assert.That(session.Load(beforeBranch, LoadIntent.ManualLoad, "undo-choice").Success, Is.True);
            Assert.That(session.Snapshot.Run.PlayerAware, Is.False);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Unbound));
            Morning(session); Do(session, CommandKind.SwitchJob, "Dancer");
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c1_n09"); Win(session, "dance");
            Do(session, CommandKind.EnterNode, "c1_n10"); Do(session, CommandKind.EnterNode, "c1_n11"); Win(session, "dance");
            Assert.That(session.Snapshot.Run.Battle.Outcome, Is.EqualTo(BattleOutcome.StunVictory));
            Do(session, CommandKind.EnterNode, "c1_n12"); Do(session, CommandKind.ReturnTown);
            Assert.That(session.Snapshot.Run.Day, Is.EqualTo(6));
            Assert.That(session.Snapshot.Run.Chapter, Is.EqualTo(1));
            int gold = session.Snapshot.Run.Gold, exp = session.Snapshot.Run.NormalExp, hExp = session.Snapshot.Run.HExp;
            Do(session, CommandKind.RestToChapterEnd);
            Assert.That(session.Snapshot.Run.Chapter, Is.EqualTo(2));
            Assert.That(session.Snapshot.Run.Day, Is.EqualTo(1));
            Assert.That(session.Snapshot.Run.Gold, Is.EqualTo(gold));
            Assert.That(session.Snapshot.Run.NormalExp, Is.EqualTo(exp));
            Assert.That(session.Snapshot.Run.HExp, Is.EqualTo(hExp));
            var secondBaseline = session.CaptureRun();
            Do(session, CommandKind.Depart); Do(session, CommandKind.EnterNode, "c2_n01"); Do(session, CommandKind.Abandon);
            Assert.That(session.Load(secondBaseline, LoadIntent.ManualLoad, "path-chapter2").Success, Is.True);
            CollectionAssert.AreEquivalent(new[] { "memory_c1", "memory_c2" }, session.Snapshot.Meta.SpecialMemoryIds);
            Do(session, CommandKind.CompleteDemo);
            Assert.That(session.Snapshot.Run.Location, Is.EqualTo(GameLocation.DemoComplete));
        }
    }
}
