using NUnit.Framework;
using RuleRunner.Core;

namespace RuleRunner.Tests
{
    /// <summary>The "easy to get wrong" details from design doc section 3.5, on tiny hand-made maps.</summary>
    public class SimulatorRuleTests
    {
        static Rule R(string text) => Rule.Parse(text);

        static RunResult Run(string map, int qi, params string[] rules) =>
            Simulator.Run(map, 0, 1, qi, Rule.ParseList(rules));

        [Test]
        public void NoRulesWalksToFinish()
        {
            var r = Run("gggf", 0);
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(3, r.Steps);
        }

        [Test]
        public void JumpOverSpikeOnlyLandingMatters()
        {
            // Jump from 0 passes over the spike at 1 and lands on 2.
            Assert.AreEqual(Outcome.Win, Run("gsgf", 1, "IF spike THEN jump").Outcome);
        }

        [Test]
        public void BlockedJumpStillCostsQi()
        {
            // Landing cell is a low wall: blocked, qi spent, runner stays.
            var r = Simulator.Run("ggw", 0, 1, 1, new[] { R("IF ground THEN jump") }, maxSteps: 1);
            Assert.AreEqual(0, r.QiLeft);
            Assert.AreEqual(0, r.Frames[0].X);
            Assert.IsTrue(r.Frames[0].Blocked);
        }

        [Test]
        public void JumpCannotPassHighWallButCanPassLowWall()
        {
            Assert.AreEqual(Outcome.Win, Run("gwgf", 1, "IF wall THEN jump").Outcome);
            var r = Simulator.Run("gHgf", 0, 1, 1, new[] { R("IF wall THEN jump") }, maxSteps: 1);
            Assert.IsTrue(r.Frames[0].Blocked);
        }

        [Test]
        public void WalkingIntoWallStaysPutUntilTimeout()
        {
            var r = Run("ggw", 0);
            Assert.AreEqual(Outcome.Timeout, r.Outcome);
            Assert.AreEqual(Vocabulary.MaxSteps, r.Steps);
        }

        [Test]
        public void OutsideMapIsHighWall()
        {
            var r = Simulator.Run("gg", 1, 1, 0, null, maxSteps: 3);
            Assert.AreEqual(1, r.Frames[2].X);
            Assert.AreEqual(Outcome.Timeout, r.Outcome);
        }

        [Test]
        public void StandingOnTrapIsSafeEnteringRaisedTrapKills()
        {
            // t=0: walk 0->1 (T at 1, trapUp(0) true) -> dies.
            Assert.AreEqual(Outcome.Trap, Run("gTgf", 0).Outcome);
            // Start ON the trap at t=0: trap is up but we did not enter it.
            var r = Simulator.Run("gTgf", 1, 1, 0, null);
            Assert.AreEqual(Outcome.Win, r.Outcome);
        }

        [Test]
        public void TrapUsesStepTimeBeforeIncrement()
        {
            // Walk 0->1 at t=0, 1->2 at t=1 (trap up -> die).
            Assert.AreEqual(Outcome.Trap, Run("ggTf", 0).Outcome);
            // Walk 0->1, 1->2, 2->3 at t=2 (trap down -> safe).
            Assert.AreEqual(Outcome.Win, Run("gggTf", 0).Outcome);
        }

        [Test]
        public void OrbAddsQiCanExceedStartAndBecomesGround()
        {
            var r = Simulator.Run("gogf", 0, 1, 1, null);
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(3, r.QiLeft);
            Assert.IsTrue(r.Frames[0].CollectedOrb);
        }

        [Test]
        public void NotEnoughQiFallsBackToWalkAndStopsChecking()
        {
            // Rule 1 holds but costs 1 with 0 qi: walk, and rule 2 is not consulted.
            var r = Run("gpgf", 0, "IF pit THEN jump", "IF NOT ground THEN wait");
            Assert.AreEqual(Outcome.Pit, r.Outcome);
            Assert.IsTrue(r.RanOutOfQi);
            Assert.AreEqual(0, r.Frames[0].StarvedRule);
            Assert.AreEqual(-1, r.Frames[0].FiredRule);
        }

        [Test]
        public void OnlyFirstTrueRuleFiresAndHitsAreCounted()
        {
            var r = Run("gpgpgf", 5, "IF pit THEN jump", "IF NOT ground THEN bigjump");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            CollectionAssert.AreEqual(new[] { 2, 0 }, r.Hits);
            Assert.AreEqual(3, r.QiLeft);
        }

        [Test]
        public void TurnFlipsDirectionWithoutMoving()
        {
            var r = Simulator.Run("fggH", 1, 1, 1, new[] { R("IF wall THEN turn") });
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(2, r.Frames[1].X);
            Assert.AreEqual(-1, r.Frames[1].Dir);
        }

        [Test]
        public void NegationAndGround2()
        {
            Assert.IsTrue(Simulator.Test("gpg".ToCharArray(), Condition.Ground2, 0, 1, 0));
            Assert.IsFalse(Simulator.Test("gpp".ToCharArray(), Condition.Ground2, 0, 1, 0));
            Assert.IsTrue(Simulator.Test("gp".ToCharArray(), Condition.Wall, 1, 1, 0), "edge of map is a wall");
        }

        [Test]
        public void RuleParseRoundTrip()
        {
            foreach (var text in new[] { "IF pit THEN jump", "IF NOT ground2 THEN bigjump", "IF trap THEN wait" })
                Assert.AreEqual(text, R(text).ToIdString());
            Assert.AreEqual("IF NOT Ground ahead THEN Jump", R("IF NOT ground THEN jump").ToDisplayString());
        }
    }
}
