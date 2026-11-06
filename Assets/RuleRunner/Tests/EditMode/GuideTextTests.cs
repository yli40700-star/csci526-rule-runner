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
            LevelDatabase.All.Where(l => l.Playable && !l.Tutorial).Select(l => l.Id); // tutorial levels teach without these lines

        [TestCaseSource(nameof(PlayableLevelIds))]
        public void EveryLevelHasIntroConceptAndThreeHints(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsNotEmpty(level.Hint, "intro line");
            // The concept diagram is drawn only on an intro card without new items.
            if (level.HasIntro && level.Guide.NewItems.Count == 0) Assert.IsNotEmpty(level.Guide.Concept, "intro diagram");
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
        public void PreviousSolutionLineIsUsedOnLevel1_3()
        {
            var level = LevelDatabase.Get("1-3");
            var result = Simulator.Run(level, level.PreviousSolution);
            Assert.AreEqual(level.Guide.PreviousLine, GuideText.DeathLine(level, level.PreviousSolution, result, 0));
        }

        [Test]
        public void EmptyLoadoutOnLevel1_1GetsALine()
        {
            var level = LevelDatabase.Get("1-1");
            var none = new List<Rule>();
            Assert.IsNotEmpty(GuideText.DeathLine(level, none, Simulator.Run(level, none), 0));
        }

        [Test]
        public void StarvedRunUsesQiLine()
        {
            // Big Jump over everything in 2-1: the Energy is gone before the double pit.
            var level = LevelDatabase.Get("2-1");
            var rules = Rule.ParseList(new[] { "IF NOT ground THEN bigjump" });
            var result = Simulator.Run(level, rules);
            Assert.IsTrue(result.RanOutOfQi);
            Assert.AreEqual(level.Guide.QiLine, GuideText.DeathLine(level, rules, result, 0));
        }

        [Test]
        public void LinesRotateWithFailCount()
        {
            var level = LevelDatabase.Get("1-3");
            var rules = Rule.ParseList(new[] { "IF NOT ground THEN jump" });
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
