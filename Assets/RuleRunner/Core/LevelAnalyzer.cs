using System.Collections.Generic;

namespace RuleRunner.Core
{
    /// <summary>
    /// Brute-force solver used by the level checker (design doc section 6.2).
    /// Tries every ordered list of 1..slots rule cards built from the level's
    /// conditions x {IF, IF NOT} x actions (cards may repeat), and reports what wins.
    /// </summary>
    public static class LevelAnalyzer
    {
        public sealed class Report
        {
            public int SlotsTried;
            public long CombinationsTried;
            public int WinningCombinations;
            /// <summary>-1 when nothing wins.</summary>
            public int MaxQiLeft = -1;
            /// <summary>-1 when nothing wins.</summary>
            public int MinRules = -1;
            public List<Rule> BestSolution;
            public List<Rule> ShortestSolution;
            /// <summary>Up to 20 winning combinations, for the designer to read.</summary>
            public List<List<Rule>> Examples = new List<List<Rule>>();
            public RunResult PreviousSolutionResult;
            /// <summary>Count of runs per outcome over all combinations.</summary>
            public Dictionary<Outcome, long> OutcomeCounts = new Dictionary<Outcome, long>();
        }

        public static List<Rule> CardPool(LevelDef level)
        {
            var pool = new List<Rule>();
            foreach (var c in level.Conditions)
                foreach (var neg in new[] { false, true })
                    foreach (var a in level.Actions)
                        pool.Add(new Rule(c, neg, a));
            return pool;
        }

        public static Report Analyze(LevelDef level, int slots = -1, int maxExamples = 20)
        {
            if (slots < 0) slots = level.Slots;
            var report = new Report { SlotsTried = slots };
            var pool = CardPool(level);
            var current = new List<Rule>(slots);
            Recurse(level, pool, slots, current, report, maxExamples);
            report.PreviousSolutionResult = Simulator.Run(level, level.PreviousSolution, recordFrames: false);
            return report;
        }

        static void Recurse(LevelDef level, List<Rule> pool, int slots, List<Rule> current, Report report, int maxExamples)
        {
            if (current.Count > 0)
            {
                var r = Simulator.Run(level, current, recordFrames: false);
                report.CombinationsTried++;
                report.OutcomeCounts.TryGetValue(r.Outcome, out var n);
                report.OutcomeCounts[r.Outcome] = n + 1;
                if (r.Won)
                {
                    report.WinningCombinations++;
                    if (r.QiLeft > report.MaxQiLeft)
                    {
                        report.MaxQiLeft = r.QiLeft;
                        report.BestSolution = new List<Rule>(current);
                    }
                    if (report.MinRules < 0 || current.Count < report.MinRules)
                    {
                        report.MinRules = current.Count;
                        report.ShortestSolution = new List<Rule>(current);
                    }
                    if (report.Examples.Count < maxExamples) report.Examples.Add(new List<Rule>(current));
                }
            }
            if (current.Count == slots) return;
            foreach (var card in pool)
            {
                current.Add(card);
                Recurse(level, pool, slots, current, report, maxExamples);
                current.RemoveAt(current.Count - 1);
            }
        }
    }
}
