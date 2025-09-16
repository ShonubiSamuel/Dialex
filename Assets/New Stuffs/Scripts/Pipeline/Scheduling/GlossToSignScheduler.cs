using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Scheduling
{
    /// <summary>
    /// Takes normalized gloss keys, validates/resolves them, applies pacing policy,
    /// optionally preloads, then feeds SignQueueController / SignPlaybackController.
    /// </summary>
    public class GlossToSignScheduler : MonoBehaviour
    {
        [Header("Dependencies")]
        public SignResolver resolver;
        public SignQueueController queueController;          // preferred
        public SignPlaybackController playbackController;    // fallback if no queue controller
        public SignLibraryLoader libraryLoader;              // for preloading (optional)

        [Header("Policy")]
        public PacingProfile pacing;
        [Tooltip("If true, drop unknown keys (not present in resolver).")]
        public bool dropUnknownKeys = true;
        [Tooltip("Remove consecutive duplicates (A, A → A).")]
        public bool compactConsecutiveDuplicates = true;

        [Header("Preload")]
        public PreloadHints preloadHints;
        [Tooltip("Preload next N items before starting playback.")]
        public int initialPreload = 3;
        
        public event Action<IReadOnlyList<string>> OnScheduled;
        public event Action<IReadOnlyList<string>> OnStarted;

        /// <summary>
        /// Entry point: provide already-normalized keys (use GlossNormalizer upstream).
        /// We still normalize defensively in case caller forgot.
        /// </summary>
        public void ScheduleFromGlosses(IList<string> normalizedKeys)
        {
            if (normalizedKeys == null || normalizedKeys.Count == 0)
            {
                Debug.LogWarning("[GlossToSignScheduler] No keys to schedule.");
                return;
            }

            // 1) Defensive normalization + optional compaction
            var keys = new List<string>(normalizedKeys.Count);
            string prev = null;
            foreach (var raw in normalizedKeys)
            {
                var k = GlossNormalizer.Normalize(raw);
                if (string.IsNullOrEmpty(k)) continue;
                if (compactConsecutiveDuplicates && prev == k) continue;
                keys.Add(k);
                prev = k;
            }

            if (keys.Count == 0)
            {
                Debug.LogWarning("[GlossToSignScheduler] Keys eliminated after normalization/compaction.");
                return;
            }

            // 2) Validate with resolver (optional)
            if (dropUnknownKeys && resolver != null)
                keys.RemoveAll(k => !resolver.Contains(k));

            if (keys.Count == 0)
            {
                Debug.LogWarning("[GlossToSignScheduler] No valid keys after resolver validation.");
                return;
            }

            // 3) Apply pacing to the player (best-effort)
            if (pacing != null && playbackController != null)
                pacing.ApplyToPlayer(playbackController);

            // 4) Preload hints
            if (preloadHints != null && libraryLoader != null)
            {
                preloadHints.loader = libraryLoader;
                preloadHints.Warmup(keys, Mathf.Max(initialPreload, 0));
            }

            OnScheduled?.Invoke(keys);

            // 5) Feed queue or enqueue directly
            if (queueController != null)
            {
                // If your SignQueueController exposes a method with pacing args,
                // call it here. For now, we just play the keys in order.
                queueController.PlaySequenceFromKeys(keys);
                OnStarted?.Invoke(keys);
            }
            else if (playbackController != null)
            {
                foreach (var k in keys) playbackController.Enqueue(k);
                OnStarted?.Invoke(keys);
            }
            else
            {
                Debug.LogError("[GlossToSignScheduler] No output target assigned (queueController / playbackController).");
            }
        }

        // Convenience overload for raw English text (normalize + schedule via resolver map check)
        public void ScheduleFromRawGlosses(IList<string> rawGlosses)
        {
            var tmp = new List<string>(rawGlosses ?? Array.Empty<string>());
            ScheduleFromGlosses(tmp);
        }
    }
}
