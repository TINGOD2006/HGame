using System;
using System.Collections.Generic;
using System.IO;
using HGame.Core;
using HGame.Unity;
using NUnit.Framework;
using UnityEngine;

namespace HGame.Tests.Runtime
{
    public class PersistenceAndGalleryTests
    {
        private string root;
        private GameData data;
        private DiskSaveStore disk;

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "HGame-QA-" + Guid.NewGuid().ToString("N"));
            disk = new DiskSaveStore(root);
            data = DefinitionLoader.Load();
        }

        [TearDown] public void TearDown()
        {
            // Delete only the unique directory created by this fixture, never player data.
            if (root != null && Path.GetFileName(root).StartsWith("HGame-QA-", StringComparison.Ordinal)
                && Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void Do(GameSession session, CommandKind kind, string target = "", string choice = "")
        {
            var result = session.Execute(new GameCommand(kind, target, choice));
            Assert.That(result.Success, Is.True, kind + ": " + result.Message);
        }

        [Test] public void RealDiskSaveSurvivesRepositoryRestart_AndContinueDoesNotCountRewind()
        {
            var session = GameSession.NewGame(data, "qa-disk");
            var repository = new SaveRepository(data, disk);
            repository.Save(session, "manual");
            Do(session, CommandKind.Work);
            repository.Persist(session);
            string saved = JsonUtility.ToJson(session.CaptureRun());
            var restarted = new SaveRepository(data, new DiskSaveStore(root));
            string message;
            var continued = restarted.Load(null, "auto", LoadIntent.Continue, out message);
            Assert.That(JsonUtility.ToJson(continued.CaptureRun()), Is.EqualTo(saved));
            Assert.That(continued.Snapshot.Meta.EffectiveRewinds, Is.Zero);
            continued = restarted.Load(continued, "auto", LoadIntent.Continue, out message);
            Assert.That(continued.Snapshot.Meta.EffectiveRewinds, Is.Zero);
        }

        [Test] public void ColdManualLoadReceivesPersistedFailureMemory_WithoutKeepingRolledBackDefeats()
        {
            var repository = new SaveRepository(data, disk);
            var session = GameSession.NewGame(data, "qa-cold-rewind");
            repository.Save(session, "manual");
            Do(session, CommandKind.Depart);
            Do(session, CommandKind.EnterNode, "c1_n01");
            Do(session, CommandKind.Abandon);
            repository.Persist(session);
            var restarted = new SaveRepository(data, new DiskSaveStore(root));
            string message;
            var loaded = restarted.Load(null, "manual", LoadIntent.ManualLoad, out message);
            Assert.That(loaded.Snapshot.Run.FinalDefeats, Is.Zero);
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.SpecialMemoryIds, Does.Contain("memory_c1"));
            Assert.That(loaded.Snapshot.Run.Route, Is.EqualTo(Route.Unbound));
            Assert.That(loaded.Snapshot.Run.PlayerAware, Is.False);
            loaded = restarted.Load(loaded, "manual", LoadIntent.ManualLoad, out message);
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.SpecialMemoryIds, Has.Count.EqualTo(1));
        }

        [Test] public void CompletedLoadMarkerCannotOverwriteAutoSlotOnRepeatedRepositoryRestart()
        {
            var repository = new SaveRepository(data, disk);
            var session = GameSession.NewGame(data, "qa-completed-load");
            repository.Save(session, "manual");
            Do(session, CommandKind.Depart);
            Do(session, CommandKind.EnterNode, "c1_n01");
            Do(session, CommandKind.Abandon);
            repository.Persist(session);
            string message;
            session = repository.Load(session, "manual", LoadIntent.ManualLoad, out message);
            Assert.That(session.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(session.Snapshot.Meta.SpecialMemoryIds, Does.Contain("memory_c1"));
            Assert.That(JsonUtility.FromJson<LoadTransaction>(disk.Read("pending-load.json")).Id,
                Is.Null.Or.Empty, "The real successful load must have cleared the pending transaction.");
            string expectedRun = JsonUtility.ToJson(session.CaptureRun());
            var expectedMeta = session.CaptureMeta();
            // A Continue may add its own transaction ID; all gameplay metadata must stay identical.
            expectedMeta.AppliedTransactionIds.Clear();
            string expectedGameplayMeta = JsonUtility.ToJson(expectedMeta);
            for (int i = 0; i < 2; i++)
            {
                repository = new SaveRepository(data, new DiskSaveStore(root));
                // Inspect the primary slot directly: backup fallback must not hide a blank overwrite.
                Assert.That(JsonUtility.ToJson(SaveCodec.DecodeRun(disk.Read("auto.json")).Run),
                    Is.EqualTo(expectedRun), "An empty completed-load marker must not be recovered as a transaction.");
                session = repository.Load(null, "auto", LoadIntent.Continue, out message);
                Assert.That(session.Snapshot.Run.CampaignId, Is.EqualTo("qa-completed-load"));
                Assert.That(JsonUtility.ToJson(session.CaptureRun()), Is.EqualTo(expectedRun));
                var actualMeta = session.CaptureMeta();
                Assert.That(actualMeta.EffectiveRewinds, Is.EqualTo(1));
                actualMeta.AppliedTransactionIds.Clear();
                Assert.That(JsonUtility.ToJson(actualMeta), Is.EqualTo(expectedGameplayMeta));
            }
        }

        [Test] public void InterruptedLoadRecoversExactPreparedResult_Once()
        {
            var store = new FailOnceStore();
            var repository = new SaveRepository(data, store);
            var session = GameSession.NewGame(data, "qa-transaction");
            repository.Save(session, "manual");
            Do(session, CommandKind.Depart);
            Do(session, CommandKind.EnterNode, "c1_n01");
            Do(session, CommandKind.Abandon);
            repository.Persist(session);
            // Crash after pending transaction and ResultMeta are durable, before the auto slot commit.
            store.FailNextWrite = "auto.json";
            string message;
            Assert.Throws<IOException>(() => repository.Load(session, "manual", LoadIntent.ManualLoad, out message));
            Assert.That(store.Read("pending-load.json"), Does.Contain("qa-transaction"));
            var recovered = new SaveRepository(data, store);
            var loaded = recovered.Load(null, "auto", LoadIntent.Continue, out message);
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.SpecialMemoryIds, Has.Count.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.ProcessedReceiptIds, Has.Count.EqualTo(1));
            Assert.That(loaded.Snapshot.Run.FinalDefeats, Is.Zero);
            string meta = JsonUtility.ToJson(loaded.CaptureMeta());
            var recoveredAgain = new SaveRepository(data, store);
            loaded = recoveredAgain.Load(null, "auto", LoadIntent.Continue, out message);
            // Continue may record its own load transaction, but cannot duplicate history or memory.
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.SpecialMemoryIds, Has.Count.EqualTo(1));
            Assert.That(loaded.Snapshot.Meta.ProcessedReceiptIds, Has.Count.EqualTo(1));
        }

        [Test] public void AtomicReplacementKeepsReadableBackup_AndCorruptionDoesNotDeleteOriginal()
        {
            var repository = new SaveRepository(data, disk);
            var session = GameSession.NewGame(data, "qa-backup");
            repository.Save(session, "manual");
            Do(session, CommandKind.Work);
            repository.Save(session, "manual");
            Assert.That(disk.Exists("manual.json.bak"), Is.True);
            Assert.That(SaveCodec.DecodeRun(disk.Read("manual.json.bak")).Run.Gold, Is.EqualTo(60));
            disk.WriteAtomic("manual.json", "{not-valid-json");
            var recovered = repository.ReadSlot("manual");
            Assert.That(recovered.Run.Gold, Is.EqualTo(105));
            Assert.That(disk.Read("manual.json"), Is.EqualTo("{not-valid-json"));
        }

        [Test] public void IncompatibleSaveWithoutBackupIsReportedAndPreserved()
        {
            var repository = new SaveRepository(data, disk);
            var envelope = new SaveEnvelope { Version = 999, Run = GameSession.NewGame(data, "qa-version").CaptureRun() };
            string incompatible = SaveCodec.Encode(envelope);
            disk.WriteAtomic("manual.json", incompatible);
            Assert.Throws<InvalidDataException>(() => repository.ReadSlot("manual"));
            Assert.That(disk.Read("manual.json"), Is.EqualTo(incompatible));
        }

        [Test] public void GalleryPlaySkipCloseCannotChangeFormalRunMetaOrProfile()
        {
            var profile = new ProfileState(); profile.UnlockedGalleryIds.Add("qa-scene");
            var session = GameSession.NewGame(data, "qa-gallery", profile);
            var repository = new SaveRepository(data, disk);
            repository.Persist(session);
            string before = JsonUtility.ToJson(session.Snapshot);
            string persisted = disk.Read("campaigns/qa-gallery/meta.json");
            var gallery = new GallerySession(session.CaptureProfile());
            Assert.That(gallery.Play("locked-scene"), Is.False);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(gallery.Play("qa-scene"), Is.True);
                gallery.Skip(); gallery.Close();
            }
            Assert.That(JsonUtility.ToJson(session.Snapshot), Is.EqualTo(before));
            Assert.That(disk.Read("campaigns/qa-gallery/meta.json"), Is.EqualTo(persisted));
            Assert.That(gallery.IsPlaying, Is.False);
            Assert.That(gallery.CurrentId, Is.Null);
        }

        [Test] public void StartingNewJourneyKeepsProfileUnlocksButNotAnotherJourneysMemory()
        {
            var first = GameSession.NewGame(data, "qa-first");
            var run = first.CaptureRun(); var meta = first.CaptureMeta(); var profile = first.CaptureProfile();
            meta.EffectiveRewinds = 3; meta.SpecialMemoryIds.Add("memory_c1");
            profile.UnlockedGalleryIds.Add("qa-scene");
            first = new GameSession(data, run, meta, profile);
            var repository = new SaveRepository(data, disk);
            repository.Persist(first);
            var second = GameSession.NewGame(data, "qa-second", repository.LoadProfile());
            repository.Save(second, "manual");
            string message;
            var loaded = new SaveRepository(data, disk).Load(null, "manual", LoadIntent.Continue, out message);
            Assert.That(loaded.Snapshot.Run.CampaignId, Is.EqualTo("qa-second"));
            Assert.That(loaded.Snapshot.Meta.EffectiveRewinds, Is.Zero);
            Assert.That(loaded.Snapshot.Meta.SpecialMemoryIds, Is.Empty);
            Assert.That(loaded.Snapshot.Profile.UnlockedGalleryIds, Does.Contain("qa-scene"));
            Assert.That(SaveCodec.DecodeMeta(disk.Read("campaigns/qa-first/meta.json")).SpecialMemoryIds,
                Does.Contain("memory_c1"), "Starting another journey must preserve the previous journey's stored metadata.");
        }

        private sealed class FailOnceStore : ISaveStore
        {
            private readonly Dictionary<string, string> files = new Dictionary<string, string>();
            public string FailNextWrite;
            public bool Exists(string name) { return files.ContainsKey(name); }
            public string Read(string name) { return files[name]; }
            public void WriteAtomic(string name, string content)
            {
                if (name == FailNextWrite) { FailNextWrite = null; throw new IOException("QA injected interrupted commit"); }
                if (files.ContainsKey(name)) files[name + ".bak"] = files[name];
                files[name] = content;
            }
        }
    }
}
