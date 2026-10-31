using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>The wordless tutorial: each level teaches one thing, starts with a run that fails, and has one answer.</summary>
    public class TutorialLevelTests
    {
        static IEnumerable<string> TutorialIds() => LevelDatabase.All.Where(l => l.Tutorial).Select(l => l.Id);

        static readonly string[] KnownSteps = { "if", "then", "not", "order" };

        [Test]
        public void TutorialComesFirstAndHasFiveLessons()
        {
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3", "T4", "T5" }, TutorialIds().ToArray());
            Assert.IsTrue(LevelDatabase.All.First().Tutorial, "a new player starts in the tutorial");
            Assert.AreEqual("1-1", LevelDatabase.Next(LevelDatabase.Get("T5")).Id, "the tutorial leads into chapter 1");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void StartingCardsFail(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsFalse(Simulator.Run(level, level.StartCards).Won, "the first run of " + id + " must fail");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void OnlyTheSolutionWins(string id)
        {
            var level = LevelDatabase.Get(id);
            if (level.LockCards)
            {
                // Only the order can change: of all orders of the given cards, exactly the solution wins.
                var orders = Permutations(level.StartCards).Where(o => Simulator.Run(level, o).Won).ToList();
                Assert.AreEqual(1, orders.Count, id);
                CollectionAssert.AreEqual(level.Solution, orders[0]);
                return;
            }
            var report = LevelAnalyzer.Analyze(level);
            Assert.AreEqual(1, report.WinningCombinations, id + " should have exactly one answer");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void StepsAreKnownAndStartUnsolved(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsNotEmpty(level.Steps, id);
            Assert.IsTrue(level.Steps.All(s => KnownSteps.Contains(s)), id + ": " + string.Join(",", level.Steps));
            Assert.IsTrue(level.Solution.All(r => level.Conditions.Contains(r.Condition) && level.Actions.Contains(r.Action)),
                id + " solution uses a card the level does not offer");
            Assert.IsTrue(level.AllowNot || level.Solution.All(r => !r.Negate), id);
        }

        [Test]
        public void FirstLessonStartsWithNoCardsAndAWrongDefaultCard()
        {
            var t1 = LevelDatabase.Get("T1");
            Assert.IsTrue(t1.CardsAfterFail);
            Assert.IsEmpty(t1.StartCards);
            // The card that appears after the first fall (first condition, first action) does nothing useful.
            var defaultCard = new Rule(t1.Conditions[0], false, t1.Actions[0]);
            Assert.IsFalse(Simulator.Run(t1, new List<Rule> { defaultCard }).Won);
        }

        [Test]
        public void QiIsHiddenUntilTheQiLesson()
        {
            foreach (var l in LevelDatabase.All.Where(l => l.Tutorial))
                Assert.AreEqual(l.Id != "T5", l.HideQi, l.Id);
            var t5 = LevelDatabase.Get("T5");
            var r = Simulator.Run(t5, t5.StartCards);
            Assert.IsTrue(r.RanOutOfQi, "T5's first run fails because qi runs out");
        }

        static IEnumerable<List<Rule>> Permutations(List<Rule> items)
        {
            if (items.Count <= 1) { yield return new List<Rule>(items); yield break; }
            for (int i = 0; i < items.Count; i++)
            {
                var rest = new List<Rule>(items);
                rest.RemoveAt(i);
                foreach (var p in Permutations(rest))
                {
                    p.Insert(0, items[i]);
                    yield return p;
                }
            }
        }
    }
}
