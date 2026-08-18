using System.Collections.Generic;
using UnityEngine;

namespace Bayou.UI
{
    /// <summary>One contextual button hint (glyph + verb).</summary>
    public readonly struct InteractionPrompt
    {
        public readonly string Button;
        public readonly string Action;
        public readonly int Priority;
        public readonly float SortDistanceSq;

        public InteractionPrompt(string button, string action, int priority = 50, float sortDistanceSq = 0f)
        {
            Button = button ?? "";
            Action = action ?? "";
            Priority = priority;
            SortDistanceSq = sortDistanceSq;
        }

        public string DisplayLine =>
            string.IsNullOrWhiteSpace(Button) ? Action : $"[{Button}]  {Action}";
    }

    /// <summary>Anything that can surface a contextual control hint while the player is near / holding a tool.</summary>
    public interface IInteractionPromptSource
    {
        bool TryGetInteractionPrompt(out InteractionPrompt prompt);
    }

    /// <summary>Collects prompt sources and picks the best one each frame.</summary>
    public static class InteractionPromptBroker
    {
        private static readonly List<IInteractionPromptSource> Sources = new();

        public static void Register(IInteractionPromptSource source)
        {
            if (source == null) return;
            if (!Sources.Contains(source))
                Sources.Add(source);
        }

        public static void Unregister(IInteractionPromptSource source)
        {
            if (source == null) return;
            Sources.Remove(source);
        }

        public static bool TryGetBest(out InteractionPrompt best)
        {
            best = default;
            var found = false;
            var bestPriority = int.MinValue;
            var bestDist = float.MaxValue;

            for (var i = Sources.Count - 1; i >= 0; i--)
            {
                var src = Sources[i];
                if (src == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                // Drop destroyed Unity objects that still implement the interface.
                if (src is Object unityObj && unityObj == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (!src.TryGetInteractionPrompt(out var prompt))
                    continue;

                if (!found ||
                    prompt.Priority > bestPriority ||
                    (prompt.Priority == bestPriority && prompt.SortDistanceSq < bestDist))
                {
                    best = prompt;
                    bestPriority = prompt.Priority;
                    bestDist = prompt.SortDistanceSq;
                    found = true;
                }
            }

            return found;
        }
    }
}
