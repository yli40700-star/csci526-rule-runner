using System.Collections.Generic;

namespace RuleRunner.Core
{
    /// <summary>
    /// Engine-independent description of one level (design doc section 4).
    /// The Levels module fills this from JSON; tests and the simulator only use this type.
    /// </summary>
    public sealed class LevelDef
    {
        public string Id;
        public int Chapter;
        public string Title;
        public string Hint;
        public string Map;
        public int Start;
        public int Dir = 1;
        public int Qi;
        public int Slots;
        public List<Condition> Conditions = new List<Condition>();
        public List<ActionType> Actions = new List<ActionType>();

        /// <summary>False for placeholder levels that are still being designed.</summary>
        public bool Playable = true;

        /// <summary>Reference solution and the qi it should leave.</summary>
        public List<Rule> Solution = new List<Rule>();
        public int SolutionQiLeft;

        /// <summary>The previous level's solution and what it is designed to do here.</summary>
        public List<Rule> PreviousSolution = new List<Rule>();
        public Outcome PreviousExpected;
    }
}
