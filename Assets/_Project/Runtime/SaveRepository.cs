using System;
using System.IO;
using System.Text;
using HGame.Core;
using UnityEngine;

namespace HGame.Unity
{
    public sealed class DiskSaveStore : ISaveStore
    {
        public string RootPath { get; private set; }
        public DiskSaveStore(string rootPath) { RootPath = Path.GetFullPath(rootPath); Directory.CreateDirectory(RootPath); }
        private string Resolve(string name)
        {
            var path = Path.GetFullPath(Path.Combine(RootPath, name));
            if (!path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("存檔路徑不合法。");
            return path;
        }
        public bool Exists(string name) { return File.Exists(Resolve(name)); }
        public string Read(string name) { return File.ReadAllText(Resolve(name), Encoding.UTF8); }
        public void WriteAtomic(string name, string content)
        {
            var path = Resolve(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }

    public static class SaveCodec
    {
        public static string Encode<T>(T value) { return JsonUtility.ToJson(value, true); }
        public static SaveEnvelope DecodeRun(string json)
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            if (envelope == null || envelope.Version != 1 || envelope.Run == null || string.IsNullOrEmpty(envelope.Run.CampaignId))
                throw new InvalidDataException("存檔損壞或版本不相容。");
            return envelope;
        }
        public static MetaState DecodeMeta(string json)
        {
            var envelope = JsonUtility.FromJson<MetaEnvelope>(json);
            if (envelope == null || envelope.Version != 1 || envelope.Meta == null || string.IsNullOrEmpty(envelope.Meta.CampaignId))
                throw new InvalidDataException("記憶資料損壞或版本不相容。");
            return envelope.Meta;
        }
    }

    public sealed class SaveRepository
    {
        private readonly ISaveStore store;
        private readonly GameData data;
        public SaveRepository(GameData data, ISaveStore store) { this.data = data; this.store = store; RecoverPendingLoad(); }
        private static string MetaPath(string campaign)
        {
            foreach (var c in campaign) if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') throw new InvalidDataException("旅程 ID 不合法。");
            return "campaigns/" + campaign + "/meta.json";
        }
        private static string SlotPath(string slot)
        {
            if (slot != "manual" && slot != "auto") throw new ArgumentException("未知存檔槽。");
            return slot + ".json";
        }
        public bool HasSlot(string slot) { return store.Exists(SlotPath(slot)); }
        public ProfileState LoadProfile()
        {
            if (!store.Exists("profile.json")) return new ProfileState();
            var profile = ReadWithBackup("profile.json", x => JsonUtility.FromJson<ProfileState>(x));
            if (profile == null || profile.Version != 1) throw new InvalidDataException("設定資料版本不相容。");
            return profile;
        }
        public bool CanSave(GameSnapshot snapshot)
        {
            if (snapshot.Run.Location == GameLocation.Town) return true;
            return snapshot.Run.Location == GameLocation.Dungeon && snapshot.Run.PendingSupplyNodeId == "" &&
                (data.Node(snapshot.Run.CurrentNodeId)?.Kind == "Save" || snapshot.Run.Battle != null && snapshot.Run.Battle.IsTerminal);
        }
        public void Persist(GameSession session, bool autosave = true)
        {
            var snapshot = session.Snapshot;
            store.WriteAtomic(MetaPath(snapshot.Run.CampaignId), SaveCodec.Encode(new MetaEnvelope { Meta = snapshot.Meta }));
            store.WriteAtomic("profile.json", SaveCodec.Encode(snapshot.Profile));
            if (autosave && CanSave(snapshot)) SaveRun("auto", snapshot.Run);
        }
        public void Save(GameSession session, string slot)
        {
            if (!CanSave(session.Snapshot)) throw new InvalidOperationException("請在城鎮或存檔點存檔。");
            Persist(session, false);
            SaveRun(slot, session.CaptureRun());
        }
        private void SaveRun(string slot, RunState run)
        {
            store.WriteAtomic(SlotPath(slot), SaveCodec.Encode(new SaveEnvelope { SavedAtUtc = DateTime.UtcNow.ToString("o"), Run = run }));
        }
        public SaveEnvelope ReadSlot(string slot) { return ReadWithBackup(SlotPath(slot), SaveCodec.DecodeRun); }
        public GameSession Load(GameSession current, string slot, LoadIntent intent, out string message)
        {
            var target = ReadSlot(slot).Run;
            var meta = current != null && current.Snapshot.Run.CampaignId == target.CampaignId
                ? current.CaptureMeta() : ReadWithBackup(MetaPath(target.CampaignId), SaveCodec.DecodeMeta);
            var profile = current != null ? current.CaptureProfile() : LoadProfile();
            RunState source = target;
            if (current != null && current.Snapshot.Run.CampaignId == target.CampaignId) source = current.CaptureRun();
            else if (intent == LoadIntent.ManualLoad && HasSlot("auto"))
            {
                var resume = ReadSlot("auto").Run;
                if (resume.CampaignId == target.CampaignId) source = resume;
            }
            if (current == null && intent == LoadIntent.ManualLoad && !string.IsNullOrEmpty(meta.LastHeadId))
            { source = StateCloner.Clone(source); source.TimelineHeadId = meta.LastHeadId; }
            var plan = RewindRules.PlanLoad(source, meta, target, intent);
            var transactionId = Guid.NewGuid().ToString("N");
            var outcome = RewindRules.ApplyLoad(plan, target, meta, transactionId);
            if (!outcome.AlreadyApplied)
                foreach (var experience in source.Experiences)
                    if (plan.DiscardedNodeIds.Contains(experience.TimelineNodeId) && !outcome.Meta.Echoes.Exists(x => x.Id == experience.Id))
                        outcome.Meta.Echoes.Add(StateCloner.Clone(experience));
            if (outcome.Run.Chapter == 2 && outcome.Meta.SpecialMemoryIds.Contains("memory_c2")) outcome.Run.DemoSegmentVisited = true;
            var transaction = new LoadTransaction { Id = transactionId, Target = outcome.Run, ResultMeta = outcome.Meta };
            store.WriteAtomic("pending-load.json", SaveCodec.Encode(transaction));
            CompleteTransaction(transaction);
            var session = new GameSession(data, outcome.Run, outcome.Meta, profile);
            Persist(session);
            message = outcome.Message;
            return session;
        }
        private void RecoverPendingLoad()
        {
            if (!store.Exists("pending-load.json")) return;
            var tx = JsonUtility.FromJson<LoadTransaction>(store.Read("pending-load.json"));
            // JsonUtility materializes null inline classes as empty objects after a restart.
            // Only a transaction with a durable ID is pending; an empty marker is complete.
            if (tx != null && !string.IsNullOrEmpty(tx.Id))
            {
                if (tx.Version != 1) throw new InvalidDataException("讀檔交易版本不相容。");
                if (tx.Target == null || tx.ResultMeta == null || string.IsNullOrEmpty(tx.Target.CampaignId) ||
                    tx.Target.CampaignId != tx.ResultMeta.CampaignId)
                    throw new InvalidDataException("讀檔交易資料損壞；原檔保留。");
                CompleteTransaction(tx);
            }
        }
        private void CompleteTransaction(LoadTransaction tx)
        {
            store.WriteAtomic(MetaPath(tx.Target.CampaignId), SaveCodec.Encode(new MetaEnvelope { Meta = tx.ResultMeta }));
            SaveRun("auto", tx.Target);
            store.WriteAtomic("pending-load.json", SaveCodec.Encode(new LoadTransaction()));
        }
        private T ReadWithBackup<T>(string path, Func<string, T> decode)
        {
            try { return decode(store.Read(path)); }
            catch (Exception original)
            {
                if (store.Exists(path + ".bak"))
                {
                    try { return decode(store.Read(path + ".bak")); }
                    catch { }
                }
                throw new InvalidDataException("無法讀取 " + path + "；原檔保留。", original);
            }
        }
    }
}
