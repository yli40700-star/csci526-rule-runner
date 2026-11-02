using System.Collections.Generic;
using System.Linq;
using RuleRunner.Cards;
using RuleRunner.Core;
using RuleRunner.UI;
using UnityEngine;

namespace RuleRunner.App
{
    /// <summary>
    /// Guidance in the tutorial levels (chapter 0). After a failed run, the card parts the level lists in Blink start
    /// blinking (IF, NOT, THEN, an empty slot, or the button that moves card 1); a part stops once the player uses it.
    /// A tapping finger on RUN shows when there is nothing left to change.
    /// </summary>
    public class Tutorial
    {
        readonly TapHint finger;
        readonly GameScreen screen;
        readonly LoadoutPanel loadout;
        LevelDef level;
        readonly HashSet<string> used = new HashSet<string>();
        readonly List<string> blinking = new List<string>();
        bool running;

        public Tutorial(TapHint finger, GameScreen screen, LoadoutPanel loadout)
        {
            this.finger = finger;
            this.screen = screen;
            this.loadout = loadout;
            loadout.PartUsed += OnPartUsed;
            loadout.Changed += OnChanged;
        }

        bool Active => level != null && level.Tutorial;

        /// <summary>True once the player has cleared every tutorial level.</summary>
        public static bool Done(System.Func<string, bool> isCleared) =>
            Levels.LevelDatabase.All.Where(l => l.Tutorial).All(l => isCleared(l.Id));

        /// <param name="demoPending">The level's cards are about to play once by themselves, so RUN is not pointed at yet.</param>
        public void LevelLoaded(LevelDef def, bool demoPending)
        {
            level = def;
            used.Clear();
            blinking.Clear();
            running = false;
            Blink.AllOff();
            if (Active && !demoPending) finger.Show(screen.RunButtonRect);
            else finger.Hide();
        }

        public void RunStarted()
        {
            running = true;
            finger.Hide();
            Blink.AllOff();
        }

        public void RunFinished(RunResult result)
        {
            running = false;
            if (!Active) return;
            if (result.Won)
            {
                blinking.Clear();
                Blink.AllOff();
                return;
            }
            // Parts not used yet start blinking: all at once, or (card-order lesson) the next one in the list.
            foreach (var part in level.Blink)
            {
                if (used.Contains(part) || blinking.Contains(part)) continue;
                blinking.Add(part);
                if (level.BlinkOneByOne) break;
            }
            Apply();
        }

        /// <summary>The first level's cards have played once and been taken away: the empty slot is the next thing to tap.</summary>
        public void DemoFinished()
        {
            running = false;
            if (!Active) return;
            if (level.Blink.Contains("slot") && !blinking.Contains("slot")) blinking.Add("slot");
            Apply();
        }

        public void Stop()
        {
            level = null;
            blinking.Clear();
            Blink.AllOff();
            finger.Hide();
        }

        void OnPartUsed(string part)
        {
            if (!Active) return;
            used.Add(part);
            blinking.Remove(part);
        }

        void OnChanged()
        {
            if (!Active || running) return;
            Apply();
        }

        /// <summary>Blinks the current parts; with nothing left to blink, the finger points at RUN.</summary>
        void Apply()
        {
            Blink.AllOff();
            bool any = false;
            foreach (var part in blinking)
            {
                var target = Target(part);
                if (target == null) continue;
                Blink.On(target);
                any = true;
            }
            // With nothing to blink, point at RUN - except in levels with no blinking at all (the player works it out).
            if (!any && level.Blink.Count > 0 && blinking.Count == 0) finger.Show(screen.RunButtonRect);
            else finger.Hide();
        }

        RectTransform Target(string part)
        {
            if (part == "slot") return loadout.FirstEmptySlot;
            var card = loadout.CardAt(0);
            if (card == null) return null;
            switch (part)
            {
                case "if": return card.IfArea;
                case "then": return card.ThenArea;
                case "not": return card.NotChip.gameObject.activeInHierarchy ? card.NotChip : null;
                case "move": return card.MoveRightButton;
                default: return null;
            }
        }
    }
}
