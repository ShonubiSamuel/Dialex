using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Scheduling
{
    /// <summary>
    /// Predictively preloads upcoming clips via SignLibraryLoader to warm its cache.
    /// </summary>
    public class PreloadHints : MonoBehaviour
    {
        [Header("Dependencies")]
        public SignLibraryLoader loader;

        [Header("Behavior")]
        [Tooltip("Max number of upcoming signs to preload.")]
        public int lookahead = 3;

        [Tooltip("Delay between individual preload requests (seconds).")]
        public float throttleSeconds = 0.02f;

        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);
        private Coroutine _co;

        public void CancelAll()
        {
            if (_co != null) StopCoroutine(_co);
            _inFlight.Clear();
            _co = null;
        }

        /// <summary>Preload first N keys from list (unique, not yet in-flight).</summary>
        public void Warmup(IList<string> upcomingKeys, int? overrideLookahead = null)
        {
            if (loader == null || upcomingKeys == null || upcomingKeys.Count == 0) return;

            int N = Mathf.Clamp(overrideLookahead ?? lookahead, 0, upcomingKeys.Count);
            if (N <= 0) return;

            // Build unique queue
            var queue = new Queue<string>();
            for (int i = 0; i < upcomingKeys.Count && queue.Count < N; i++)
            {
                var k = upcomingKeys[i];
                if (string.IsNullOrEmpty(k)) continue;
                if (_inFlight.Contains(k)) continue;
                queue.Enqueue(k);
                _inFlight.Add(k);
            }

            if (queue.Count == 0) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(PreloadRoutine(queue));
        }

        private IEnumerator PreloadRoutine(Queue<string> q)
        {
            while (q.Count > 0)
            {
                var key = q.Dequeue();
                bool done = false;

                // Fire and forget; loader caches internally when enableCache = true
                loader.LoadClip(key, _ => { done = true; });

                // Wait until this one finishes (avoid spamming)
                while (!done) yield return null;

                // Small throttle to keep frame hitching low
                if (throttleSeconds > 0f) yield return new WaitForSeconds(throttleSeconds);
                _inFlight.Remove(key);
            }
            _co = null;
        }
    }
}
