using System.Linq;
using HGame.Core;
using NUnit.Framework;

namespace HGame.Tests
{
    public class BattleFlowTests
    {
        private GameData data;
        [SetUp] public void SetUp() { data = DemoTestFixture.Data(); }

        [Test] public void PhysicalPaymentCannotKnockOutHero_AndOrdinaryMpZeroIsNotDefeat()
        {
            var session = DemoTestFixture.Battle(data, hp: 5, mp: 0);
            var rejected = session.Execute(new GameCommand(CommandKind.Skill, "qa-enemy", "slash"));
            Assert.That(rejected.Success, Is.False);
            Assert.That(session.Snapshot.Run.Hp, Is.EqualTo(5));
            DemoTestFixture.Do(session, CommandKind.Attack, "qa-enemy");
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.Zero);
            Assert.That(session.Snapshot.Run.Battle.IsTerminal, Is.False);
        }

        [Test] public void ActiveEntryKeepsHp_AndDoesNotCountAsFinalDefeat()
        {
            var session = DemoTestFixture.Battle(data, hp: 37);
            int uses = session.Snapshot.Run.HDailyUsesRemaining;
            DemoTestFixture.Do(session, CommandKind.EnterDeep);
            Assert.That(session.Snapshot.Run.Battle.Phase, Is.EqualTo(BattlePhase.Deep));
            Assert.That(session.Snapshot.Run.Battle.EntryReason, Is.EqualTo(EntryReason.Active));
            Assert.That(session.Snapshot.Run.Hp, Is.EqualTo(37));
            Assert.That(session.Snapshot.Run.Mp, Is.EqualTo(19));
            Assert.That(session.Snapshot.Run.HDailyUsesRemaining, Is.EqualTo(uses - 1));
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.Zero);
        }

        [Test] public void KnockdownHVictoryDoesNotHealOrRepeatDefeat_AndZeroHpCannotExplore()
        {
            var enemy = DemoTestFixture.Enemy(hp: 100, damage: 150);
            var session = DemoTestFixture.Battle(data, hp: 30, enemies: new[] { enemy });
            DemoTestFixture.Do(session, CommandKind.Defend);
            Assert.That(session.Snapshot.Run.Battle.AwaitingKnockdownChoice, Is.True);
            DemoTestFixture.Do(session, CommandKind.AcceptDeep);
            Assert.That(session.Snapshot.Run.Battle.EntryReason, Is.EqualTo(EntryReason.Knockdown));
            while (!session.Snapshot.Run.Battle.IsTerminal)
                DemoTestFixture.Skill(session, "h_touch");
            var won = session.Snapshot.Run;
            Assert.That(won.Battle.Outcome, Is.EqualTo(BattleOutcome.HVictory));
            Assert.That(won.Hp, Is.Zero);
            Assert.That(won.FinalDefeats, Is.Zero);
            string next = data.Nodes.First(n => n.Chapter == 1 && n.Index > data.Node(won.CurrentNodeId).Index).Id;
            Assert.That(session.Execute(new GameCommand(CommandKind.EnterNode, next)).Success, Is.False);
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.Zero);
            DemoTestFixture.Do(session, CommandKind.ReturnTown);
            Assert.That(session.Snapshot.Run.Location, Is.EqualTo(GameLocation.Town));
            Assert.That(session.Snapshot.Run.Hp, Is.Zero);
        }

        [Test] public void LastMpCannotBePaid_ButWaitingStillAllowsAnOrderedDefeat()
        {
            var session = DemoTestFixture.Battle(data, mp: 1, phase: BattlePhase.Deep,
                enemies: new[] { DemoTestFixture.Enemy(hAttack: 100) });
            Assert.That(session.Execute(new GameCommand(CommandKind.Skill, "qa-enemy", "h_touch")).Success, Is.False);
            Assert.That(session.Snapshot.Run.Mp, Is.EqualTo(1));
            DemoTestFixture.Do(session, CommandKind.DeepWait);
            Assert.That(session.Snapshot.Run.Battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.EqualTo(1));
            Assert.That(session.Execute(new GameCommand(CommandKind.DeepWait)).Success, Is.False);
            Assert.That(session.Snapshot.Run.FinalDefeats, Is.EqualTo(1));
        }

        [Test] public void FinalEnemyHDefeatStopsEnemyTurnAndCannotAwardTwice()
        {
            var enemy = DemoTestFixture.Enemy(stamina: 1, hAttack: 100);
            enemy.H = 20;
            var session = DemoTestFixture.Battle(data, mp: 2, phase: BattlePhase.Deep, enemies: new[] { enemy });
            DemoTestFixture.Skill(session, "h_touch");
            var won = session.Snapshot.Run;
            Assert.That(won.Battle.Outcome, Is.EqualTo(BattleOutcome.HVictory));
            Assert.That(won.Mp, Is.EqualTo(1), "A terminal winning skill must stop the enemy action.");
            Assert.That(won.HWins, Is.EqualTo(1));
            int experience = won.HExp;
            Assert.That(session.Execute(new GameCommand(CommandKind.Skill, "qa-enemy", "h_touch")).Success, Is.False);
            Assert.That(session.Snapshot.Run.HExp, Is.EqualTo(experience));
            Assert.That(session.Snapshot.Run.HWins, Is.EqualTo(1));
        }

        [TestCase(100, 3, 0)]
        [TestCase(75, 3, 75)]
        [TestCase(50, 2, 50)]
        public void DamageTransferUsesOneSharedProgressBudget(int hp, int expectedStamina, int expectedH)
        {
            var session = DemoTestFixture.Battle(data, enemies: new[] { DemoTestFixture.Enemy(hp: hp) });
            DemoTestFixture.Do(session, CommandKind.EnterDeep);
            var enemy = session.Snapshot.Run.Battle.Enemies.Single();
            Assert.That(enemy.Stamina, Is.EqualTo(expectedStamina));
            Assert.That(enemy.H, Is.EqualTo(expectedH));
        }

        [Test] public void DeadEnemiesDoNotJoinDeep_AndOneLivingEnemyDefeatIsNotWholeBattleVictory()
        {
            var dead = DemoTestFixture.Enemy("dead", hp: 0);
            var first = DemoTestFixture.Enemy("first", stamina: 1); first.H = 20;
            var second = DemoTestFixture.Enemy("second", stamina: 1);
            var session = DemoTestFixture.Battle(data, phase: BattlePhase.Deep, enemies: new[] { first, second });
            DemoTestFixture.Skill(session, "h_touch", "first");
            Assert.That(session.Snapshot.Run.Battle.IsTerminal, Is.False);
            Assert.That(session.Snapshot.Run.HWins, Is.Zero);
            var normal = DemoTestFixture.Battle(data, enemies: new[] { dead, second });
            DemoTestFixture.Do(normal, CommandKind.EnterDeep);
            Assert.That(normal.Snapshot.Run.Battle.Enemies.Any(e => e.InstanceId == "dead" && e.Stamina > 0), Is.False);
        }

        [Test] public void StunVictoryRequiresOverlap_AndBossCanBeStunned()
        {
            var a = DemoTestFixture.Enemy("a", stunThreshold: 60);
            var b = DemoTestFixture.Enemy("b", stunThreshold: 90);
            var session = DemoTestFixture.Battle(data, job: Job.Dancer, enemies: new[] { a, b });
            DemoTestFixture.Skill(session, "dance");
            DemoTestFixture.Skill(session, "dance");
            Assert.That(session.Snapshot.Run.Battle.IsTerminal, Is.False);
            DemoTestFixture.Skill(session, "dance");
            Assert.That(session.Snapshot.Run.Battle.Outcome, Is.EqualTo(BattleOutcome.StunVictory));
            var boss = DemoTestFixture.Battle(data, job: Job.Dancer,
                enemies: new[] { DemoTestFixture.Enemy(stunThreshold: 150) });
            for (int i = 0; i < 4; i++) DemoTestFixture.Skill(boss, "dance");
            Assert.That(boss.Snapshot.Run.Battle.Outcome, Is.EqualTo(BattleOutcome.StunVictory));
        }
    }
}
