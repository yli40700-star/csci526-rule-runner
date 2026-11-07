using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// Cards only appear in the level that first needs them (in the chapters; the tutorial teaches its own subset first),
    /// and each new card's demo plays cleanly.
    /// </summary>
    public class CardIntroductionTests
    {
        static string FirstLevelWith(System.Func<LevelDef, bool> has) =>
            LevelDatabase.All.Where(l => l.Playable && !l.Tutorial).First(has).Id;

        [TestCase(Condition.Pit, "1-1")]
        [TestCase(Condition.Ground, "1-1")]
        [TestCase(Condition.Spike, "1-1")]
        [TestCase(Condition.Wall, "1-1")]
        [TestCase(Condition.Ground2, "1-2")]
        public void ConditionFirstAppears(Condition c, string level) =>
            Assert.AreEqual(level, FirstLevelWith(l => l.Conditions.Contains(c)));

        [TestCase(ActionType.Jump, "1-1")]
        [TestCase(ActionType.BigJump, "1-1")]
        [TestCase(ActionType.Turn, "2-1")]
        [TestCase(ActionType.Walk, "2-1")]
        public void ActionFirstAppears(ActionType a, string level) =>
            Assert.AreEqual(level, FirstLevelWith(l => l.Actions.Contains(a)));

        [Test]
        public void TrapsAndWaitAreNotOffered()
        {
            Assert.IsFalse(LevelDatabase.All.Any(l => l.Conditions.Contains(Condition.Trap) || l.Actions.Contains(ActionType.Wait)));
        }

        [Test]
        public void Chapter1TeachesInEveryLevelUntilItIsCleared()
        {
            var chapter = LevelDatabase.All.Where(l => l.Chapter == 1).ToList();
            CollectionAssert.AreEqual(new[] { "1-1", "1-2", "1-3" }, chapter.Select(l => l.Id).ToArray());
            Assert.IsTrue(chapter.All(l => l.HasIntro && l.IntroUntilCleared && l.Guide.NewItems.Count > 0));
        }

        [Test]
        public void NotIsTaughtInTheTutorialBeforeChapter1()
        {
            // T2 offers "NOT Ground ahead" as an option; from T3 on it is the NOT button, as in the chapters.
            Assert.AreEqual("T2", LevelDatabase.All.First(l => l.NotOptions.Count > 0 || l.AllowNot).Id);
            Assert.AreEqual("T3", LevelDatabase.All.First(l => l.AllowNot).Id);
            Assert.AreEqual("1-1", FirstLevelWith(l => l.AllowNot));
        }

        [Test]
        public void LevelWithoutNotNeverOffersANotCard()
        {
            foreach (var level in LevelDatabase.All.Where(l => !l.AllowNot))
                Assert.IsFalse(LevelAnalyzer.CardPool(level).Any(r => r.Negate && !level.NotOptions.Contains(r.Condition)), level.Id);
        }

        [Test]
        public void AndIsTaughtByTheGuideAndChapter2HasNoOtherIntros()
        {
            Assert.AreEqual("2-0", LevelDatabase.All.First(l => l.AllowAnd).Id, "the AND guide comes first");
            Assert.AreEqual("2-1", FirstLevelWith(l => l.AllowAnd));
            Assert.IsFalse(LevelDatabase.Get("2-1").HasIntro);
            Assert.IsFalse(LevelDatabase.Get("2-3").HasIntro);
            var turn = LevelDatabase.Get("2-2");
            Assert.IsTrue(turn.HasIntro && turn.IntroUntilCleared, "2-2 introduces the high wall and Turn until it is cleared");
            Assert.AreEqual(2, turn.Guide.NewItems.Count);
        }

        [Test]
        public void LevelWithoutAndNeverOffersAnAndCard()
        {
            foreach (var level in LevelDatabase.All.Where(l => !l.AllowAnd))
                Assert.IsFalse(LevelAnalyzer.CardPool(level).Any(r => r.And), level.Id);
        }

        [Test]
        public void EveryNewDemoRunsWithoutDying()
        {
            bool Offered(LevelDef level, Condition c) => level.Conditions.Contains(c) || c == Condition.Ground;
            foreach (var level in LevelDatabase.All)
                foreach (var d in level.Guide.NewItems)
                {
                    var r = Simulator.Run(d.Map, d.Start, d.Dir, d.Qi, d.Rules, true, d.Steps, d.Lower);
                    bool ok = r.Outcome == Outcome.Win || r.Outcome == Outcome.Timeout;
                    Assert.AreEqual(!d.ExpectFailure, ok, level.Id + " demo '" + d.Title + "' ends with " + r.Outcome);
                    Assert.IsTrue(d.Rules.All(rule => Offered(level, rule.Condition) && (!rule.And || Offered(level, rule.Condition2))),
                        level.Id + " demo uses a condition the level does not offer");
                    Assert.IsTrue(level.AllowAnd || d.Rules.All(rule => !rule.And), level.Id + " demo uses AND before it is introduced");
                }
        }
    }
}
