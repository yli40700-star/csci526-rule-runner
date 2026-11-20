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
        /// <summary>Floor at the start of the step: 0 = the level's Map (upper floor), 1 = Lower. Always 0 on one-row levels.</summary>
        public int FloorBefore;

        public int X;
        public int Dir;
        public int Qi;
        public int Floor;

        /// <summary>True when the runner dropped through a pit in the upper floor onto the lower floor this step.</summary>
        public bool Fell;

        /// <summary>Index of the cracked tile (on FloorBefore) that broke because the runner left it this step, or -1.</summary>
        public int Crumbled = -1;

        /// <summary>The action actually performed (Walk when no rule fired).</summary>
        public ActionType Action;

        /// <summary>Index of the rule that fired, or -1.</summary>
        public int FiredRule = -1;

        /// <summary>Index of a rule whose condition held but which lacked qi, or -1.</summary>
        public int StarvedRule = -1;

        /// <summary>Index of the Wait card still holding the runner (no cost, no cards checked), or -1.</summary>
        public int HoldingRule = -1;

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
