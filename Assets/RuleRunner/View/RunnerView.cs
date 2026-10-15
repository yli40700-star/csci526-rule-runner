using System.Collections;
using RuleRunner.Core;
using UnityEngine;

namespace RuleRunner.View
{
    /// <summary>The runner: a yellow block with an eye on its facing side. Plays back StepFrames.</summary>
    public class RunnerView : MonoBehaviour
    {
        const float Size = 0.7f;
        SpriteRenderer body, eye;
        Transform visual;
        int dir = 1;

        /// <summary>World position of the runner's head, for floating labels.</summary>
        public Vector3 HeadPosition => transform.position + new Vector3(0f, Size + 0.35f, 0f);

        void Awake()
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            body = Shapes.Create(visual, "Body", Shapes.Square, Palette.Runner, Vector2.zero, new Vector2(Size, Size), 5);
            eye = Shapes.Create(visual, "Eye", Shapes.Square, Palette.RunnerEye, new Vector2(0.18f, 0.38f), new Vector2(0.14f, 0.18f), 6);
        }

        public void Place(int x, int facing)
        {
            StopAllCoroutines();
            transform.position = new Vector3(x, 0f, 0f);
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            visual.localRotation = Quaternion.identity;
            body.color = Palette.Runner;
            SetDir(facing);
        }

        void SetDir(int facing)
        {
            dir = facing;
            eye.transform.localPosition = new Vector3(0.18f * dir, 0.38f, 0f);
        }

        /// <summary>Animates one simulation step over the given duration.</summary>
        public IEnumerator PlayStep(StepFrame f, float duration)
        {
            Vector3 from = new Vector3(f.XBefore, 0f, 0f);
            Vector3 to = new Vector3(f.X, 0f, 0f);
            float t = 0f;
            switch (f.Action)
            {
                case ActionType.Walk:
                case ActionType.Jump:
                case ActionType.BigJump:
                    if (f.Blocked)
                    {
                        // Bump into the wall and come back.
                        float hop = f.Action == ActionType.Walk ? 0f : 0.5f;
                        while (t < 1f)
                        {
                            t += Time.deltaTime / duration;
                            float k = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                            transform.position = from + new Vector3(0.28f * f.DirBefore * k, hop * k, 0f);
                            yield return null;
                        }
                    }
                    else
                    {
                        float height = f.Action == ActionType.Walk ? 0f : f.Action == ActionType.Jump ? 1.2f : 1.7f;
                        while (t < 1f)
                        {
                            t += Time.deltaTime / duration;
                            float k = Mathf.Clamp01(t);
                            float e = f.Action == ActionType.Walk ? k : Mathf.SmoothStep(0f, 1f, k);
                            transform.position = Vector3.Lerp(from, to, e) + Vector3.up * (height * 4f * k * (1f - k));
                            yield return null;
                        }
                    }
                    transform.position = to;
                    break;

                case ActionType.Turn:
                    while (t < 1f)
                    {
                        t += Time.deltaTime / duration;
                        float k = Mathf.Clamp01(t);
                        visual.localScale = new Vector3(Mathf.Abs(Mathf.Cos(k * Mathf.PI)), 1f, 1f);
                        if (k >= 0.5f && dir != f.Dir) SetDir(f.Dir);
                        yield return null;
                    }
                    visual.localScale = Vector3.one;
                    break;

                case ActionType.Wait:
                    while (t < 1f)
                    {
                        t += Time.deltaTime / duration;
                        float k = Mathf.Clamp01(t);
                        visual.localScale = new Vector3(1f + 0.12f * Mathf.Sin(k * Mathf.PI), 1f - 0.15f * Mathf.Sin(k * Mathf.PI), 1f);
                        yield return null;
                    }
                    visual.localScale = Vector3.one;
                    break;
            }
            SetDir(f.Dir);
        }

        /// <summary>Plays the ending: fall, hurt, or a victory hop.</summary>
        public IEnumerator PlayOutcome(Outcome outcome, float duration)
        {
            float t = 0f;
            Vector3 start = transform.position;
            switch (outcome)
            {
                case Outcome.Pit:
                    while (t < 1f)
                    {
                        t += Time.deltaTime / duration;
                        float k = Mathf.Clamp01(t);
                        transform.position = start + Vector3.down * (3f * k * k);
                        visual.localScale = Vector3.one * (1f - 0.5f * k);
                        yield return null;
                    }
                    break;
                case Outcome.Spike:
                case Outcome.Trap:
                    body.color = Palette.Dead;
                    while (t < 1f)
                    {
                        t += Time.deltaTime / duration;
                        float k = Mathf.Clamp01(t);
                        visual.localPosition = new Vector3(Mathf.Sin(k * 40f) * 0.08f * (1f - k), 0f, 0f);
                        visual.localRotation = Quaternion.Euler(0f, 0f, -80f * dir * k);
                        yield return null;
                    }
                    break;
                case Outcome.Win:
                    for (int hop = 0; hop < 2; hop++)
                    {
                        t = 0f;
                        while (t < 1f)
                        {
                            t += Time.deltaTime / (duration * 0.5f);
                            float k = Mathf.Clamp01(t);
                            transform.position = start + Vector3.up * (0.6f * 4f * k * (1f - k));
                            yield return null;
                        }
                    }
                    transform.position = start;
                    break;
                case Outcome.Timeout:
                    body.color = Color.Lerp(Palette.Runner, Color.gray, 0.7f);
                    yield return new WaitForSeconds(duration * 0.5f);
                    break;
            }
        }
    }
}
