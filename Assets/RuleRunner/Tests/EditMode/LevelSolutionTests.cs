using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>Design doc section 6.1: every level's reference solution and design intent.</summary>
    public class LevelSolutionTests
    {
        static IEnumerable<string> PlayableLevelIds() =>
            LevelDatabase.All.Where(l => l.Playable).Select(l => l.Id);

        [TestCaseSource(nameof(PlayableLevelIds))]
        public void ReferenceSolutionWinsWithExpectedQi(string id)
        {
            var level = LevelDatabase.Get(id);
            var result = Simulator.Run(level, level.Solution);
            Assert.AreEqual(Outcome.Win, result.Outcome, "Reference solution should clear " + id);
            Assert.AreEqual(level.SolutionQiLeft, result.QiLeft, "Qi left after clearing " + id);
        }

        [TestCaseSource(nameof(PlayableLevelIds))]
        public void PreviousSolutionBehavesAsDesigned(string id)
        {
            var level = LevelDatabase.Get(id);
            var result = Simulator.Run(level, level.PreviousSolution);
            Assert.AreEqual(level.PreviousExpected, result.Outcome,
                "Previous solution [" + Rule.JoinIds(level.PreviousSolution) + "] on " + id);
        }

        // Explicit table from the design doc, so a JSON edit cannot silently change the intent.
        [TestCase("1-1", "timeout")]
        [TestCase("1-2", "pit")]
        [TestCase("1-3", "spike")]
        [TestCase("2-0", "pit")]
        [TestCase("2-1", "timeout")]
        [TestCase("2-2", "timeout")]
        [TestCase("2-3", "pit")]
        [TestCase("3-1", "timeout")]   // 2-3's cards
        [TestCase("3-2", "pit")]
        [TestCase("3-3", "timeout")]
        [TestCase("3-4", "spike")]   // 3-3's cards
        [TestCase("3-5", "spike")]   // 3-4's cards
        public void PreviousExpectedMatchesDesignDoc(string id, string expected)
        {
            Assert.AreEqual(Vocabulary.ParseOutcome(expected), LevelDatabase.Get(id).PreviousExpected);
        }

        [TestCase("1-1", 1)]
        [TestCase("1-2", 1)]
        [TestCase("1-3", 1)]
        [TestCase("2-0", 0)]
        [TestCase("2-1", 1)]   // chapter 2 leaves 1 Energy to spare
        [TestCase("2-2", 1)]
        [TestCase("2-3", 1)]
        [TestCase("3-1", 0)]
        [TestCase("3-2", 0)]
        [TestCase("3-3", 0)]
        [TestCase("3-4", 0)]
        [TestCase("3-5", 2)]
        public void SolutionQiMatchesDesignDoc(string id, int qiLeft)
        {
            Assert.AreEqual(qiLeft, LevelDatabase.Get(id).SolutionQiLeft);
        }
    }
}
