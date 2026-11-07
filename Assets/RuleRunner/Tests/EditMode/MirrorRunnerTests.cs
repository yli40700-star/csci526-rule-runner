using NUnit.Framework;
using RuleRunner.Core;

namespace RuleRunner.Tests
{
    /// <summary>Chapter 4 mirror runners (Simulator.RunMirror) on tiny hand-made maps.</summary>
    public class MirrorRunnerTests
    {
        // Yellow starts at 1 facing right, Pink one tile in from the right end facing left.
        static RunResult Run(string map, int qi, params string[] rules) =>
            Simulator.RunMirror(map, 1, 1, map.Length - 2, -1, qi, Rule.ParseList(rules));

        [Test]
        public void RunnersTakeTurnsYellowFirst()
        {
            var r = Run("fggggf", 1, "IF wall THEN jump");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(0, r.Frames[0].Runner);
            Assert.AreEqual(1, r.Frames[1].Runner);
            Assert.AreEqual(0, r.Frames[2].Runner);
        }

        [Test]
        public void TeammateIsAWall()
        {
            // Face to face with no cards: walking into the other runner is blocked, and the run ends as stuck.
            var r = Run("fggggf", 0);
            Assert.AreEqual(Outcome.Timeout, r.Outcome);
            Assert.IsTrue(r.Frames[r.Frames.Count - 1].Blocked);
            Assert.Less(r.Steps, Vocabulary.MaxSteps, "Stuck runners end the run early");

            // Wall ahead is true in front of the teammate, and a Jump passes over them.
            Assert.AreEqual(Outcome.Win, Run("fggggf", 1, "IF wall THEN jump").Outcome);
        }

        [Test]
        public void JumpCannotLandOnTeammate()
        {
            // Yellow at 1, Pink at 3: a Jump would land on Pink, so it is blocked (and still paid for).
            var r = Simulator.RunMirror("fgggggf", 1, 1, 3, -1, 1, Rule.ParseList(new[] { "IF ground THEN jump" }), maxSteps: 1);
            Assert.IsTrue(r.Frames[0].Blocked);
            Assert.AreEqual(1, r.Frames[0].X);
            Assert.AreEqual(0, r.QiLeft);
        }

        [Test]
        public void OnlyTheFarFlagIsTheGoal()
        {
            // The flag at 2 is not Yellow's goal (the far right one is), so stepping on it is plain ground.
            var r = Run("fgfggf", 1, "IF wall THEN jump");
            Assert.AreEqual(Outcome.Win, r.Outcome);
            Assert.AreEqual(2, r.Frames[0].X);
            Assert.IsFalse(r.Frames[0].ReachedGoal);
        }

        [Test]
        public void EnergyIsShared()
        {
            // Both runners cross the spike and one jumps over the other: 3 Jumps from one pool.
            var r = Run("fgsgggf", 2, "IF NOT ground THEN jump");
            Assert.AreNotEqual(Outcome.Win, r.Outcome);
            Assert.IsTrue(r.RanOutOfQi);
            Assert.AreEqual(Outcome.Win, Run("fgsgggf", 3, "IF NOT ground THEN jump").Outcome);
        }

        [Test]
        public void BothRunnersMustGetHome()
        {
            var r = Run("fggggf", 1, "IF wall THEN jump");
            int home = 0;
            foreach (var f in r.Frames) if (f.ReachedGoal) home++;
            Assert.AreEqual(2, home);
            Assert.IsTrue(r.Frames[r.Frames.Count - 1].ReachedGoal);
        }
    }
}
