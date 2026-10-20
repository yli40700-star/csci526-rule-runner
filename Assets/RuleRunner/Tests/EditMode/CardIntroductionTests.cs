using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>Cards only appear in the level that first needs them, and each new card's demo plays cleanly.</summary>
    public class CardIntroductionTests
    {
        static string FirstLevelWith(System.Func<LevelDef, bool> has) =>
            LevelDatabase.All.Where(l => l.Playable).First(has).Id;

        [TestCase(Condition.Pit, "1-1")]
        [TestCase(Condition.Ground, "1-2")]
        [TestCase(Condition.Spike, "1-2")]
        [TestCase(Condition.Trap, "2-1")]
        [TestCase(Condition.Wall, "2-2")]
        [TestCase(Condition.Ground2, "2-3")]
        public void ConditionFirstAppears(Condition c, string level) =>
            Assert.AreEqual(level, FirstLevelWith(l => l.Conditions.Contains(c)));

        [TestCase(ActionType.Jump, "1-1")]
        [TestCase(ActionType.BigJump, "1-4")]
        [TestCase(ActionType.Wait, "2-1")]
        [TestCase(ActionType.Turn, "2-2")]
        [TestCase(ActionType.Walk, "2-4")]
        public void ActionFirstAppears(ActionType a, string level) =>
            Assert.AreEqual(level, FirstLevelWith(l => l.Actions.Contains(a)));

        [Test]
        public void NotFirstAppearsInTheNotLevel()
        {
            Assert.AreEqual("1-2", FirstLevelWith(l => l.AllowNot));
            Assert.AreEqual("not", LevelDatabase.All.First(l => l.Id == "1-2").Guide.Concept);
        }

        [Test]
        public void LevelWithoutNotNeverOffersANotCard()
        {
            foreach (var level in LevelDatabase.All.Where(l => !l.AllowNot))
                Assert.IsFalse(LevelAnalyzer.CardPool(level).Any(r => r.Negate), level.Id);
        }

        [Test]
        public void EveryNewDemoRunsWithoutDying()
        {
            foreach (var level in LevelDatabase.All)
                foreach (var d in level.Guide.NewItems)
                {
                    var r = Simulator.Run(d.Map, d.Start, d.Dir, d.Qi, d.Rules, true, d.Steps);
                    bool ok = r.Outcome == Outcome.Win || r.Outcome == Outcome.Timeout;
                    Assert.AreEqual(!d.ExpectFailure, ok, level.Id + " demo '" + d.Title + "' ends with " + r.Outcome);
                    Assert.IsTrue(d.Rules.All(rule => level.Conditions.Contains(rule.Condition) || rule.Condition == Condition.Ground),
                        level.Id + " demo uses a condition the level does not offer");
                }
        }
    }
}
