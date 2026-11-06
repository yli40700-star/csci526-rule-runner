using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>Winning-combination counts from the web prototype (design doc section 6.2).</summary>
    public class LevelAnalyzerTests
    {
        [TestCase("1-1", 2)]
        [TestCase("1-2", 10)]
        [TestCase("1-3", 1)]
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
        public void Level1_2_NeedsGround2Ahead()
        {
            var l = LevelDatabase.Get("1-2");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(Without(l, c: Condition.Ground2)).WinningCombinations);
        }

        [Test]
        public void Level1_3_NeedsTheOrb()
        {
            var l = Without(LevelDatabase.Get("1-3"));
            l.Map = l.Map.Replace('o', 'g');
            Assert.AreEqual(0, LevelAnalyzer.Analyze(l).WinningCombinations, "no deck wins without the Energy orb");
        }

        [Test]
        public void Level1_3_BigJumpEverywhereFliesOverTheOrbAndRunsOut()
        {
            var l = LevelDatabase.Get("1-3");
            var r = Simulator.Run(l, Rule.ParseList(new[] { "IF NOT ground THEN bigjump" }));
            Assert.AreEqual(Outcome.Spike, r.Outcome);
            Assert.IsTrue(r.RanOutOfQi);
        }

        [Test]
        public void AndCardsJoinThePoolOnlyWhenAllowed()
        {
            // 2-1: 5 conditions x {IF, IF NOT} x 4 actions = 40 single cards;
            // 10 pairs of different conditions x 4 NOT combinations x 4 actions = 160 AND cards.
            var level = LevelDatabase.Get("2-1");
            Assert.AreEqual(200, LevelAnalyzer.CardPool(level).Count);
            Assert.AreEqual(40, LevelAnalyzer.CardPool(level, includeAnd: false).Count);
            Assert.AreEqual(20, LevelAnalyzer.CardPool(LevelDatabase.Get("1-2")).Count, "no AND before chapter 2");
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
