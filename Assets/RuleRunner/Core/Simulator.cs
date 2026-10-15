using System;
using System.Collections.Generic;

namespace RuleRunner.Core
{
    /// <summary>
    /// Runs a whole level in one go (design doc section 3.5). Do not change the step order:
    /// the level data depends on these exact semantics.
    /// </summary>
    public static class Simulator
    {
        public static RunResult Run(LevelDef level, IList<Rule> rules, bool recordFrames = true) =>
            Run(level.Map, level.Start, level.Dir, level.Qi, rules, recordFrames);

        public static RunResult Run(string map, int start, int dir, int qi, IList<Rule> rules,
            bool recordFrames = true, int maxSteps = Vocabulary.MaxSteps)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (dir != 1 && dir != -1) throw new ArgumentException("dir must be +1 or -1", nameof(dir));
            rules = rules ?? Array.Empty<Rule>();

            var cells = map.ToCharArray();
            var result = new RunResult { Hits = new int[rules.Count], Outcome = Outcome.Timeout };
            int x = start;

            for (int t = 0; t < maxSteps; t++)
            {
                StepFrame frame = recordFrames
                    ? new StepFrame { T = t, XBefore = x, DirBefore = dir, QiBefore = qi }
                    : null;

                // 1. Pick the action: first rule whose (possibly negated) condition holds.
                var action = ActionType.Walk;
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    bool ok = Test(cells, rule.Condition, x, dir, t);
                    if (rule.Negate) ok = !ok;
                    if (!ok) continue;

                    int cost = Vocabulary.Cost(rule.Action);
                    if (qi >= cost)
                    {
                        action = rule.Action;
                        qi -= cost;
                        result.Hits[i]++;
                        if (frame != null) frame.FiredRule = i;
                    }
                    else
                    {
                        result.RanOutOfQi = true;
                        if (frame != null) frame.StarvedRule = i;
                    }
                    break;
                }

                // 2. Perform it.
                var outcome = Outcome.Running;
                bool blocked = false, orb = false;
                switch (action)
                {
                    case ActionType.Walk:
                    {
                        int nx = x + dir;
                        if (Vocabulary.IsWall(Cell(cells, nx))) blocked = true;
                        else { x = nx; outcome = Resolve(cells, x, t, ref qi, out orb); }
                        break;
                    }
                    case ActionType.Jump:
                    case ActionType.BigJump:
                    {
                        int span = action == ActionType.Jump ? 2 : 3;
                        bool highWallInPath = false;
                        for (int k = 1; k < span; k++)
                            if (Cell(cells, x + k * dir) == Vocabulary.HighWall) highWallInPath = true;
                        int land = x + span * dir;
                        // Qi was already paid above, even if the jump is blocked.
                        if (highWallInPath || Vocabulary.IsWall(Cell(cells, land))) blocked = true;
                        else { x = land; outcome = Resolve(cells, x, t, ref qi, out orb); }
                        break;
                    }
                    case ActionType.Turn:
                        dir = -dir;
                        break;
                    case ActionType.Wait:
                        break;
                }

                if (frame != null)
                {
                    frame.Action = action;
                    frame.X = x;
                    frame.Dir = dir;
                    frame.Qi = qi;
                    frame.Blocked = blocked;
                    frame.CollectedOrb = orb;
                    frame.Outcome = outcome;
                    result.Frames.Add(frame);
                }

                result.Steps = t + 1;
                if (outcome != Outcome.Running)
                {
                    result.Outcome = outcome;
                    break;
                }
            }

            result.QiLeft = qi;
            return result;
        }

        /// <summary>Cell at index i; everything outside the map is a high wall.</summary>
        public static char Cell(char[] cells, int i) =>
            i < 0 || i >= cells.Length ? Vocabulary.HighWall : cells[i];

        public static bool Test(char[] cells, Condition cond, int x, int dir, int t)
        {
            char ahead = Cell(cells, x + dir);
            switch (cond)
            {
                case Condition.Ground: return Vocabulary.IsStandable(ahead);
                case Condition.Pit: return ahead == Vocabulary.Pit;
                case Condition.Spike: return ahead == Vocabulary.Spike;
                case Condition.Wall: return Vocabulary.IsWall(ahead);
                case Condition.Ground2: return Vocabulary.IsStandable(Cell(cells, x + 2 * dir));
                case Condition.Trap: return ahead == Vocabulary.Trap && Vocabulary.TrapUp(t);
                default: throw new ArgumentOutOfRangeException(nameof(cond));
            }
        }

        /// <summary>What happens when the runner enters a cell at step t (before t is incremented).</summary>
        static Outcome Resolve(char[] cells, int x, int t, ref int qi, out bool collectedOrb)
        {
            collectedOrb = false;
            switch (Cell(cells, x))
            {
                case Vocabulary.Pit: return Outcome.Pit;
                case Vocabulary.Spike: return Outcome.Spike;
                case Vocabulary.Trap: return Vocabulary.TrapUp(t) ? Outcome.Trap : Outcome.Running;
                case Vocabulary.Finish: return Outcome.Win;
                case Vocabulary.Orb:
                    qi += Vocabulary.OrbQi;
                    cells[x] = Vocabulary.Ground;
                    collectedOrb = true;
                    return Outcome.Running;
                default: return Outcome.Running;
            }
        }
    }
}
