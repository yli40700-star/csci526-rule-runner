using System.Diagnostics;
using System.Linq;
using System.Text;
using RuleRunner.Core;
using RuleRunner.Levels;
using UnityEditor;
using UnityEngine;

namespace RuleRunner.EditorTools
{
    /// <summary>
    /// Rule Runner > Level Checker. For level designers (design doc section 6.2):
    /// tries every rule list up to the slot limit and reports how the level can be solved.
    /// </summary>
    public class LevelCheckerWindow : EditorWindow
    {
        int levelIndex;
        int slotOverride = -1;
        string report = "Pick a level and press Check.";
        Vector2 scroll;

        [MenuItem("Rule Runner/Level Checker")]
        public static void Open() => GetWindow<LevelCheckerWindow>("Level Checker");

        [MenuItem("Rule Runner/Check All Levels (log)")]
        public static void CheckAllToConsole()
        {
            LevelDatabase.Reload();
            var sb = new StringBuilder("Rule Runner level check\n");
            foreach (var level in LevelDatabase.All.Where(l => l.Playable))
                sb.AppendLine(BuildReport(level, level.Slots)).AppendLine();
            UnityEngine.Debug.Log(sb.ToString());
        }

        void OnGUI()
        {
            var levels = LevelDatabase.All;
            var names = levels.Select(l => l.Id + "  " + l.Title + (l.Playable ? "" : "  (draft)")).ToArray();

            EditorGUILayout.LabelField("Edit Assets/RuleRunner/Levels/Resources/levels.json, then Reload.", EditorStyles.wordWrappedMiniLabel);
            levelIndex = Mathf.Clamp(EditorGUILayout.Popup("Level", levelIndex, names), 0, names.Length - 1);
            var level = levels[levelIndex];
            slotOverride = EditorGUILayout.IntField(new GUIContent("Slots (-1 = level's)"), slotOverride);

            EditorGUILayout.LabelField("Map", level.Map + "   start " + level.Start + " dir " + (level.Dir > 0 ? "+1" : "-1") + "   qi " + level.Qi);
            int slots = slotOverride < 0 ? level.Slots : slotOverride;
            long pool = level.Conditions.Count * 2L * level.Actions.Count;
            long total = 0, p = 1;
            for (int k = 1; k <= slots; k++) { p *= pool; total += p; }
            EditorGUILayout.LabelField("Combinations to try", total.ToString("N0") + (total > 20000000 ? "  (slow!)" : ""));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check")) report = BuildReport(level, slots);
                if (GUILayout.Button("Reload levels.json")) { LevelDatabase.Reload(); report = "Reloaded."; }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string BuildReport(LevelDef level, int slots)
        {
            var watch = Stopwatch.StartNew();
            var r = LevelAnalyzer.Analyze(level, slots);
            watch.Stop();

            var sb = new StringBuilder();
            sb.AppendLine("Level " + level.Id + " \"" + level.Title + "\"  map " + level.Map);
            sb.AppendLine("Slots tried: " + slots + "   combinations: " + r.CombinationsTried.ToString("N0") +
                          "   (" + watch.ElapsedMilliseconds + " ms)");
            sb.AppendLine("Winning combinations: " + r.WinningCombinations);
            sb.AppendLine("Most qi left: " + (r.MaxQiLeft < 0 ? "-" : r.MaxQiLeft.ToString()) +
                          (r.BestSolution != null ? "   e.g. " + Rule.JoinIds(r.BestSolution) : ""));
            sb.AppendLine("Fewest rules: " + (r.MinRules < 0 ? "-" : r.MinRules.ToString()) +
                          (r.ShortestSolution != null ? "   e.g. " + Rule.JoinIds(r.ShortestSolution) : ""));

            var ref_ = Simulator.Run(level, level.Solution, recordFrames: false);
            sb.AppendLine("Reference solution [" + Rule.JoinIds(level.Solution) + "]: " +
                          Vocabulary.Id(ref_.Outcome) + ", qi left " + ref_.QiLeft +
                          (ref_.Won && ref_.QiLeft == level.SolutionQiLeft ? "  OK" : "  (expected win, qi " + level.SolutionQiLeft + ")"));
            var prev = r.PreviousSolutionResult;
            sb.AppendLine("Previous level's solution [" + Rule.JoinIds(level.PreviousSolution) + "]: " +
                          Vocabulary.Id(prev.Outcome) +
                          (prev.Outcome == level.PreviousExpected ? "  OK" : "  (designed: " + Vocabulary.Id(level.PreviousExpected) + ")"));

            sb.AppendLine("Outcomes over all combinations: " +
                          string.Join(", ", r.OutcomeCounts.OrderByDescending(kv => kv.Value)
                              .Select(kv => Vocabulary.Id(kv.Key) + " " + kv.Value.ToString("N0"))));
            if (r.Examples.Count > 0)
            {
                sb.AppendLine("Winning examples:");
                foreach (var ex in r.Examples)
                    sb.AppendLine("  " + Rule.JoinIds(ex) + "   -> qi " + Simulator.Run(level, ex, false).QiLeft);
            }
            return sb.ToString();
        }
    }
}
