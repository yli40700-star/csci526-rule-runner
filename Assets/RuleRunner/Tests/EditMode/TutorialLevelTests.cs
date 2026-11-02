using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// The tutorial: T1 cards, T2 NOT, T3 Big Jump (and the NOT button), T4 card order, T5 Energy.
    /// Each lesson starts with a run that fails, and the cards the player starts with cannot win by themselves.
    /// </summary>
    public class TutorialLevelTests
    {
        static IEnumerable<string> TutorialIds() => LevelDatabase.All.Where(l => l.Tutorial).Select(l => l.Id);

        static readonly string[] KnownBlinkParts = { "slot", "if", "not", "then", "move" };

        [Test]
        public void TutorialComesFirstAndHasFiveLessons()
        {
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3", "T4", "T5" }, TutorialIds().ToArray());
            Assert.IsTrue(LevelDatabase.All.First().Tutorial, "a new player starts in the tutorial");
            Assert.AreEqual("1-1", LevelDatabase.Next(LevelDatabase.Get("T5")).Id, "chapter 1 comes right after the tutorial");
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
        public void T2OffersNotGroundAsAnOptionBeforeTheNotButton()
        {
            var t2 = LevelDatabase.Get("T2");
            Assert.IsFalse(t2.AllowNot);
            CollectionAssert.AreEqual(new[] { Condition.Ground }, t2.NotOptions);
            Assert.IsTrue(LevelAnalyzer.CardPool(t2).Any(r => r.Negate && r.Condition == Condition.Ground));
            var report = LevelAnalyzer.Analyze(t2);
            Assert.AreEqual(1, report.WinningCombinations);
            CollectionAssert.AreEqual(t2.Solution, report.BestSolution);
        }

        [Test]
        public void T3NeedsNotGroundAndBigJump()
        {
            var t3 = LevelDatabase.Get("T3");
            Assert.IsTrue(t3.AllowNot);
            Assert.IsEmpty(t3.StartCards);
            var report = LevelAnalyzer.Analyze(t3);
            Assert.AreEqual(1, report.WinningCombinations);
            CollectionAssert.AreEqual(Rule.ParseList(new[] { "IF NOT ground THEN bigjump" }), report.BestSolution);
            CollectionAssert.AreEqual(new[] { "NEW ACTION: Big Jump", "NOT is now a button" }, t3.Guide.NewItems.Select(n => n.Title).ToArray());
        }

        [Test]
        public void T4NeedsBothTheSwapAndTheBigJump()
        {
            var t4 = LevelDatabase.Get("T4");
            Assert.IsTrue(t4.LockConditions);
            // IF and NOT are fixed: the player can only reorder the cards and change their THEN.
            var winners = new List<List<Rule>>();
            foreach (var order in Permutations(t4.StartCards))
                foreach (var a0 in t4.Actions)
                    foreach (var a1 in t4.Actions)
                    {
                        var cards = new List<Rule> { WithAction(order[0], a0), WithAction(order[1], a1) };
                        if (Simulator.Run(t4, cards).Won) winners.Add(cards);
                    }
            Assert.AreEqual(1, winners.Count, "exactly one way through");
            CollectionAssert.AreEqual(t4.Solution, winners[0]);

            var swapped = new List<Rule> { t4.StartCards[1], t4.StartCards[0] };
            Assert.IsFalse(Simulator.Run(t4, swapped).Won, "swapping alone is not enough");
            var bigJumpOnly = new List<Rule> { t4.StartCards[0], WithAction(t4.StartCards[1], ActionType.BigJump) };
            Assert.IsFalse(Simulator.Run(t4, bigJumpOnly).Won, "Big Jump alone is not enough: that card is never read");
        }

        [Test]
        public void EnergyIsHiddenUntilT5AndOneCardCannotDoItAll()
        {
            foreach (var l in LevelDatabase.All.Where(l => l.Tutorial))
                Assert.AreEqual(l.Id != "T5", l.HideQi, l.Id);
            var t5 = LevelDatabase.Get("T5");
            var r = Simulator.Run(t5, t5.StartCards);
            Assert.IsTrue(r.RanOutOfQi, "T5's first run fails because Energy runs out");
            Assert.AreEqual(Outcome.Pit, r.Outcome);
            var report = LevelAnalyzer.Analyze(t5);
            Assert.Greater(report.WinningCombinations, 0);
            Assert.AreEqual(2, report.MinRules, "no single card clears T5");
            Assert.IsEmpty(t5.Blink, "T5 leaves the player to work it out");
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
