using System.Collections.Generic;
using NUnit.Framework;
using RuleRunner.Analytics;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>The Beta analytics fields: where a run ended (repeat-mistake metric) and the internal-test build tag.</summary>
    public class AnalyticsTrackerTests
    {
        static void End(string levelId, string[] rules, out RunResult result, out int x, out int floor)
        {
            var level = LevelDatabase.Get(levelId);
            result = Simulator.Run(level, Rule.ParseList(rules));
            AnalyticsTracker.EndPosition(level, result, out x, out floor);
        }

        [Test]
        public void EmptyHandEndsOnTheFirstPit()
        {
            // T1: gggpggpggf, walking with no card falls into the pit at tile 3.
            End("T1", new string[0], out var result, out int x, out int floor);
            Assert.AreEqual(Outcome.Pit, result.Outcome);
            Assert.AreEqual(3, x);
            Assert.AreEqual(0, floor);
        }

        [Test]
        public void WinEndsOnTheFlag()
        {
            End("T1", new[] { "IF Pit THEN Jump" }, out var result, out int x, out _);
            Assert.AreEqual(Outcome.Win, result.Outcome);
            Assert.AreEqual(LevelDatabase.Get("T1").Map.IndexOf('f'), x);
        }

        [Test]
        public void EndPositionMatchesTheLastFrameOnEveryLevel()
        {
            foreach (var level in LevelDatabase.All)
            {
                if (!level.Playable) continue;
                var result = Simulator.Run(level, new List<Rule>());
                AnalyticsTracker.EndPosition(level, result, out int x, out int floor);
                var last = result.Frames[result.Frames.Count - 1];
                Assert.AreEqual(last.X, x, level.Id);
                Assert.AreEqual(last.Floor, floor, level.Id);
            }
        }

        [TestCase(true, "", true)]
        [TestCase(false, "http://localhost:8000/beta/", true)]
        [TestCase(false, "http://127.0.0.1:8080/index.html", true)]
        [TestCase(false, "https://yli40700-star.github.io/csci526-rule-runner/beta/?internal=1", true)]
        [TestCase(false, "https://yli40700-star.github.io/csci526-rule-runner/beta/", false)]
        [TestCase(false, "", false)]
        public void InternalRunsAreTagged(bool editor, string url, bool expected) =>
            Assert.AreEqual(expected, AnalyticsTracker.IsInternal(editor, url));
    }
}
