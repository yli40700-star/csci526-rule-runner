using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RuleRunner.EditorTools
{
    /// <summary>
    /// One-click WebGL builds into docs/&lt;stage&gt;/ for GitHub Pages (main branch, /docs folder).
    /// Each stage has its own folder so earlier links are never overwritten.
    /// Command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod RuleRunner.EditorTools.BuildWebGL.BuildBeta
    ///   (optional: -buildStage alpha|beta|gold)
    /// </summary>
    public static class BuildWebGL
    {
        [MenuItem("Rule Runner/Build WebGL Beta")]
        public static void BuildBeta() => Build(StageFromArgs() ?? "beta");

        [MenuItem("Rule Runner/Build WebGL Gold")]
        public static void BuildGold() => Build("gold");

        public static void Build(string stage)
        {
            ProjectSetup.ApplyPlayerSettings();
            if (!File.Exists(ProjectSetup.ScenePath)) ProjectSetup.CreateMainScene();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string docs = Path.Combine(projectRoot, "docs");
            string output = Path.Combine(docs, stage);
            if (Directory.Exists(output)) Directory.Delete(output, true);
            Directory.CreateDirectory(output);
            // Stop GitHub Pages' Jekyll from hiding files.
            File.WriteAllText(Path.Combine(docs, ".nojekyll"), "");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { ProjectSetup.ScenePath };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            string msg = "[RuleRunner] WebGL " + stage + " build " + summary.result + " -> " + output +
                         " (" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB, " +
                         summary.totalTime.TotalSeconds.ToString("0") + " s)";
            if (summary.result == BuildResult.Succeeded) Debug.Log(msg);
            else
            {
                Debug.LogError(msg);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static string StageFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildStage");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
