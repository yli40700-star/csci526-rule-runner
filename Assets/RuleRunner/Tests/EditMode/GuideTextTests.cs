using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>One-line guidance: every level has it, and the right line is picked after a failure.</summary>
    public class GuideTextTests
    {
        static IEnumerable<string> PlayableLevelIds() =>
            LevelDatabase.All.Where(l => l.Playable).Select(l => l.Id);

        [TestCaseSource(nameof(PlayableLevelIds))]
        public void EveryLevelHasIntroConceptAndThreeHints(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsNotEmpty(level.Hint, "intro line");
            Assert.IsNotEmpty(level.Guide.Concept, "intro diagram");
            Assert.AreEqual(3, level.Guide.Hints.Count, "three hints, vaguest first");
            Assert.IsTrue(level.Guide.Hints.All(h => !string.IsNullOrWhiteSpace(h)));
        }

        [TestCaseSource(nameof(PlayableLevelIds))]
        public void PreviousSolutionFailureGetsALine(string id)
        {
            var level = LevelDatabase.Get(id);
            var result = Simulator.Run(level, level.PreviousSolution);
            if (result.Won) Assert.Pass("previous solution is meant to win here");
            Assert.IsNotEmpty(GuideText.DeathLine(level, level.PreviousSolution, result, 0));
        }

        [Test]
        public void PreviousSolutionLineIsUsedOnLevel1_2()
        {
            var level = LevelDatabase.Get("1-2");
            var result = Simulator.Run(level, level.PreviousSolution);
            Assert.AreEqual(level.Guide.PreviousLine, GuideText.DeathLine(level, level.PreviousSolution, result, 0));
        }

        [Test]
        public void EmptyLoadoutOnLevel1_1UsesItsOwnLine()
        {
            var level = LevelDatabase.Get("1-1");
            var none = new List<Rule>();
            Assert.AreEqual(level.Guide.PreviousLine, GuideText.DeathLine(level, none, Simulator.Run(level, none), 0));
        }

        [Test]
        public void StarvedRunUsesQiLine()
        {
            // 1-4 reference solution with one less Big Jump budget: spends all qi and starves.
            var level = LevelDatabase.Get("1-4");
            var rules = Rule.ParseList(new[] { "IF NOT ground THEN bigjump" });
            var result = Simulator.Run(level, rules);
            Assert.IsTrue(result.RanOutOfQi);
            Assert.AreEqual(level.Guide.QiLine, GuideText.DeathLine(level, rules, result, 0));
        }

        [Test]
        public void LinesRotateWithFailCount()
        {
            var level = LevelDatabase.Get("1-4");
            var rules = Rule.ParseList(new[] { "IF NOT ground THEN jump", "IF pit THEN bigjump" });
            var result = Simulator.Run(level, rules);
            Assert.AreEqual(Outcome.Spike, result.Outcome);
            var first = GuideText.DeathLine(level, rules, result, 0);
            var second = GuideText.DeathLine(level, rules, result, 1);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(first, GuideText.DeathLine(level, rules, result, 2));
        }

        [Test]
        public void WinHasNoLine()
        {
            var level = LevelDatabase.Get("1-1");
            var result = Simulator.Run(level, level.Solution);
            Assert.AreEqual("", GuideText.DeathLine(level, level.Solution, result, 0));
        }
    }
}
