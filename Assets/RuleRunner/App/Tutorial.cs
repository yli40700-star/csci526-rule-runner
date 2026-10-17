using System;
using RuleRunner.Cards;
using RuleRunner.Core;
using RuleRunner.UI;
using RuleRunner.View;
using UnityEngine;

namespace RuleRunner.App
{
    /// <summary>
    /// Hands-on tutorial for level 1-1, shown once: point at the runner, run with no cards, see it fail,
    /// equip the card, run again. Each step spotlights the one thing to click; Skip ends it.
    /// </summary>
    public class Tutorial
    {
        public const string DoneKey = "rr_tutorial_done";

        enum Step { None, Runner, FirstRun, Edit, Equip, Explain, SecondRun }

        readonly CoachOverlay overlay;
        readonly GameScreen screen;
        readonly LoadoutPanel loadout;
        readonly ResultPopup popup;
        readonly RunnerView runner;
        readonly CameraFitter cameraFitter;
        Step step = Step.None;

        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;
        public bool Active => step != Step.None;

        public Tutorial(CoachOverlay overlay, GameScreen screen, LoadoutPanel loadout, ResultPopup popup,
            RunnerView runner, CameraFitter cameraFitter)
        {
            this.overlay = overlay;
            this.screen = screen;
            this.loadout = loadout;
            this.popup = popup;
            this.runner = runner;
            this.cameraFitter = cameraFitter;

            overlay.Next += OnNext;
            overlay.Skip += Finish;
            screen.Run += () => { if (step == Step.FirstRun || step == Step.SecondRun) overlay.Hide(); };
            popup.EditRules += () => { if (step == Step.Edit) Go(Step.Equip); };
            loadout.Changed += () => { if (step == Step.Equip && loadout.Rules.Count > 0) Go(Step.Explain); };
        }

        public void Begin() => Go(Step.Runner);

        /// <summary>Called by the controller after the result popup is shown.</summary>
        public void OnRunFinished(RunResult result)
        {
            if (step == Step.FirstRun)
            {
                if (result.Won) Finish();
                else Go(Step.Edit);
            }
            else if (step == Step.SecondRun) Finish();
        }

        /// <summary>Stop without marking it done (e.g. the player left the level).</summary>
        public void Abort()
        {
            step = Step.None;
            overlay.Hide();
        }

        void OnNext()
        {
            if (step == Step.Runner) Go(Step.FirstRun);
            else if (step == Step.Explain) Go(Step.SecondRun);
        }

        void Go(Step next)
        {
            step = next;
            switch (next)
            {
                case Step.Runner:
                {
                    var c = cameraFitter.WorldToCanvasPoint(runner.transform.position + new Vector3(0, 0.35f, 0), screen.CanvasRect);
                    overlay.Show(new Rect(c.x - 40, c.y - 40, 80, 80),
                        "This is your runner. It walks on its own - you never press jump.", true);
                    break;
                }
                case Step.FirstRun:
                    overlay.Show(screen.RunButtonRect, "Press RUN with no cards and watch what happens.", false);
                    break;
                case Step.Edit:
                    overlay.Show(popup.EditButtonRect, "It walked straight into the pit. Nobody told it to jump.", false);
                    break;
                case Step.Equip:
                {
                    var slot = loadout.FirstEmptySlot;
                    if (slot != null) overlay.Show(slot, "Equip a card. Cards are the only way to tell the runner what to do.", false);
                    else Go(Step.Explain);
                    break;
                }
                case Step.Explain:
                {
                    var card = loadout.CardAt(0);
                    if (card != null)
                        overlay.Show(card.Body, "IF there is a pit ahead, THEN jump. It fires before every pit on the map - and every jump costs 1 qi.", true);
                    else Go(Step.SecondRun);
                    break;
                }
                case Step.SecondRun:
                    overlay.Show(screen.RunButtonRect, "Run it again.", false);
                    break;
            }
        }

        void Finish()
        {
            step = Step.None;
            overlay.Hide();
            PlayerPrefs.SetInt(DoneKey, 1);
        }
    }
}
