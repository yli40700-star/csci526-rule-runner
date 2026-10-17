using System;
using System.Collections.Generic;
using System.Linq;
using RuleRunner.Core;
using UnityEngine;

namespace RuleRunner.Levels
{
    /// <summary>
    /// Loads Resources/levels.json into Core LevelDefs.
    /// Level designers only edit the JSON; field names match design doc section 4.
    /// </summary>
    public static class LevelDatabase
    {
        public const string ResourcePath = "levels";

        [Serializable]
        class LevelFile { public LevelJson[] levels; }

        [Serializable]
        class LevelJson
        {
            public string id;
            public int chapter;
            public string title;
            public string hint;
            public bool draft;
            public string map;
            public int start;
            public int dir = 1;
            public int qi;
            public int slots;
            public string[] conditions;
            public string[] actions;
            public string[] solution;
            public int solutionQiLeft;
            public string[] previousSolution;
            public string previousExpected;

            // One-line guidance (see Core.LevelGuide).
            public string concept;
            public string[] hints;
            public string[] deathPit;
            public string[] deathSpike;
            public string[] deathTrap;
            public string[] deathTimeout;
            public string qiLine;
            public string previousLine;
            public DemoJson[] newItems;
        }

        [Serializable]
        class DemoJson
        {
            public string title;
            public string line;
            public string map;
            public int start;
            public int dir;
            public int qi;
            public string[] rules;
            public int steps;
        }

        static List<LevelDef> cache;

        public static IReadOnlyList<LevelDef> All
        {
            get
            {
                if (cache == null) cache = Load();
                return cache;
            }
        }

        public static LevelDef Get(string id) => All.FirstOrDefault(l => l.Id == id);

        /// <summary>Next level in list order that is playable, or null.</summary>
        public static LevelDef Next(LevelDef level)
        {
            var list = All;
            int i = IndexOf(level);
            for (int j = i + 1; j < list.Count; j++)
                if (list[j].Playable) return list[j];
            return null;
        }

        /// <summary>Position of the level in levels.json, or -1.</summary>
        public static int IndexOf(LevelDef level)
        {
            var list = All;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == level.Id) return i;
            return -1;
        }

        /// <summary>Forces the JSON to be read again (used by editor tools after editing).</summary>
        public static void Reload() => cache = null;

        static List<LevelDef> Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing Resources/" + ResourcePath + ".json");
            return Parse(asset.text);
        }

        public static List<LevelDef> Parse(string json)
        {
            var file = JsonUtility.FromJson<LevelFile>(json);
            return file.levels.Select(ToDef).ToList();
        }

        static LevelDef ToDef(LevelJson j) => new LevelDef
        {
            Id = j.id,
            Chapter = j.chapter,
            Title = j.title,
            Hint = j.hint,
            Playable = !j.draft,
            Map = j.map,
            Start = j.start,
            Dir = j.dir == 0 ? 1 : j.dir,
            Qi = j.qi,
            Slots = j.slots,
            Conditions = (j.conditions ?? new string[0]).Select(Vocabulary.ParseCondition).ToList(),
            Actions = (j.actions ?? new string[0]).Select(Vocabulary.ParseAction).ToList(),
            Solution = Rule.ParseList(j.solution),
            SolutionQiLeft = j.solutionQiLeft,
            PreviousSolution = Rule.ParseList(j.previousSolution),
            PreviousExpected = string.IsNullOrEmpty(j.previousExpected)
                ? Outcome.Win
                : Vocabulary.ParseOutcome(j.previousExpected),
            Guide = ToGuide(j),
        };

        static LevelGuide ToGuide(LevelJson j)
        {
            var g = new LevelGuide
            {
                Concept = j.concept ?? "",
                Hints = (j.hints ?? new string[0]).ToList(),
                QiLine = j.qiLine ?? "",
                PreviousLine = j.previousLine ?? "",
                NewItems = (j.newItems ?? new DemoJson[0]).Select(n => new DemoSpec
                {
                    Title = n.title ?? "",
                    Line = n.line ?? "",
                    Map = string.IsNullOrEmpty(n.map) ? "ggg" : n.map,
                    Start = n.start,
                    Dir = n.dir == 0 ? 1 : n.dir,
                    Qi = n.qi,
                    Rules = Rule.ParseList(n.rules),
                    Steps = n.steps <= 0 ? 4 : n.steps,
                }).ToList(),
            };
            void Add(Outcome o, string[] lines)
            {
                if (lines != null && lines.Length > 0) g.DeathLines[o] = lines.ToList();
            }
            Add(Outcome.Pit, j.deathPit);
            Add(Outcome.Spike, j.deathSpike);
            Add(Outcome.Trap, j.deathTrap);
            Add(Outcome.Timeout, j.deathTimeout);
            return g;
        }
    }
}
