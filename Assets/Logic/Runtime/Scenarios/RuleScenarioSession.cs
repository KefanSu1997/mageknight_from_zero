using System;
using System.Collections.Generic;
using System.Globalization;

namespace MK.Logic.Runtime.Scenarios
{
    /// <summary>可重放的教学局状态。预期数值由验收配置独立给出，不在运行时生成通过结论。</summary>
    public abstract class RuleScenarioSession
    {
        public PlayerState Player { get; protected set; } = new();
        public int Revision { get; private set; }
        public bool LastAccepted { get; private set; } = true;
        public string Message { get; protected set; } = "选择一个行动，观察数值变化。";
        public string Rule { get; protected set; } = "";
        public List<string> Journal { get; } = new();

        public bool Apply(string action)
        {
            Revision++;
            LastAccepted = Execute(action);
            Journal.Add((LastAccepted ? "• " : "× ") + Message);
            if (Journal.Count > 5) Journal.RemoveAt(0);
            return LastAccepted;
        }

        protected abstract bool Execute(string action);
        protected bool Outcome(bool accepted, string message, string rule)
        {
            Message = message;
            Rule = rule;
            return accepted;
        }

        public virtual Dictionary<string, string> ReadState() => new()
        {
            ["revision"] = Value(Revision), ["accepted"] = LastAccepted ? "1" : "0",
            ["wounds"] = Value(Player.Wounds), ["fame"] = Value(Player.Fame),
            ["units"] = Value(Player.Units.Count), ["slots"] = Value(Player.CommandSlots)
        };

        protected static string Value(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
