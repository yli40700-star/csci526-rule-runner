using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// The tutorial: T1 cards, T2 NOT (the NOT button from the start), T3 card order (Big Jump shows up as a ready-made card), T4 Energy. The old T3 (Big Jump) was cut and the later lessons moved up.
    /// Each lesson starts with a run that fails, and the cards the player starts with cannot win by themselves.
    /// </summary>
    public class TutorialLevelTests
    {
        // The tutorial proper is chapter 0; chapter 2's AND guide (2-0) also plays in the tutorial screen (see the 2-0 test below).
        static IEnumerable<string> TutorialIds() => LevelDatabase.All.Where(l => l.Tutorial && l.Chapter == 0).Select(l => l.Id);

        static readonly string[] KnownBlinkParts = { "slot", "if", "not", "then", "move" };

        [Test]
        public void TutorialComesFirstAndHasFourLessons()
        {
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3", "T4" }, TutorialIds().ToArray());
            Assert.IsTrue(LevelDatabase.All.First().Tutorial, "a new player starts in the tutorial");
            Assert.AreEqual("1-1", LevelDatabase.Next(LevelDatabase.Get("T4")).Id, "chapter 1 comes right after the tutorial");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void FirstRunFails(string id)
        {
            var level = LevelDatabase.Get(id);
            // T1's start cards are a demo that plays once and is then taken away: the player starts with nothing.
            var first = level.DemoFirst ? new List<Rule>() : level.StartCards;
            Assert.IsFalse(Simulator.Run(level, first).Won, "the first run of " + id + " must fail");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void SolutionUsesOnlyWhatTheLevelOffers(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsTrue(Simulator.Run(level, level.Solution).Won, id);
            Assert.IsTrue(level.Solution.All(r => level.Conditions.Contains(r.Condition) && level.Actions.Contains(r.Action)), id);
            Assert.IsTrue(level.Solution.All(r => !r.Negate || level.AllowNot || level.NotOptions.Contains(r.Condition)), id);
            Assert.IsFalse(level.Actions.Contains(ActionType.Walk), id + ": no card walks in the tutorial");
        }

        [TestCaseSource(nameof(TutorialIds))]
        public void BlinkPartsAreKnown(string id)
        {
            var level = LevelDatabase.Get(id);
            Assert.IsTrue(level.Blink.All(p => KnownBlinkParts.Contains(p)), id + ": " + string.Join(",", level.Blink));
            Assert.IsTrue(level.AllowNot || !level.Blink.Contains("not"), id + " blinks a NOT button it does not show");
        }

        [Test]
        public void T1PlaysItsCardsOnceThenTheDefaultCardIsWrong()
        {
            var t1 = LevelDatabase.Get("T1");
            Assert.IsTrue(t1.DemoFirst);
            Assert.IsTrue(Simulator.Run(t1, t1.StartCards).Won, "the demo shows a winning run");
            // The card equipped from the empty slot (first condition, first action) does nothing useful.
            var equipped = new Rule(t1.Conditions[0], false, t1.Actions[0]);
            Assert.IsFalse(Simulator.Run(t1, new List<Rule> { equipped }).Won);
            Assert.AreEqual(1, LevelAnalyzer.Analyze(t1).WinningCombinations);
        }

        [Test]
        public void T2UsesTheNotButton()
        {
            var t2 = LevelDatabase.Get("T2");
            Assert.IsTrue(t2.AllowNot, "T2 has the NOT button, as every later level does");
            Assert.IsEmpty(t2.NotOptions, "no NOT Ground ahead option that turns into a button later");
            CollectionAssert.AreEqual(new[] { "if", "not" }, t2.Blink);
            var report = LevelAnalyzer.Analyze(t2);
            Assert.AreEqual(1, report.WinningCombinations);
            CollectionAssert.AreEqual(t2.Solution, report.BestSolution);
        }

        [Test]
        public void T3OnlyNeedsTheSwap()
        {
            var t3 = LevelDatabase.Get("T3");
            Assert.IsTrue(t3.LockConditions);
            // Both cards are ready-made (the Pit card already has Big Jump): the player only changes their order.
            Assert.IsTrue(t3.StartCards.Any(r => r.Action == ActionType.BigJump), "Big Jump first shows up in T3's cards");
            Assert.AreEqual(Outcome.Pit, Simulator.Run(t3, t3.StartCards).Outcome, "the Pit card is never read, so the runner falls");
            var winners = Permutations(t3.StartCards).Where(order => Simulator.Run(t3, order).Won).ToList();
            Assert.AreEqual(1, winners.Count, "exactly one order works");
            CollectionAssert.AreEqual(t3.Solution, winners[0]);
            CollectionAssert.AreEqual(new[] { "NEW ACTION: Big Jump", "NEW RULE: card order" }, t3.Guide.NewItems.Select(n => n.Title).ToArray());
        }

        [Test]
        public void EnergyIsHiddenUntilT4AndOneCardCannotDoItAll()
        {
            foreach (var l in LevelDatabase.All.Where(l => l.Tutorial && l.Chapter == 0))
                Assert.AreEqual(l.Id != "T4", l.HideQi, l.Id);
            var t4 = LevelDatabase.Get("T4");
            var r = Simulator.Run(t4, t4.StartCards);
            Assert.IsTrue(r.RanOutOfQi, "T4's first run fails because Energy runs out");
            Assert.AreEqual(Outcome.Pit, r.Outcome);
            var report = LevelAnalyzer.Analyze(t4);
            Assert.Greater(report.WinningCombinations, 0);
            Assert.AreEqual(2, report.MinRules, "no single card clears T4");
            Assert.IsEmpty(t4.Blink, "T4 leaves the player to work it out");
        }

        [Test]
        public void AndGuideOpensChapter2()
        {
            var g = LevelDatabase.Get("2-0");
            Assert.IsTrue(g.Tutorial && g.AllowAnd, "2-0 is the AND guide, played in the tutorial screen");
            Assert.AreEqual("2-0", LevelDatabase.Next(LevelDatabase.Get("1-3")).Id, "chapter 2 starts with its guide");
            Assert.AreEqual("2-0", LevelDatabase.All.First(l => l.AllowAnd).Id, "AND is taught in the guide first");
            var start = Simulator.Run(g, g.StartCards);
            Assert.IsFalse(start.Won);
            Assert.IsTrue(start.RanOutOfQi, "the start cards Big Jump every pit and run out of Energy");
            Assert.AreEqual(0, LevelAnalyzer.Analyze(g, includeAnd: false).WinningCombinations, "no way through without AND");
            Assert.AreEqual(4, LevelAnalyzer.Analyze(g).WinningCombinations);
        }

        static Rule WithAction(Rule r, ActionType a)
        {
            var c = r.Clone();
            c.Action = a;
            return c;
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
