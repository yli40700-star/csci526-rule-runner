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
        /// <summary>Runs a level; a level with a second runner (<see cref="LevelDef.Mirror"/>) runs both.</summary>
        public static RunResult Run(LevelDef level, IList<Rule> rules, bool recordFrames = true) =>
            level.Mirror
                ? RunMirror(level.Map, level.Start, level.Dir, level.Start2, level.Dir2, level.Qi, rules, recordFrames)
                : Run(level.Map, level.Start, level.Dir, level.Qi, rules, recordFrames);

        public static RunResult Run(string map, int start, int dir, int qi, IList<Rule> rules,
            bool recordFrames = true, int maxSteps = Vocabulary.MaxSteps)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (dir != 1 && dir != -1) throw new ArgumentException("dir must be +1 or -1", nameof(dir));
            rules = rules ?? Array.Empty<Rule>();

            var cells = map.ToCharArray();
            var result = new RunResult { Hits = new int[rules.Count], Outcome = Outcome.Timeout };
            int x = start;
            // Index of the Wait card the runner is holding on, or -1. A Wait card is paid once and
            // keeps the runner in place, without checking cards, until the trap ahead goes down.
            int holdRule = -1;

            for (int t = 0; t < maxSteps; t++)
            {
                var outcome = Step(cells, rules, t, ref x, ref dir, ref qi, ref holdRule, result, recordFrames, out var frame, out _);
                if (frame != null) result.Frames.Add(frame);

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

        /// <summary>
        /// Mirror runners (chapter 4): two runners on one map, with the same cards and one shared Energy pool.
        /// Each step runner 0 acts first, then runner 1. Each runner's goal is the flag at the far end of its
        /// starting direction; the other flag is plain ground to it. To each runner the other one is a low wall:
        /// Wall ahead is true, walking into it is blocked, a jump can pass over it but cannot land on it.
        /// A runner that reached its flag leaves the board. The run is won when both are home; either one
        /// falling or getting hit ends it. When nothing changes for two whole steps (and the map has no traps),
        /// the runners are stuck for good and the run ends as a timeout.
        /// Frames alternate between the runners (<see cref="StepFrame.Runner"/>).
        /// </summary>
        public static RunResult RunMirror(string map, int start, int dir, int start2, int dir2, int qi, IList<Rule> rules,
            bool recordFrames = true, int maxSteps = Vocabulary.MaxSteps)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if ((dir != 1 && dir != -1) || (dir2 != 1 && dir2 != -1)) throw new ArgumentException("dir must be +1 or -1");
            rules = rules ?? Array.Empty<Rule>();

            var cells = map.ToCharArray();
            var view = new char[cells.Length];
            var result = new RunResult { Hits = new int[rules.Count], Outcome = Outcome.Timeout };
            var xs = new[] { start, start2 };
            var dirs = new[] { dir, dir2 };
            var holds = new[] { -1, -1 };
            var goals = new[] { Goal(cells, start, dir), Goal(cells, start2, dir2) };
            var home = new bool[2];
            bool hasTraps = Array.IndexOf(cells, Vocabulary.Trap) >= 0;
            int stillSteps = 0;

            for (int t = 0; t < maxSteps; t++)
            {
                bool changed = false;
                for (int i = 0; i < 2; i++)
                {
                    if (home[i]) continue;
                    int other = 1 - i;
                    // What this runner sees: only its own flag is a finish, and the other runner is a low wall.
                    for (int j = 0; j < cells.Length; j++)
                        view[j] = cells[j] == Vocabulary.Finish && j != goals[i] ? Vocabulary.Ground : cells[j];
                    if (!home[other] && xs[other] >= 0 && xs[other] < view.Length) view[xs[other]] = Vocabulary.LowWall;

                    int x = xs[i], d = dirs[i], q = qi;
                    var outcome = Step(view, rules, t, ref xs[i], ref dirs[i], ref qi, ref holds[i], result, recordFrames,
                        out var frame, out bool orb);
                    if (orb) cells[xs[i]] = Vocabulary.Ground;
                    if (xs[i] != x || dirs[i] != d || qi != q || holds[i] >= 0) changed = true;

                    if (outcome == Outcome.Win)
                    {
                        home[i] = true;
                        if (frame != null) frame.ReachedGoal = true;
                        outcome = home[other] ? Outcome.Win : Outcome.Running;
                    }
                    if (frame != null)
                    {
                        frame.Runner = i;
                        frame.Outcome = outcome;
                        result.Frames.Add(frame);
                    }
                    if (outcome != Outcome.Running)
                    {
                        result.Outcome = outcome;
                        result.Steps = t + 1;
                        result.QiLeft = qi;
                        return result;
                    }
                }

                result.Steps = t + 1;
                stillSteps = changed || hasTraps ? 0 : stillSteps + 1;
                if (stillSteps >= 2) break;
            }

            result.QiLeft = qi;
            return result;
        }

        /// <summary>The flag farthest along the runner's starting direction (its goal in a mirror level), or -1.</summary>
        static int Goal(char[] cells, int start, int dir)
        {
            int goal = -1;
            for (int j = start + dir; j >= 0 && j < cells.Length; j += dir)
                if (cells[j] == Vocabulary.Finish) goal = j;
            return goal;
        }

        /// <summary>
        /// One step of one runner (design doc section 3.5). Do not change the order inside:
        /// the level data depends on these exact semantics.
        /// </summary>
        static Outcome Step(char[] cells, IList<Rule> rules, int t, ref int x, ref int dir, ref int qi, ref int holdRule,
            RunResult result, bool recordFrames, out StepFrame frame, out bool orb)
        {
            frame = recordFrames
                ? new StepFrame { T = t, XBefore = x, DirBefore = dir, QiBefore = qi }
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
            bool blocked = false;
            orb = false;
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
            }
            return outcome;
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
