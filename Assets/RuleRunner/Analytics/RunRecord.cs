using System;

namespace RuleRunner.Analytics
{
    /// <summary>
    /// One row in the analytics sheet (design doc section 7). Sent when a level opens (eventType "level_start",
    /// run fields left empty) and when a run finishes (eventType "run_end").
    /// </summary>
    [Serializable]
    public class RunRecord
    {
        public const string LevelStart = "level_start";
        public const string RunEnd = "run_end";

        public string sessionId;
        /// <summary>Random id kept in browser storage, so the same person keeps it across page reloads (a new session each time).</summary>
        public string playerId;
        public string build;
        public string eventType;
        public string levelId;
        public int attempt;
        public string result;
        public int qiLeft;
        public int ruleCount;
        public string rules;
        public int steps;
        public bool ranOutOfQi;
        public int secondsSinceLevelStart;
        /// <summary>How many one-line hints the player had opened on this level before this run.</summary>
        public int hintsUsed;
        /// <summary>Tile where the run ended (the death tile on a failed run); -1 on level_start rows.</summary>
        public int endX = -1;
        /// <summary>Floor where the run ended: 0 = upper (the level's Map), 1 = lower; -1 on level_start rows.</summary>
        public int endFloor = -1;
        public string timestampUtc;
    }
}
