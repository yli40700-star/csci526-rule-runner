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
        [TestCase("1-3", 6)]  // was 12 before Turn was moved to 2-2
        [TestCase("1-4", 4)]
        [TestCase("1-5", 2)]
        public void Chapter1WinningCombinationCounts(string id, int expected)
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get(id));
            Assert.AreEqual(expected, report.WinningCombinations);
        }

        [TestCase("2-3", 4)]
        [TestCase("2-4", 6)]
        public void Chapter2WinningCombinationCounts(string id, int expected)
        {
            Assert.AreEqual(expected, LevelAnalyzer.Analyze(LevelDatabase.Get(id)).WinningCombinations);
        }

        static LevelDef Without(LevelDef l, Condition? c = null, ActionType? a = null) => new LevelDef
        {
            Id = l.Id, Map = l.Map, Start = l.Start, Dir = l.Dir, Qi = l.Qi, Slots = l.Slots,
            Conditions = l.Conditions.FindAll(x => x != c), Actions = l.Actions.FindAll(x => x != a),
        };

        [Test]
        public void Level2_3_NeedsGround2Ahead()
        {
            var l = LevelDatabase.Get("2-3");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(Without(l, c: Condition.Ground2)).WinningCombinations);
        }

        [Test]
        public void Level2_4_NeedsWalkCardAndOrb()
        {
            var l = LevelDatabase.Get("2-4");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(Without(l, a: ActionType.Walk)).WinningCombinations);
            var noOrb = Simulator.Run(l.Map.Replace('o', 'g'), l.Start, l.Dir, l.Qi, l.Solution);
            Assert.AreNotEqual(Outcome.Win, noOrb.Outcome, "the qi orb must be needed");
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
