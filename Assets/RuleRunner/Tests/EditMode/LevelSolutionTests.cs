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
        [TestCase("1-1", "pit")]
        [TestCase("1-2", "spike")]
        [TestCase("1-3", "win")]
        [TestCase("1-4", "spike")]
        [TestCase("1-5", "win")]
        [TestCase("2-1", "trap")]
        [TestCase("2-2", "timeout")]
        [TestCase("2-5", "spike")]
        public void PreviousExpectedMatchesDesignDoc(string id, string expected)
        {
            Assert.AreEqual(Vocabulary.ParseOutcome(expected), LevelDatabase.Get(id).PreviousExpected);
        }

        [TestCase("1-1", 2)]
        [TestCase("1-2", 2)]
        [TestCase("1-3", 1)]
        [TestCase("1-4", 1)]
        [TestCase("1-5", 2)]
        [TestCase("2-1", 0)]
        [TestCase("2-2", 0)]
        [TestCase("2-5", 0)]
        public void SolutionQiMatchesDesignDoc(string id, int qiLeft)
        {
            Assert.AreEqual(qiLeft, LevelDatabase.Get(id).SolutionQiLeft);
        }

        [Test]
        public void Level1_4_ReversedRuleOrderHitsSpike()
        {
            var level = LevelDatabase.Get("1-4");
            var reversed = level.Solution.AsEnumerable().Reverse().ToList();
            Assert.AreEqual(Outcome.Spike, Simulator.Run(level, reversed).Outcome);
        }
    }
}
