using System;
using System.Collections.Generic;

namespace RuleRunner.Core
{
    /// <summary>
    /// Runs a whole level in one go (design doc section 3.5). Do not change the step order:
    /// the level data depends on these exact semantics.
    /// Chapter 3 adds two things after a move, both no-ops on maps without them:
    /// a cracked tile ('c') the runner just left becomes a pit, and on a two-floor level a pit entered on the
    /// upper floor drops the runner onto the lower floor (same x, same direction, same step) before the cell is resolved.
    /// </summary>
    public static class Simulator
    {
        public static RunResult Run(LevelDef level, IList<Rule> rules, bool recordFrames = true) =>
            Run(level.Map, level.Start, level.Dir, level.Qi, rules, recordFrames, Vocabulary.MaxSteps, level.Lower);

        /// <param name="lower">The floor under map for two-floor levels (same length), or null.</param>
        public static RunResult Run(string map, int start, int dir, int qi, IList<Rule> rules,
            bool recordFrames = true, int maxSteps = Vocabulary.MaxSteps, string lower = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (dir != 1 && dir != -1) throw new ArgumentException("dir must be +1 or -1", nameof(dir));
            if (!string.IsNullOrEmpty(lower) && lower.Length != map.Length)
                throw new ArgumentException("the lower floor must be as long as the map", nameof(lower));
            rules = rules ?? Array.Empty<Rule>();

            // floors[0] is the map the runner starts on; floors[1] the floor under it (two-floor levels only).
            var floors = new[] { map.ToCharArray(), string.IsNullOrEmpty(lower) ? null : lower.ToCharArray() };
            int floor = 0;
            var cells = floors[0];
            var result = new RunResult { Hits = new int[rules.Count], Outcome = Outcome.Timeout };
            int x = start;
            // Index of the Wait card the runner is holding on, or -1. A Wait card is paid once and
            // keeps the runner in place, without checking cards, until the trap ahead goes down.
            int holdRule = -1;

            for (int t = 0; t < maxSteps; t++)
            {
                StepFrame frame = recordFrames
                    ? new StepFrame { T = t, XBefore = x, DirBefore = dir, QiBefore = qi, FloorBefore = floor }
                    : null;

                // 1. Pick the action: keep holding after a Wait while the trap ahead is up;
                //    otherwise the first rule whose (possibly negated) condition holds
                //    (both conditions for an AND card).
                var action = ActionType.Walk;
                bool holding = holdRule >= 0 && Cell(cells, x + dir) == Vocabulary.Trap && Vocabulary.TrapUp(t);
                if (holding)
                {
                    action = ActionType.Wait;
                    if (frame != null) frame.HoldingRule = holdRule;
                }
                else holdRule = -1;

                for (int i = 0; i < rules.Count && !holding; i++)
                {
                    var rule = rules[i];
                    if (!Holds(cells, rule, x, dir, t)) continue;

                    int cost = rule.Cost;
                    if (qi >= cost)
                    {
                        action = rule.Action;
                        qi -= cost;
                        result.Hits[i]++;
                        if (frame != null) frame.FiredRule = i;
                        if (action == ActionType.Wait) holdRule = i;
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
                bool blocked = false, orb = false, fell = false;
                int from = x, crumbled = -1;
                switch (action)
                {
                    case ActionType.Walk:
                    {
                        int nx = x + dir;
                        if (Vocabulary.IsWall(Cell(cells, nx))) blocked = true;
                        else x = nx;
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
                        else x = land;
                        break;
                    }
                    case ActionType.Turn:
                        dir = -dir;
                        break;
                    case ActionType.Wait:
                        break;
                }

                if (x != from)
                {
                    // Cracked ground falls away once the runner has left it.
                    if (cells[from] == Vocabulary.Crumble)
                    {
                        cells[from] = Vocabulary.Pit;
                        crumbled = from;
                    }
                    // A pit in the upper floor of a two-floor level is a way down, not the end.
                    if (floor == 0 && floors[1] != null && cells[x] == Vocabulary.Pit)
                    {
                        floor = 1;
                        cells = floors[1];
                        fell = true;
                    }
                    outcome = Resolve(cells, x, t, ref qi, out orb);
                }

                if (frame != null)
                {
                    frame.Action = action;
                    frame.X = x;
                    frame.Dir = dir;
                    frame.Qi = qi;
                    frame.Floor = floor;
                    frame.Fell = fell;
                    frame.Crumbled = crumbled;
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

        /// <summary>True when the card's (possibly negated) condition holds; an AND card needs both.</summary>
        public static bool Holds(char[] cells, Rule rule, int x, int dir, int t)
        {
            bool ok = Test(cells, rule.Condition, x, dir, t) != rule.Negate;
            if (ok && rule.And) ok = Test(cells, rule.Condition2, x, dir, t) != rule.Negate2;
            return ok;
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
                // Only reachable by dropping through a hole onto a wall below it: counts as a fall.
                case Vocabulary.LowWall:
                case Vocabulary.HighWall: return Outcome.Pit;
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
