using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// Chapter 4: the elevator ('E'), the gold flag ('F') and the three stars, on tiny hand-made maps first,
    /// then level 4-1. The expected paths of 4-1 come from the web prototype (docs/proto/crack-2.html).
    /// </summary>
    public class ChapterFourTests
    {
        static RunResult Run(string map, string lower, int start, int qi, params string[] rules) =>
            Simulator.Run(map, start, 1, qi, Rule.ParseList(rules), true, Vocabulary.MaxSteps, lower);

        /// <summary>The run as "t2 t3 b5": floor (t upper, b lower) and column after every step.</summary>
        static string Path(RunResult r) => string.Join(" ", r.Frames.Select(f => (f.Floor == 0 ? "t" : "b") + f.X));

        // ---------- Elevator ----------

        [Test]
        public void SteppingOnTheElevatorDownstairsLiftsTheRunnerForFree()
        {
            var r = Run("gpgpgf", "pggEgg", 0, 0);
            Assert.AreEqual("b1 b2 t3 t4 t5", Path(r));
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.IsTrue(r.Frames[2].Rode);
            Assert.AreEqual(1, r.Frames.Count(f => f.Rode));
            Assert.AreEqual(0, r.QiLeft, "the ride costs nothing");
        }

        [Test]
        public void LandingAJumpOnTheElevatorAlsoLifts()
        {
            var r = Run("gpgpgf", "pgsEgg", 0, 1, "IF spike THEN jump");
            Assert.AreEqual("b1 t3 t4 t5", Path(r));
            Assert.IsTrue(r.Frames[1].Rode);
        }

        [Test]
        public void FallingOntoTheElevatorDoesNotLift()
        {
            var r = Run("gpgg", "pEgf", 0, 0);
            Assert.AreEqual("b1 b2 b3", Path(r));
            Assert.IsTrue(r.Frames[0].Fell);
            Assert.IsFalse(r.Frames.Any(f => f.Rode));
        }

        [Test]
        public void CardsSeeTheHoleAboveAsAPitAndThePlatformAsGround()
        {
            Assert.IsTrue(Simulator.Test("gpg".ToCharArray(), Condition.Pit, 0, 1, 0), "upper tile before the ride");
            Assert.IsTrue(Simulator.Test("gEg".ToCharArray(), Condition.Ground, 0, 1, 0), "the platform downstairs");
            Assert.IsFalse(Simulator.Test("gEg".ToCharArray(), Condition.Pit, 0, 1, 0));
        }

        [Test]
        public void TheElevatorStaysUpAsABridgeAndLiftsOnlyOnce()
        {
            // Down the hole, onto the elevator, up, on to the wall, turn, back across the raised platform,
            // down the first hole again, turn at the wall downstairs, and over the tile the platform left:
            // plain ground now, no second ride.
            var r = Run("gpgpgH", "HggEgH", 0, 2, "IF wall THEN turn");
            StringAssert.StartsWith("b1 b2 t3 t4 t4 t3 t2 b1 b1 b2 b3 b4", Path(r));
            Assert.IsFalse(r.Frames[5].Fell, "the raised platform is ground");
            Assert.AreEqual(1, r.Frames.Count(f => f.Rode));
        }

        [Test]
        public void EveryElevatorSitsUnderAHoleOnTheLowerFloor()
        {
            foreach (var level in LevelDatabase.All)
            {
                Assert.IsFalse(level.Map.Contains(Vocabulary.Elevator), level.Id + ": the elevator belongs on the lower floor");
                if (!level.TwoFloors) continue;
                for (int i = 0; i < level.Lower.Length; i++)
                    if (level.Lower[i] == Vocabulary.Elevator)
                        Assert.AreEqual(Vocabulary.Pit, level.Map[i], level.Id + ": the tile above the elevator at " + i + " must be a pit");
            }
        }

        // ---------- Gold flag and stars ----------

        [Test]
        public void GoldFlagWinsAndIsReported()
        {
            var gold = Run("ggF", null, 0, 0);
            Assert.AreEqual(Outcome.Win, gold.Outcome);
            Assert.IsTrue(gold.Gold);
            Assert.IsFalse(Run("ggf", null, 0, 0).Gold);
            Assert.IsTrue(Simulator.Test("gF".ToCharArray(), Condition.Ground, 0, 1, 0), "cards see the gold flag as ground");
        }

        [Test]
        public void OnlyLevelsWithAGoldFlagHaveStars()
        {
            foreach (var level in LevelDatabase.All.Where(l => l.Playable))
            {
                Assert.AreEqual(level.Chapter == 4, level.HasStars, level.Id);
                if (!level.HasStars)
                    Assert.IsFalse(Simulator.Run(level, level.Solution).Stars(level).Any(s => s), level.Id);
            }
        }

        [Test]
        public void AFailedRunHasNoStars()
        {
            var level = LevelDatabase.Get("4-1");
            Assert.IsFalse(Simulator.Run(level, null).Stars(level).Any(s => s));
        }

        // ---------- 4-1 Build the Bridge ----------

        static RunResult Run41(params string[] rules) => Simulator.Run(LevelDatabase.Get("4-1"), Rule.ParseList(rules));

        [Test]
        public void GoldRouteBuildsTheBridge()
        {
            var level = LevelDatabase.Get("4-1");
            var r = Run41("IF pit THEN jump", "IF spike THEN turn", "IF wall THEN turn");
            // Jump into the gap, turn at the spike, step onto the elevator (the crack breaks behind), ride up,
            // turn at the left wall, walk back over the raised platform, jump the one hole left.
            Assert.AreEqual("t2 t3 b5 b5 t4 t3 t2 t1 t0 t0 t1 t2 t3 t4 t6 t7 t8 t9", Path(r));
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.IsTrue(r.Gold);
            Assert.AreEqual(1, r.QiLeft);
            Assert.AreEqual(5, r.Frames[4].Crumbled, "the cracked tile breaks when the runner steps off it onto the elevator");
            CollectionAssert.AreEqual(new[] { true, true, true }, r.Stars(level));
        }

        [Test]
        public void NormalFlagGetsTwoStars()
        {
            var level = LevelDatabase.Get("4-1");
            var r = Run41("IF spike THEN jump", "IF wall THEN jump");
            Assert.AreEqual("t2 t3 b4 b5 b7 b9", Path(r));
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.IsFalse(r.Gold);
            Assert.AreEqual(3, r.QiLeft);
            CollectionAssert.AreEqual(new[] { true, true, false }, r.Stars(level));
        }

        [Test]
        public void ComingBackOverTheBrokenCrackFails()
        {
            // Jump the spike, turn at the low wall, jump back: the cracked tile is gone.
            var r = Run41("IF pit THEN jump", "IF spike THEN jump", "IF wall THEN turn");
            Assert.AreEqual("t2 t3 b5 b7 b7 b5", Path(r));
            Assert.AreEqual(Outcome.Pit, r.Outcome);
        }

        [Test]
        public void BridgeWithoutAJumpFallsIntoTheHoleThatIsLeft()
        {
            var r = Run41("IF spike THEN turn", "IF wall THEN turn");
            Assert.AreEqual("t2 t3 b4 b5 b5 t4 t3 t2 t1 t0 t0 t1 t2 t3 t4 b5", Path(r));
            Assert.AreEqual(Outcome.Pit, r.Outcome);
        }

        [TestCase("IF pit THEN jump", "spike")]
        [TestCase("IF spike THEN turn", "timeout")]
        [TestCase("IF wall THEN turn", "spike")]
        public void OneCardNeverWins(string rule, string expected)
        {
            Assert.AreEqual(Vocabulary.ParseOutcome(expected), Run41(rule).Outcome);
        }

        [Test]
        public void OnlyTheThreeIntendedCardsReachTheGoldFlag()
        {
            // Every deck the level offers, up to its 3 slots.
            var level = LevelDatabase.Get("4-1");
            var pool = LevelAnalyzer.CardPool(level);
            var want = new[] { "IF pit THEN jump", "IF spike THEN turn", "IF wall THEN turn" };
            int found = 0;
            foreach (var a in pool)
            {
                Assert.IsFalse(Simulator.Run(level, new[] { a }, false).Gold, a.ToIdString());
                foreach (var b in pool)
                {
                    Assert.IsFalse(Simulator.Run(level, new[] { a, b }, false).Gold, a.ToIdString() + " | " + b.ToIdString());
                    foreach (var c in pool)
                    {
                        if (!Simulator.Run(level, new[] { a, b, c }, false).Gold) continue;
                        found++;
                        CollectionAssert.AreEqual(want, new[] { a.ToIdString(), b.ToIdString(), c.ToIdString() }.OrderBy(s => s).ToArray());
                    }
                }
            }
            Assert.AreEqual(6, found, "the three cards in any order");
        }
    }
}
