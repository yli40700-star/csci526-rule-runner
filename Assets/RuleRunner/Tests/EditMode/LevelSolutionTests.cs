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

        // Chapter 4 levels are mirror levels; each one's reference solution is the only winning idea.
        [TestCase("4-1")]
        [TestCase("4-2")]
        [TestCase("4-3")]
        public void MirrorLevelsAreMirrored(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsTrue(level.Mirror, id + " should have two runners");
            Assert.AreEqual(level.Map.Length - 2, level.Start2);
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
        [TestCase("1-1", "win")]
        [TestCase("1-2", "win")]
        [TestCase("1-3", "trap")]
        [TestCase("1-4", "timeout")]
        [TestCase("1-5", "spike")]
        [TestCase("1-6", "pit")]
        [TestCase("1-7", "trap")]
        [TestCase("2-1", "pit")]
        [TestCase("2-2", "pit")]
        [TestCase("4-1", "timeout")]
        [TestCase("4-2", "pit")]
        [TestCase("4-3", "spike")]
        public void PreviousExpectedMatchesDesignDoc(string id, string expected)
        {
            Assert.AreEqual(Vocabulary.ParseOutcome(expected), LevelDatabase.Get(id).PreviousExpected);
        }

        [TestCase("1-1", 1)]
        [TestCase("1-2", 2)]
        [TestCase("1-3", 0)]
        [TestCase("1-4", 0)]
        [TestCase("1-5", 0)]
        [TestCase("1-6", 0)]
        [TestCase("1-7", 0)]
        [TestCase("2-1", 0)]
        [TestCase("2-2", 0)]
        [TestCase("4-1", 0)]
        [TestCase("4-2", 0)]
        [TestCase("4-3", 0)]
        public void SolutionQiMatchesDesignDoc(string id, int qiLeft)
        {
            Assert.AreEqual(qiLeft, LevelDatabase.Get(id).SolutionQiLeft);
        }
    }
}
