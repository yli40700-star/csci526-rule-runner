using System;

namespace RuleRunner.Core
{
    /// <summary>Conditions a rule card can test. All are relative to the runner's facing direction.</summary>
    public enum Condition { Ground, Pit, Spike, Wall, Ground2, Trap }

    /// <summary>Actions a rule card can trigger.</summary>
    public enum ActionType { Walk, Jump, BigJump, Turn, Wait }

    /// <summary>How a run ended. Running only appears on intermediate frames.</summary>
    public enum Outcome { Running, Win, Pit, Spike, Trap, Timeout }

    /// <summary>Map characters and the ID / display / cost tables from the design doc (section 3).</summary>
    public static class Vocabulary
    {
        public const char Ground = 'g';
        public const char Pit = 'p';
        public const char Spike = 's';
        public const char LowWall = 'w';
        public const char HighWall = 'H';
        public const char Trap = 'T';
        public const char Orb = 'o';
        public const char Finish = 'f';

        public const int OrbQi = 2;
        public const int MaxSteps = 90;

        public static bool IsStandable(char c) => c == Ground || c == Orb || c == Finish || c == Trap;
        public static bool IsWall(char c) => c == LowWall || c == HighWall;

        /// <summary>Traps are up for 2 steps, then down for 2 steps.</summary>
        public static bool TrapUp(int t) => (t % 4) < 2;

        public static int Cost(ActionType a)
        {
            switch (a)
            {
                case ActionType.Walk: return 0;
                case ActionType.Jump: return 1;
                case ActionType.BigJump: return 3;
                case ActionType.Turn: return 1;
                case ActionType.Wait: return 1;
                default: throw new ArgumentOutOfRangeException(nameof(a));
            }
        }

        public static string Id(Condition c)
        {
            switch (c)
            {
                case Condition.Ground: return "ground";
                case Condition.Pit: return "pit";
                case Condition.Spike: return "spike";
                case Condition.Wall: return "wall";
                case Condition.Ground2: return "ground2";
                case Condition.Trap: return "trap";
                default: throw new ArgumentOutOfRangeException(nameof(c));
            }
        }

        public static string Id(ActionType a)
        {
            switch (a)
            {
                case ActionType.Walk: return "walk";
                case ActionType.Jump: return "jump";
                case ActionType.BigJump: return "bigjump";
                case ActionType.Turn: return "turn";
                case ActionType.Wait: return "wait";
                default: throw new ArgumentOutOfRangeException(nameof(a));
            }
        }

        public static string Id(Outcome o)
        {
            switch (o)
            {
                case Outcome.Running: return "running";
                case Outcome.Win: return "win";
                case Outcome.Pit: return "pit";
                case Outcome.Spike: return "spike";
                case Outcome.Trap: return "trap";
                case Outcome.Timeout: return "timeout";
                default: throw new ArgumentOutOfRangeException(nameof(o));
            }
        }

        public static string Display(Condition c)
        {
            switch (c)
            {
                case Condition.Ground: return "Ground ahead";
                case Condition.Pit: return "Pit ahead";
                case Condition.Spike: return "Spike ahead";
                case Condition.Wall: return "Wall ahead";
                case Condition.Ground2: return "Ground 2 ahead";
                case Condition.Trap: return "Trap up ahead";
                default: throw new ArgumentOutOfRangeException(nameof(c));
            }
        }

        public static string Display(ActionType a)
        {
            switch (a)
            {
                case ActionType.Walk: return "Walk";
                case ActionType.Jump: return "Jump";
                case ActionType.BigJump: return "Big Jump";
                case ActionType.Turn: return "Turn";
                case ActionType.Wait: return "Wait";
                default: throw new ArgumentOutOfRangeException(nameof(a));
            }
        }

        /// <summary>Short failure text shown in the result popup.</summary>
        public static string Describe(Outcome o)
        {
            switch (o)
            {
                case Outcome.Win: return "Level clear!";
                case Outcome.Pit: return "Fell into a pit";
                case Outcome.Spike: return "Hit a spike";
                case Outcome.Trap: return "Hit a raised trap";
                case Outcome.Timeout: return "Out of time";
                default: return "";
            }
        }

        public static Condition ParseCondition(string id)
        {
            foreach (Condition c in Enum.GetValues(typeof(Condition)))
                if (Id(c) == id) return c;
            throw new FormatException("Unknown condition id: " + id);
        }

        public static ActionType ParseAction(string id)
        {
            foreach (ActionType a in Enum.GetValues(typeof(ActionType)))
                if (Id(a) == id) return a;
            throw new FormatException("Unknown action id: " + id);
        }

        public static Outcome ParseOutcome(string id)
        {
            foreach (Outcome o in Enum.GetValues(typeof(Outcome)))
                if (Id(o) == id) return o;
            throw new FormatException("Unknown outcome id: " + id);
        }
    }
}
