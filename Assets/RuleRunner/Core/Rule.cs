using System;
using System.Collections.Generic;
using System.Linq;

namespace RuleRunner.Core
{
    /// <summary>One rule card: IF [NOT] condition [AND [NOT] condition] THEN action.</summary>
    [Serializable]
    public sealed class Rule : IEquatable<Rule>
    {
        public Condition Condition;
        public bool Negate;
        public ActionType Action;

        /// <summary>True for an AND card: it fires only when both conditions hold.</summary>
        public bool And;
        /// <summary>Second condition of an AND card (ignored unless <see cref="And"/>).</summary>
        public Condition Condition2;
        public bool Negate2;

        public Rule(Condition condition, bool negate, ActionType action)
        {
            Condition = condition;
            Negate = negate;
            Action = action;
        }

        /// <summary>An AND card: IF [NOT] condition AND [NOT] condition2 THEN action.</summary>
        public Rule(Condition condition, bool negate, Condition condition2, bool negate2, ActionType action)
            : this(condition, negate, action)
        {
            And = true;
            Condition2 = condition2;
            Negate2 = negate2;
        }

        /// <summary>Qi paid each time the card fires: the action's cost, plus one for an AND card.</summary>
        public int Cost => Vocabulary.Cost(Action) + (And ? Vocabulary.AndExtraQi : 0);

        public Rule Clone() =>
            And ? new Rule(Condition, Negate, Condition2, Negate2, Action) : new Rule(Condition, Negate, Action);

        /// <summary>ID form, e.g. "IF NOT ground THEN jump" or "IF pit AND NOT ground2 THEN bigjump".
        /// Used in level data and analytics.</summary>
        public string ToIdString() =>
            "IF " + Side(Negate, Vocabulary.Id(Condition)) +
            (And ? " AND " + Side(Negate2, Vocabulary.Id(Condition2)) : "") +
            " THEN " + Vocabulary.Id(Action);

        /// <summary>Player-facing form, e.g. "IF NOT Ground ahead THEN Jump".</summary>
        public string ToDisplayString() =>
            "IF " + Side(Negate, Vocabulary.Display(Condition)) +
            (And ? " AND " + Side(Negate2, Vocabulary.Display(Condition2)) : "") +
            " THEN " + Vocabulary.Display(Action);

        static string Side(bool negate, string condition) => (negate ? "NOT " : "") + condition;

        public override string ToString() => ToIdString();

        /// <summary>Parses the ID form: "IF [NOT] cond [AND [NOT] cond] THEN action" (case-insensitive keywords).</summary>
        public static Rule Parse(string text)
        {
            var parts = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int i = 0;
            if (parts.Length < 4 || !Is(parts[i++], "IF")) throw Malformed(text);
            ReadCondition(parts, ref i, text, out var negate, out var cond);

            bool and = i < parts.Length && Is(parts[i], "AND");
            bool negate2 = false;
            var cond2 = default(Condition);
            if (and)
            {
                i++;
                ReadCondition(parts, ref i, text, out negate2, out cond2);
            }

            if (i >= parts.Length || !Is(parts[i++], "THEN") || i != parts.Length - 1) throw Malformed(text);
            var action = Vocabulary.ParseAction(parts[i].ToLowerInvariant());
            return and ? new Rule(cond, negate, cond2, negate2, action) : new Rule(cond, negate, action);
        }

        static void ReadCondition(string[] parts, ref int i, string text, out bool negate, out Condition cond)
        {
            negate = i < parts.Length && Is(parts[i], "NOT");
            if (negate) i++;
            if (i >= parts.Length) throw Malformed(text);
            cond = Vocabulary.ParseCondition(parts[i++].ToLowerInvariant());
        }

        static bool Is(string word, string keyword) => word.Equals(keyword, StringComparison.OrdinalIgnoreCase);

        static FormatException Malformed(string text) =>
            new FormatException("Rule must look like 'IF [NOT] cond [AND [NOT] cond] THEN action': " + text);

        public static List<Rule> ParseList(IEnumerable<string> lines) =>
            lines == null ? new List<Rule>() : lines.Select(Parse).ToList();

        public static string JoinIds(IEnumerable<Rule> rules) => string.Join("; ", rules.Select(r => r.ToIdString()));

        public bool Equals(Rule other)
        {
            if (other == null || Action != other.Action || And != other.And) return false;
            bool sameFirst = Condition == other.Condition && Negate == other.Negate;
            if (!And) return sameFirst;
            // Both sides must hold, so A AND B is the same card as B AND A.
            return (sameFirst && Condition2 == other.Condition2 && Negate2 == other.Negate2) ||
                   (Condition == other.Condition2 && Negate == other.Negate2 &&
                    Condition2 == other.Condition && Negate2 == other.Negate);
        }

        public override bool Equals(object obj) => Equals(obj as Rule);

        public override int GetHashCode()
        {
            if (!And) return ((int)Condition * 397) ^ ((int)Action * 31) ^ (Negate ? 1 : 0);
            // Symmetric in the two sides, to match Equals.
            int a = (int)Condition * 2 + (Negate ? 1 : 0), b = (int)Condition2 * 2 + (Negate2 ? 1 : 0);
            return ((a + b) * 397) ^ (a * b * 7919) ^ ((int)Action * 31) ^ 0x5A5A;
        }
    }
}
