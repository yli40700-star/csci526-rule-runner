using System;
using System.Linq;
using NUnit.Framework;
using RuleRunner.Core;
using RuleRunner.Levels;

namespace RuleRunner.Tests
{
    /// <summary>
    /// Chapter 3: cracked ground ('c') and two-floor levels (LevelDef.Lower), on tiny hand-made maps first,
    /// then the chapter's levels. The chapter was designed in a web prototype and brute-forced there too.
    /// </summary>
    public class ChapterThreeTests
    {
        static RunResult Run(string map, int start, int dir, int qi, string lower, params string[] rules) =>
            Simulator.Run(map, start, dir, qi, Rule.ParseList(rules), true, Vocabulary.MaxSteps, lower);

        // ---------- Cracked ground ----------

        [Test]
        public void CrackedGroundIsGroundUntilYouLeaveIt()
        {
            var r = Run("gcgf", 0, 1, 0, null);
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(-1, r.Frames[0].Crumbled, "stepping onto it does not break it");
            Assert.AreEqual(1, r.Frames[1].Crumbled, "leaving it does");
            Assert.IsTrue(Simulator.Test("gc".ToCharArray(), Condition.Ground, 0, 1, 0), "Ground ahead sees cracked ground as ground");
        }

        [Test]
        public void TurningOnCrackedGroundDoesNotBreakIt()
        {
            var r = Run("Hcgf", 1, -1, 1, null, "IF wall THEN turn");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(ActionType.Turn, r.Frames[0].Action);
            Assert.AreEqual(-1, r.Frames[0].Crumbled);
            Assert.AreEqual(1, r.Frames[1].Crumbled);
        }

        [Test]
        public void FootprintsAreHolesOnTheWayBack()
        {
            // Out over the crack, turn at the wall, walk back into the hole it left.
            var r = Run("gcgH", 0, 1, 1, null, "IF wall THEN turn");
            Assert.AreEqual(Outcome.Pit, r.Outcome);
            Assert.AreEqual(1, r.Frames[r.Frames.Count - 1].X);
        }

        [Test]
        public void JumpingOffCrackedGroundBreaksItButJumpingOverDoesNot()
        {
            var off = Run("cgpgf", 0, 1, 1, null, "IF ground2 THEN jump");
            Assert.AreEqual(0, off.Frames[0].Crumbled);

            var over = Run("gcgf", 0, 1, 1, null, "IF ground2 THEN jump");
            Assert.AreEqual(2, over.Frames[0].X);
            Assert.AreEqual(-1, over.Frames[0].Crumbled, "a jump never touches the tile it flies over");
        }

        [Test]
        public void ABlockedMoveDoesNotBreakCrackedGround()
        {
            var r = Simulator.Run("cw", 0, 1, 0, null, true, 2);
            Assert.IsTrue(r.Frames[0].Blocked);
            Assert.AreEqual(-1, r.Frames[0].Crumbled);
        }

        // ---------- Two floors ----------

        [Test]
        public void WalkingIntoAHoleDropsToTheFloorBelow()
        {
            var r = Run("ggpg", 0, 1, 0, "gggf");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.IsTrue(r.Frames[1].Fell);
            Assert.AreEqual(0, r.Frames[1].FloorBefore);
            Assert.AreEqual(1, r.Frames[1].Floor);
            Assert.AreEqual(2, r.Frames[1].X, "same x, one floor down");
            Assert.AreEqual(1, r.Frames[2].Floor);
        }

        [Test]
        public void AJumpLandingInAHoleAlsoDrops()
        {
            var r = Run("gspgg", 0, 1, 1, "gggfg", "IF spike THEN jump");
            Assert.IsTrue(r.Frames[0].Fell);
            Assert.AreEqual(Outcome.Win, r.Outcome);
        }

        [TestCase('s', Outcome.Spike)]
        [TestCase('p', Outcome.Pit)]
        [TestCase('f', Outcome.Win)]
        [TestCase('H', Outcome.Pit)]
        public void TheFloorBelowDecidesWhatTheDropMeans(char below, Outcome expected)
        {
            var r = Run("gp", 0, 1, 0, "g" + below);
            Assert.AreEqual(expected, r.Outcome);
            Assert.IsTrue(r.Frames[0].Fell);
        }

        [Test]
        public void PitAheadStillSeesAHole()
        {
            // The jump card fires at the hole, so the runner never drops: chapter 3's main trap.
            var r = Run("gpgf", 0, 1, 1, "gggg", "IF pit THEN jump");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.IsFalse(r.Frames.Any(f => f.Fell));
        }

        [Test]
        public void ThereIsNoWayBackUp()
        {
            // On the lower floor its own pits are deadly.
            var r = Run("gpgg", 0, 1, 0, "ggpf");
            Assert.AreEqual(Outcome.Pit, r.Outcome);
            Assert.AreEqual(1, r.Frames[r.Frames.Count - 1].Floor);
        }

        [Test]
        public void LowerFloorMustBeAsLongAsTheMap()
        {
            Assert.Throws<ArgumentException>(() => Run("ggg", 0, 1, 0, "gg"));
        }

        [Test]
        public void OneRowLevelsNeverChangeFloorOrCrumble()
        {
            foreach (var level in LevelDatabase.All.Where(l => l.Playable && !l.TwoFloors && !l.Map.Contains(Vocabulary.Crumble)))
                foreach (var f in Simulator.Run(level, level.Solution).Frames)
                    Assert.IsTrue(f.Floor == 0 && f.FloorBefore == 0 && !f.Fell && f.Crumbled == -1, level.Id);
        }

        // ---------- The chapter ----------

        [Test]
        public void ChapterThreeIsTheLastChapterAndStartsWithTurnAround()
        {
            var ch3 = LevelDatabase.All.Where(l => l.Chapter == 3).Select(l => l.Id).ToArray();
            CollectionAssert.AreEqual(new[] { "3-1", "3-2", "3-3", "3-4", "3-5" }, ch3);
            Assert.AreEqual("3-5", LevelDatabase.All[LevelDatabase.All.Count - 1].Id);
            Assert.IsNull(LevelDatabase.Get("1-4"), "1-4 Turn Around moved to 3-1");
            Assert.AreEqual("Turn Around", LevelDatabase.Get("3-1").Title);
            Assert.AreEqual("Two Floors", LevelDatabase.Get("3-4").Title);
            Assert.AreEqual("Bridge for Later", LevelDatabase.Get("3-5").Title);
        }

        [Test]
        public void CrackedGroundAndTwoFloorsArriveWithTheirIntroDemos()
        {
            Assert.AreEqual("3-1", LevelDatabase.All.First(l => l.Map.Contains(Vocabulary.Crumble)).Id);
            Assert.AreEqual("3-3", LevelDatabase.All.First(l => l.TwoFloors).Id);
            var t31 = LevelDatabase.Get("3-1").Guide.NewItems.Select(d => d.Title).ToList();
            CollectionAssert.AreEqual(new[] { "NEW TILE: Cracked ground" }, t31);   // Turn gets its intro in chapter 2
            Assert.AreEqual("NEW: Two floors", LevelDatabase.Get("3-3").Guide.NewItems[0].Title);
            Assert.IsTrue(LevelDatabase.Get("3-3").Guide.NewItems[0].Lower != null, "the two-floor demo has a lower floor");
        }

        [Test]
        public void OnlyTurnAroundAndTrapdoorOpenWithAnIntroCard()
        {
            var withIntro = LevelDatabase.All.Where(l => l.Chapter == 3 && l.HasIntro).Select(l => l.Id).ToArray();
            CollectionAssert.AreEqual(new[] { "3-1", "3-3" }, withIntro);
        }

        [Test]
        public void TwoFloorLevelsAreWellFormed()
        {
            foreach (var l in LevelDatabase.All.Where(x => x.TwoFloors))
            {
                Assert.AreEqual(l.Map.Length, l.Lower.Length, l.Id + ": both floors are the same length");
                Assert.IsTrue(Vocabulary.IsStandable(l.Map[l.Start]), l.Id + ": the runner starts on the upper floor");
                Assert.IsTrue(l.Lower.Contains(Vocabulary.Finish) && !l.Map.Contains(Vocabulary.Finish), l.Id + ": the flag is downstairs");
                for (int i = 0; i < l.Map.Length; i++)
                    if (l.Map[i] == Vocabulary.Pit || l.Map[i] == Vocabulary.Crumble)
                        Assert.IsFalse(Vocabulary.IsWall(l.Lower[i]), l.Id + ": no wall under the hole at " + i);
            }
        }

        [TestCase("3-1", 2, 0)]
        [TestCase("3-2", 6, 0)]
        [TestCase("3-3", 2, 0)]    // one deck, in either order
        [TestCase("3-4", 2, 0)]    // one deck, in either order
        [TestCase("3-5", 11, 2)]   // two different ideas; see BridgeForLaterHasTwoWaysDown
        public void WinningCombinationCounts(string id, int expected, int maxQiLeft)
        {
            var report = LevelAnalyzer.Analyze(LevelDatabase.Get(id));
            Assert.AreEqual(expected, report.WinningCombinations);
            Assert.AreEqual(maxQiLeft, report.MaxQiLeft);
        }

        [TestCase("3-2")]
        [TestCase("3-4")]
        [TestCase("3-5")]
        public void NeedsAnd(string id)
        {
            Assert.AreEqual(0, LevelAnalyzer.Analyze(LevelDatabase.Get(id), includeAnd: false).WinningCombinations);
        }

        [Test]
        public void CracksAreWhatBreakThe3_1CardsIn3_2()
        {
            var l = LevelDatabase.Get("3-2");
            Assert.AreEqual(Outcome.Pit, Simulator.Run(l, l.PreviousSolution).Outcome);
            var solid = Simulator.Run(l.Map.Replace(Vocabulary.Crumble, Vocabulary.Ground), l.Start, l.Dir, l.Qi, l.PreviousSolution);
            Assert.AreEqual(Outcome.Win, solid.Outcome, "with solid ground the 3-1 cards would clear 3-2");
        }

        [TestCase("3-3")]
        [TestCase("3-4")]
        [TestCase("3-5")]
        public void TheWayDownIsTheOnlyWay(string id)
        {
            var l = LevelDatabase.Get(id);
            var r = Simulator.Run(l, l.Solution);
            Assert.AreEqual(1, r.Frames.Count(f => f.Fell), "the solution drops exactly once");
            Assert.IsTrue(r.Frames.Any(f => f.Crumbled >= 0 && f.FloorBefore == 0), "through a hole the runner made itself");
        }

        // ---------- 3-5 Bridge for Later ----------

        [Test]
        public void BridgeForLaterHasTwoWaysDown()
        {
            var l = LevelDatabase.Get("3-5");

            // 1) Big Jump over the spike AND the far crack, collect the orbs, come back over the crack you kept.
            var main = Simulator.Run(l, l.Solution);
            Assert.AreEqual(Outcome.Win, main.Outcome);
            Assert.AreEqual(ActionType.BigJump, main.Frames[2].Action);
            Assert.AreEqual(6, main.Frames[2].X, "flies over the far crack at 5");
            Assert.AreEqual(3, main.Frames.Count(f => f.CollectedOrb));
            Assert.AreEqual(1, main.Frames.Count(f => f.Fell));

            // 2) Never go right: break the first crack by walking off it to the left, then come back through it.
            //    The Big Jump card wants to fire at the hole but cannot pay, so the runner walks in.
            var alt = Rule.ParseList(new[] { "IF pit AND NOT ground2 THEN bigjump", "IF pit THEN jump", "IF NOT ground THEN turn" });
            var other = Simulator.Run(l, alt);
            Assert.AreEqual(Outcome.Win, other.Outcome);
            Assert.AreEqual(0, other.QiLeft);
            Assert.AreEqual(0, other.Frames.Count(f => f.CollectedOrb), "skips the Energy entirely");
            var drop = other.Frames.Single(f => f.Fell);
            Assert.AreEqual(0, drop.StarvedRule, "the unaffordable Big Jump is what walks it into the hole");
            Assert.AreEqual(ActionType.Walk, drop.Action);
        }

        [Test]
        public void BridgeForLaterNeedsTheCrackToSurviveTheWayOut()
        {
            var l = LevelDatabase.Get("3-5");
            var landsOnCrack = Simulator.Run(l, l.PreviousSolution);
            Assert.AreEqual(Outcome.Spike, landsOnCrack.Outcome);
            Assert.AreEqual(1, landsOnCrack.Frames[landsOnCrack.Frames.Count - 1].Floor, "falls through the far crack onto the spikes below");
        }
    }
}
