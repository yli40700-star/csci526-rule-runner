using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>Winning-combination counts from the web prototype (design doc section 6.2).</summary>
    public class LevelAnalyzerTests
    {
        [TestCase("1-1", 6)]  // was 12 before Turn moved later in the chapter
        [TestCase("1-2", 2)]
        [TestCase("1-5", 4)]
        [TestCase("1-6", 6)]
        public void ChapterWinningCombinationCounts(string id, int expected)
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get(id));
            Assert.AreEqual(expected, report.WinningCombinations);
        }

        static LevelDef Without(LevelDef l, Condition? c = null, ActionType? a = null) => new LevelDef
        {
            Id = l.Id, Map = l.Map, Start = l.Start, Dir = l.Dir, Qi = l.Qi, Slots = l.Slots,
            Conditions = l.Conditions.FindAll(x => x != c), Actions = l.Actions.FindAll(x => x != a),
        };

        [Test]
        public void Level1_5_NeedsGround2Ahead()
        {
            var l = LevelDatabase.Get("1-5");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(Without(l, c: Condition.Ground2)).WinningCombinations);
        }

        [Test]
        public void Level1_6_NeedsWalkCardAndOrb()
        {
            var l = LevelDatabase.Get("1-6");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(Without(l, a: ActionType.Walk)).WinningCombinations);
            var noOrb = Simulator.Run(l.Map.Replace('o', 'g'), l.Start, l.Dir, l.Qi, l.Solution);
            Assert.AreNotEqual(Outcome.Win, noOrb.Outcome, "the Energy orb must be needed");
        }

        [Test]
        public void Level1_7_NoWinWithThreeSlots()
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get("1-7"), slots: 3);
            Assert.AreEqual(0, report.WinningCombinations);
        }

        [Test] // ~5.5M combinations, about 2-3 s
        public void Level1_7_EighteenWinsWithFourSlots()
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get("1-7"), slots: 4);
            Assert.AreEqual(18, report.WinningCombinations);
        }

        [Test]
        public void AndCardsJoinThePoolOnlyWhenAllowed()
        {
            // 2-1: 5 conditions x {IF, IF NOT} x 4 actions = 40 single cards;
            // 10 pairs of different conditions x 4 NOT combinations x 4 actions = 160 AND cards.
            var level = LevelDatabase.Get("2-1");
            Assert.AreEqual(200, LevelAnalyzer.CardPool(level).Count);
            Assert.AreEqual(40, LevelAnalyzer.CardPool(level, includeAnd: false).Count);
            Assert.AreEqual(16, LevelAnalyzer.CardPool(LevelDatabase.Get("1-5")).Count, "no AND before chapter 2");
        }

        // Every condition and action except traps and Wait is offered; 2-2 and 2-3 are ~8M combinations each.
        [TestCase("2-1", 2)]
        [TestCase("2-2", 22)]
        [TestCase("2-3", 58)]
        public void Chapter2WinningCombinationCounts(string id, int expected)
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get(id));
            Assert.AreEqual(expected, report.WinningCombinations);
            Assert.AreEqual(1, report.MaxQiLeft, "1 Energy to spare, never more");
        }

        [TestCase("2-1")]
        [TestCase("2-2")]
        [TestCase("2-3")]
        public void Chapter2NeedsAnd(string id)
        {
            Assert.AreEqual(0, LevelAnalyzer.Analyze(LevelDatabase.Get(id), includeAnd: false).WinningCombinations);
        }

        [Test]
        public void Level2_1_KeepsTwoSlotsBecauseAThirdWouldReplaceAnd()
        {
            var level = LevelDatabase.Get("2-1");
            Assert.AreEqual(2, level.Slots);
            Assert.Greater(LevelAnalyzer.Analyze(level, slots: 3, includeAnd: false).WinningCombinations, 0,
                "with a third slot a Walk card stands in for AND, which is why 2-1 has two");
        }
    }
}
