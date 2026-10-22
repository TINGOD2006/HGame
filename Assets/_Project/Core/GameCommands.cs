using System;

namespace HGame.Core
{
    public enum CommandKind
    {
        SwitchJob, Wait, Rest, Work, AdultWork, Buy, UseItem, Depart, EnterNode, ReturnTown,
        ChooseSupply, Attack, Skill, Defend, EnterDeep, AcceptDeep, RefuseDeep, DeepWait, Abandon,
        AcceptRoute, DeclineRoute, RestToChapterEnd, CompleteDemo
    }
    [Serializable] public class GameCommand
    {
        public CommandKind Kind;
        public string TargetId = "", ChoiceId = "";
        public int Quantity = 1;
        public GameCommand() { }
        public GameCommand(CommandKind kind, string targetId = "", string choiceId = "", int quantity = 1)
        { Kind = kind; TargetId = targetId; ChoiceId = choiceId; Quantity = quantity; }
    }
    [Serializable] public class ActionAvailability
    {
        public string Id = "", Label = "", Group = "", Reason = "";
        public bool Enabled = true;
        public GameCommand Command;
    }
}
