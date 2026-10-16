using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>Winning-combination counts from the web prototype (design doc section 6.2).</summary>
    public class LevelAnalyzerTests
    {
        [TestCase("1-1", 2)]
        [TestCase("1-2", 1)]
        [TestCase("1-3", 12)]
        [TestCase("1-4", 4)]
        [TestCase("1-5", 2)]
        public void Chapter1WinningCombinationCounts(string id, int expected)
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get(id));
            Assert.AreEqual(expected, report.WinningCombinations);
        }

        [Test]
        public void Level2_5_NoWinWithThreeSlots()
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get("2-5"), slots: 3);
            Assert.AreEqual(0, report.WinningCombinations);
        }

        [Test] // ~5.5M combinations, about 2-3 s
        public void Level2_5_EighteenWinsWithFourSlots()
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get("2-5"), slots: 4);
            Assert.AreEqual(18, report.WinningCombinations);
        }
    }
}
