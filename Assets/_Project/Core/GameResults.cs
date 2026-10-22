using System;
using System.Collections.Generic;

namespace HGame.Core
{
    [Serializable] public class DomainEvent { public string Kind = "", Id = "", Text = ""; }
    [Serializable] public class CommandResult
    {
        public bool Success, Changed, RequestAutosave;
        public string Message = "";
        public List<DomainEvent> Events = new List<DomainEvent>();
        public static CommandResult Reject(string message) { return new CommandResult { Message = message }; }
        public static CommandResult Ok(string message, bool changed = true)
        { return new CommandResult { Success = true, Changed = changed, RequestAutosave = changed, Message = message }; }
    }
    [Serializable] public class LoadPlan
    {
        public bool IsRewind, CampaignSwitch;
        public string SourceHeadId = "", TargetHeadId = "";
        public List<string> DiscardedNodeIds = new List<string>();
        public List<string> ReceiptIds = new List<string>();
    }
    [Serializable] public class LoadOutcome
    {
        public RunState Run;
        public MetaState Meta;
        public bool IsRewind, AlreadyApplied;
        public string Message = "";
    }
}
