using System.Collections.Generic;
using System.Linq;

namespace RuleRunner.Core
{
    /// <summary>Picks the one-line message shown after a failed run.</summary>
    public static class GuideText
    {
        static readonly Dictionary<Outcome, string[]> Fallback = new Dictionary<Outcome, string[]>
        {
            { Outcome.Pit, new[] { "The ground ended before your plan did." } },
            { Outcome.Spike, new[] { "Something sharp was waiting where you landed." } },
            { Outcome.Trap, new[] { "It was up. You were early." } },
            { Outcome.Timeout, new[] { "It's still out there, doing exactly what you told it." } },
        };

        /// <param name="failCount">How many failed runs on this level so far (0 for the first), used to rotate lines.</param>
        public static string DeathLine(LevelDef level, IList<Rule> rules, RunResult result, int failCount)
        {
            if (result.Won) return "";
            var g = level.Guide ?? new LevelGuide();

            if (!string.IsNullOrEmpty(g.PreviousLine) && SameRules(rules, level.PreviousSolution))
                return g.PreviousLine;
            if (result.RanOutOfQi && !string.IsNullOrEmpty(g.QiLine))
                return g.QiLine;

            if (g.DeathLines.TryGetValue(result.Outcome, out var lines) && lines.Count > 0)
                return lines[failCount % lines.Count];
            if (result.RanOutOfQi) return "You ran out of qi before you ran out of course.";
            return Fallback.TryGetValue(result.Outcome, out var fb) ? fb[failCount % fb.Length] : "";
        }

        public static bool SameRules(IList<Rule> a, IList<Rule> b) =>
            a != null && b != null && a.Count == b.Count && a.SequenceEqual(b);
    }
}
