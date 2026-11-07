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
        /// <summary>
        /// Two-floor levels (chapter 3): the floor under Map, same length, or null for a one-row level.
        /// The runner starts on Map (the upper floor); walking or jumping into a pit there drops it onto this floor.
        /// </summary>
        public string Lower;
        public int Start;
        public int Dir = 1;
        public int Qi;
        public int Slots;
        public bool TwoFloors => !string.IsNullOrEmpty(Lower);
        public List<Condition> Conditions = new List<Condition>();
        public List<ActionType> Actions = new List<ActionType>();
        /// <summary>False before NOT is introduced: cards cannot be negated in this level.</summary>
        public bool AllowNot = true;
        /// <summary>True when cards may test two conditions with AND (off unless the level turns it on).</summary>
        public bool AllowAnd;

        /// <summary>Tutorial levels teach one thing each; the cards ride above the runner and are read out loud, card by card.</summary>
        public bool Tutorial;
        /// <summary>Tutorial: the Energy pool stays hidden (Energy has its own tutorial level).</summary>
        public bool HideQi;
        /// <summary>Tutorial: the start cards first play the level once by themselves, then they are taken away.</summary>
        public bool DemoFirst;
        /// <summary>Tutorial: IF and NOT are fixed; only THEN and the card order can change (the card-order lesson).</summary>
        public bool LockConditions;
        /// <summary>
        /// Negated conditions offered as options of their own in the IF picker while the NOT button is not shown yet
        /// (the NOT lesson offers "NOT Ground ahead" next to Ground, Pit and Spike).
        /// </summary>
        public List<Condition> NotOptions = new List<Condition>();
        /// <summary>Tutorial: cards already in the tray when the level opens.</summary>
        public List<Rule> StartCards = new List<Rule>();
        /// <summary>
        /// Tutorial: card parts that blink after a failed run until the player uses them:
        /// "slot" (an empty slot), "if", "not", "then" (card 1), "move" (card 1's move-right button).
        /// </summary>
        public List<string> Blink = new List<string>();
        /// <summary>Tutorial: the Blink parts take turns (the next one starts once the one before is used) instead of all at once.</summary>
        public bool BlinkOneByOne;

        /// <summary>False for placeholder levels that are still being designed.</summary>
        public bool Playable = true;

        /// <summary>The level opens straight into play: no intro card and no "?" button (chapter 2 teaches only in its guide).</summary>
        public bool NoIntro;
        /// <summary>The intro card opens every time the level does, until the level is cleared (not just the first time).</summary>
        public bool IntroUntilCleared;

        /// <summary>
        /// Whether the level has an intro card: chapter levels do unless NoIntro is set; tutorial levels only
        /// when they bring something new.
        /// </summary>
        public bool HasIntro => !NoIntro && (!Tutorial || Guide.NewItems.Count > 0);

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
        /// <summary>Optional floor under Map (see LevelDef.Lower).</summary>
        public string Lower;
        public int Start;
        public int Dir = 1;
        public int Qi = 5;
        public List<Rule> Rules = new List<Rule>();
        public int Steps = 4;
        /// <summary>True when the demo is meant to end badly (e.g. showing what running out of qi does).</summary>
        public bool ExpectFailure;
    }
}
