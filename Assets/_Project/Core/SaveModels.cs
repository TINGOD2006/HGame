using System;

namespace HGame.Core
{
    [Serializable] public class SaveEnvelope
    {
        public int Version = 1;
        public string SavedAtUtc = "";
        public RunState Run;
    }
    [Serializable] public class MetaEnvelope { public int Version = 1; public MetaState Meta; }
    [Serializable] public class LoadTransaction
    {
        public int Version = 1;
        public string Id = "";
        public RunState Target;
        public MetaState ResultMeta;
    }
    public interface ISaveStore
    {
        bool Exists(string name);
        string Read(string name);
        void WriteAtomic(string name, string content);
    }
}
