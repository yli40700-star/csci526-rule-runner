using System;

namespace RuleRunner.Analytics
{
    /// <summary>One row in the analytics sheet: sent once per finished run (design doc section 7).</summary>
    [Serializable]
    public class RunRecord
    {
        public string sessionId;
        public string build;
        public string levelId;
        public int attempt;
        public string result;
        public int qiLeft;
        public int ruleCount;
        public string rules;
        public int steps;
        public bool ranOutOfQi;
        public int secondsSinceLevelStart;
        public string timestampUtc;
    }
}
