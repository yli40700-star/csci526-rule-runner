using System.Linq;
using RuleRunner.Cards;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;

namespace RuleRunner.App
{
    /// <summary>
    /// Drives the wordless tutorial levels (chapter 0): the only guidance is a tapping finger.
    /// It first points at RUN, so every lesson starts with a run that fails. After that it walks through the
    /// level's Steps ("if", "then", "not", "order"), pointing at each card part until it matches the solution,
    /// and finally points at RUN again.
    /// </summary>
    public class Tutorial
    {
        readonly TapHint hint;
        readonly GameScreen screen;
        readonly LoadoutPanel loadout;
        LevelDef level;
        bool failedOnce;

        public Tutorial(TapHint hint, GameScreen screen, LoadoutPanel loadout)
        {
            this.hint = hint;
            this.screen = screen;
            this.loadout = loadout;
            loadout.Changed += () => { if (Active && failedOnce) PointAtNextStep(); };
        }

        bool Active => level != null && level.Tutorial;

        /// <summary>True once the player has cleared every tutorial level.</summary>
        public static bool Done(System.Func<string, bool> isCleared) =>
            Levels.LevelDatabase.All.Where(l => l.Tutorial).All(l => isCleared(l.Id));

        public void LevelLoaded(LevelDef def)
        {
            level = def;
            failedOnce = false;
            if (!Active)
            {
                hint.Hide();
                return;
            }
            // The first lesson opens with nothing but the map and RUN; the cards arrive after the first fall.
            if (level.CardsAfterFail && loadout.Rules.Count == 0) screen.SetLoadoutVisible(false);
            hint.Show(screen.RunButtonRect);
        }

        public void RunStarted()
        {
            if (Active) hint.Hide();
        }

        public void RunFinished(RunResult result)
        {
            if (!Active) return;
            if (result.Won)
            {
                hint.Hide();
                return;
            }
            failedOnce = true;
            screen.SetLoadoutVisible(true);
            loadout.EnsureOneCard();
            PointAtNextStep();
        }

        public void Stop()
        {
            level = null;
            hint.Hide();
        }

        void PointAtNextStep()
        {
            foreach (var step in level.Steps)
            {
                if (Satisfied(step)) continue;
                var target = Target(step);
                if (target != null)
                {
                    hint.Show(target);
                    return;
                }
            }
            hint.Show(screen.RunButtonRect);
        }

        bool Satisfied(string step)
        {
            var rules = loadout.Rules;
            var want = level.Solution;
            if (rules.Count == 0 || want.Count == 0) return false;
            switch (step)
            {
                case "if": return rules[0].Condition == want[0].Condition;
                case "then": return rules[0].Action == want[0].Action;
                case "not": return rules[0].Negate == want[0].Negate;
                case "order": return rules.Count == want.Count && rules.Zip(want, (a, b) => a.Equals(b)).All(x => x);
                default: return true;
            }
        }

        RectTransform Target(string step)
        {
            var card = loadout.CardAt(0);
            if (card == null) return null;
            switch (step)
            {
                case "if": return card.IfArea;
                case "then": return card.ThenArea;
                case "not": return card.NotChip;
                case "order": return card.MoveRightButton;
                default: return null;
            }
        }
    }
}
