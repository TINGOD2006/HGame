using System.Collections.Generic;
using System.Linq;
using HGame.Core;
using NUnit.Framework;

namespace HGame.Tests
{
    public class RewindAndRouteTests
    {
        private GameData data;
        [SetUp] public void SetUp() { data = DemoTestFixture.Data(); }

        private static MetaState Timeline()
        {
            return new MetaState
            {
                CampaignId = "qa-timeline", LastHeadId = "failure",
                Timeline = new List<TimelineNode>
                {
                    new TimelineNode { Id = "root" },
                    new TimelineNode { Id = "before", ParentId = "root" },
                    new TimelineNode { Id = "failure", ParentId = "before" },
                    new TimelineNode { Id = "after", ParentId = "failure" },
                    new TimelineNode { Id = "side", ParentId = "root" }
                },
                Receipts = new List<FailureReceipt>
                {
                    new FailureReceipt { Id = "receipt-1", CampaignId = "qa-timeline", Chapter = 1,
                        TimelineNodeId = "failure", EventId = "site-1", MemoryId = "memory-1" }
                }
            };
        }
        private static RunState At(string head)
        { return new RunState { CampaignId = "qa-timeline", TimelineHeadId = head }; }

        [TestCase("failure", "before", true, true)]
        [TestCase("failure", "side", true, true)]
        [TestCase("failure", "failure", false, false)]
        [TestCase("before", "after", false, false)]
        [TestCase("after", "failure", true, false)]
        public void LoadCountsDiscardedExperience_AndOnlyErasedFailureProducesMemory(
            string from, string to, bool rewind, bool memory)
        {
            var meta = Timeline();
            var plan = RewindRules.PlanLoad(At(from), meta, At(to), LoadIntent.ManualLoad);
            Assert.That(plan.IsRewind, Is.EqualTo(rewind));
            var outcome = RewindRules.ApplyLoad(plan, At(to), meta, "tx-boundary");
            Assert.That(outcome.Meta.EffectiveRewinds, Is.EqualTo(rewind ? 1 : 0));
            Assert.That(outcome.Meta.SpecialMemoryIds.Contains("memory-1"), Is.EqualTo(memory));
        }

        [Test] public void ContinueAndForeignJourneyDoNotCountAsRewind()
        {
            var meta = Timeline();
            Assert.That(RewindRules.PlanLoad(At("failure"), meta, At("before"), LoadIntent.Continue).IsRewind, Is.False);
            var foreign = At("before"); foreign.CampaignId = "another-journey";
            var plan = RewindRules.PlanLoad(At("failure"), meta, foreign, LoadIntent.ManualLoad);
            Assert.That(plan.IsRewind, Is.False);
            Assert.That(plan.CampaignSwitch, Is.True);
        }

        [Test] public void TransactionReplayCannotDuplicateRewindsOrChapterMemory()
        {
            var meta = Timeline();
            var target = At("before");
            var plan = RewindRules.PlanLoad(At("failure"), meta, target, LoadIntent.ManualLoad);
            var first = RewindRules.ApplyLoad(plan, target, meta, "stable-tx");
            var repeated = RewindRules.ApplyLoad(plan, target, first.Meta, "stable-tx");
            Assert.That(repeated.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(repeated.Meta.SpecialMemoryIds.Count(id => id == "memory-1"), Is.EqualTo(1));
            Assert.That(repeated.Meta.ProcessedReceiptIds.Count(id => id == "receipt-1"), Is.EqualTo(1));
            Assert.That(repeated.AlreadyApplied, Is.True);
        }

        [Test] public void DifferentChapterReceiptsRemainDifferentMemories()
        {
            var meta = Timeline();
            meta.Receipts.Add(new FailureReceipt { Id = "receipt-2", CampaignId = "qa-timeline", Chapter = 2,
                TimelineNodeId = "after", EventId = "site-2", MemoryId = "memory-2" });
            var target = At("before");
            var outcome = RewindRules.ApplyLoad(
                RewindRules.PlanLoad(At("after"), meta, target, LoadIntent.ManualLoad), target, meta, "chapter-tx");
            CollectionAssert.AreEquivalent(new[] { "memory-1", "memory-2" }, outcome.Meta.SpecialMemoryIds);
        }

        private GameSession Eligible(Route route = Route.Unbound)
        {
            var session = GameSession.NewGame(data, "qa-route");
            var run = session.CaptureRun(); var meta = session.CaptureMeta();
            run.Virtue = 30; run.HExp = 20; run.HWins = 1; run.FinalDefeats = 2; run.Route = route;
            meta.EffectiveRewinds = 3; meta.SpecialMemoryIds.Add("ch1-memory");
            return new GameSession(data, run, meta, session.CaptureProfile());
        }

        [Test] public void AcceptingTransformationIsExplicitAndReplaySafe()
        {
            var session = Eligible();
            var baseline = session.CaptureRun();
            DemoTestFixture.Do(session, CommandKind.DeclineRoute);
            Assert.That(session.Snapshot.Run.PlayerAware, Is.False);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Unbound));
            DemoTestFixture.Do(session, CommandKind.AcceptRoute);
            Assert.That(session.Snapshot.Run.PlayerAware, Is.True);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Corrupted));
            string locked = DemoTestFixture.Fingerprint(session.Snapshot);
            Assert.That(session.Execute(new GameCommand(CommandKind.AcceptRoute)).Success, Is.False);
            Assert.That(DemoTestFixture.Fingerprint(session.Snapshot), Is.EqualTo(locked));
            Assert.That(session.Load(baseline, LoadIntent.ManualLoad, "unchoose-route").Success, Is.True);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(Route.Unbound));
            Assert.That(session.Snapshot.Run.PlayerAware, Is.False);
            Assert.That(session.Snapshot.Meta.SpecialMemoryIds, Does.Contain("ch1-memory"));
            Assert.That(session.Snapshot.Meta.EffectiveRewinds, Is.GreaterThanOrEqualTo(3));
        }

        [TestCase("rewinds")]
        [TestCase("memory")]
        [TestCase("defeats")]
        [TestCase("virtue")]
        [TestCase("growth")]
        [TestCase("victory")]
        [TestCase("other-route")]
        public void MissingAnyNecessaryGatePreventsTransformation(string missing)
        {
            var eligible = Eligible(); var run = eligible.CaptureRun(); var meta = eligible.CaptureMeta();
            switch (missing)
            {
                case "rewinds": meta.EffectiveRewinds = 2; break;
                case "memory": meta.SpecialMemoryIds.Clear(); break;
                case "defeats": run.FinalDefeats = 1; break;
                case "virtue": run.Virtue = 31; break;
                case "growth": run.HExp = 0; break;
                case "victory": run.HWins = 0; break;
                case "other-route": run.Route = Route.Dark; break;
            }
            var session = new GameSession(data, run, meta, eligible.CaptureProfile());
            Assert.That(session.Execute(new GameCommand(CommandKind.AcceptRoute)).Success, Is.False, missing);
            Assert.That(session.Snapshot.Run.PlayerAware, Is.False);
            Assert.That(session.Snapshot.Run.Route, Is.EqualTo(run.Route));
        }

        [Test] public void SnapshotsDoNotLeakMutableCollections_AndNewJourneyKeepsOnlyProfileUnlocks()
        {
            var session = Eligible();
            var view = session.Snapshot;
            view.Run.Gold = 999;
            view.Meta.SpecialMemoryIds.Clear();
            Assert.That(session.Snapshot.Run.Gold, Is.EqualTo(60));
            Assert.That(session.Snapshot.Meta.SpecialMemoryIds, Has.Count.EqualTo(1));
            var profile = session.CaptureProfile(); profile.UnlockedGalleryIds.Add("qa-gallery");
            var next = GameSession.NewGame(data, "new-journey", profile);
            Assert.That(next.Snapshot.Meta.SpecialMemoryIds, Is.Empty);
            Assert.That(next.Snapshot.Meta.EffectiveRewinds, Is.Zero);
            Assert.That(next.Snapshot.Run.PlayerAware, Is.False);
            Assert.That(next.Snapshot.Profile.UnlockedGalleryIds, Does.Contain("qa-gallery"));
            profile.UnlockedGalleryIds.Clear();
            Assert.That(next.Snapshot.Profile.UnlockedGalleryIds, Does.Contain("qa-gallery"));
        }
    }
}
