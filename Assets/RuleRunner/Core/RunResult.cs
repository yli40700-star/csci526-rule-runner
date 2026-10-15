using System.Collections.Generic;

namespace RuleRunner.Core
{
    /// <summary>Everything that happened during one simulation step. The View plays these back.</summary>
    public sealed class StepFrame
    {
        /// <summary>Step time t (starts at 0). Traps are evaluated with this value.</summary>
        public int T;

        public int XBefore;
        public int DirBefore;
        public int QiBefore;

        public int X;
        public int Dir;
        public int Qi;

        /// <summary>The action actually performed (Walk when no rule fired).</summary>
        public ActionType Action;

        /// <summary>Index of the rule that fired, or -1.</summary>
        public int FiredRule = -1;

        /// <summary>Index of a rule whose condition held but which lacked qi, or -1.</summary>
        public int StarvedRule = -1;

        /// <summary>True when a walk or jump was stopped by a wall (runner did not move).</summary>
        public bool Blocked;

        /// <summary>True when the runner picked up a qi orb this step.</summary>
        public bool CollectedOrb;

        /// <summary>Running unless this step ended the run.</summary>
        public Outcome Outcome = Outcome.Running;
    }

    public sealed class RunResult
    {
        public Outcome Outcome;
        public int QiLeft;
        public int Steps;

        /// <summary>How many times each rule fired.</summary>
        public int[] Hits;

        /// <summary>True when at least one rule wanted to fire but there was not enough qi.</summary>
        public bool RanOutOfQi;

        /// <summary>Per-step record. Empty when the run was simulated with recordFrames = false.</summary>
        public List<StepFrame> Frames = new List<StepFrame>();

        public bool Won => Outcome == Outcome.Win;
    }
}
