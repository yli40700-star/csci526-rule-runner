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

        /// <summary>One-line guidance: intro, hints and lines shown after a failed run.</summary>
        public LevelGuide Guide = new LevelGuide();
    }

    /// <summary>
    /// Player guidance for a level, written as short one-liners (like the death messages in classic games):
    /// they sound like a remark, but each one points at what this level is about.
    /// </summary>
    public sealed class LevelGuide
    {
        /// <summary>Which idea the intro card illustrates: basics, not, lowwall, order, combo, trap, turn, walkblock.</summary>
        public string Concept = "";

        /// <summary>Hints unlocked one by one with the Hint button, vaguest first.</summary>
        public List<string> Hints = new List<string>();

        /// <summary>Lines per failure cause; one is picked per failed run, cycling.</summary>
        public Dictionary<Outcome, List<string>> DeathLines = new Dictionary<Outcome, List<string>>();

        /// <summary>Shown when a card was true but there was not enough qi.</summary>
        public string QiLine = "";

        /// <summary>Shown when the player runs exactly the previous level's solution and it fails.</summary>
        public string PreviousLine = "";

        /// <summary>Cards, tiles or rules this level introduces, each with a tiny looping demo.</summary>
        public List<DemoSpec> NewItems = new List<DemoSpec>();
    }

    /// <summary>A tiny scripted run shown on the intro card: a short map, a few cards, a few steps.</summary>
    public sealed class DemoSpec
    {
        public string Title = "";
        public string Line = "";
        public string Map = "ggg";
        public int Start;
        public int Dir = 1;
        public int Qi = 5;
        public List<Rule> Rules = new List<Rule>();
        public int Steps = 4;
    }
}
