using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleRunner.Core
{
    /// <summary>One rule card: IF [NOT] condition THEN action.</summary>
    [Serializable]
    public sealed class Rule : IEquatable<Rule>
    {
        public Condition Condition;
        public bool Negate;
        public ActionType Action;

        public Rule(Condition condition, bool negate, ActionType action)
        {
            Condition = condition;
            Negate = negate;
            Action = action;
        }

        public Rule Clone() => new Rule(Condition, Negate, Action);

        /// <summary>ID form, e.g. "IF NOT ground THEN jump". Used in level data and analytics.</summary>
        public string ToIdString() =>
            "IF " + (Negate ? "NOT " : "") + Vocabulary.Id(Condition) + " THEN " + Vocabulary.Id(Action);

        /// <summary>Player-facing form, e.g. "IF NOT Ground ahead THEN Jump".</summary>
        public string ToDisplayString() =>
            "IF " + (Negate ? "NOT " : "") + Vocabulary.Display(Condition) + " THEN " + Vocabulary.Display(Action);

        public override string ToString() => ToIdString();

        /// <summary>Parses the ID form: "IF [NOT] cond THEN action" (case-insensitive keywords).</summary>
        public static Rule Parse(string text)
        {
            var parts = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            if (parts.Length < 4 || !parts[i++].Equals("IF", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Rule must look like 'IF [NOT] cond THEN action': " + text);
            bool negate = false;
            if (parts[i].Equals("NOT", StringComparison.OrdinalIgnoreCase)) { negate = true; i++; }
            var cond = Vocabulary.ParseCondition(parts[i++].ToLowerInvariant());
            if (i >= parts.Length || !parts[i++].Equals("THEN", StringComparison.OrdinalIgnoreCase) || i != parts.Length - 1)
                throw new FormatException("Rule must look like 'IF [NOT] cond THEN action': " + text);
            var action = Vocabulary.ParseAction(parts[i].ToLowerInvariant());
            return new Rule(cond, negate, action);
        }

        public static List<Rule> ParseList(IEnumerable<string> lines) =>
            lines == null ? new List<Rule>() : lines.Select(Parse).ToList();

        public static string JoinIds(IEnumerable<Rule> rules) => string.Join("; ", rules.Select(r => r.ToIdString()));

        public bool Equals(Rule other) =>
            other != null && Condition == other.Condition && Negate == other.Negate && Action == other.Action;

        public override bool Equals(object obj) => Equals(obj as Rule);
        public override int GetHashCode() => ((int)Condition * 397) ^ ((int)Action * 31) ^ (Negate ? 1 : 0);
    }
}
