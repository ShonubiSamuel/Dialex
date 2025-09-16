using System;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Config
{
    /// <summary>
    /// Author a pool of idle motions (keys or clips) for fallback/ambient animation.
    /// Create via: Assets → Create → Sign Pipeline → Idle Set
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/Idle Set", fileName = "IdleSet")]
    public class IdleSet : ScriptableObject
    {
        public enum SourceType { Keys, Clips, Mixed }

        [Serializable]
        public class Entry
        {
            [Tooltip("If set, we use this normalized key via SignLibraryLoader/Addressables.")]
            public string key;

            [Tooltip("If set, we use this clip directly (no loading).")]
            public AnimationClip clip;

            [Tooltip("Relative weight for random selection.")]
            [Range(0f, 10f)] public float weight = 1f;

            public bool HasKey => !string.IsNullOrWhiteSpace(key);
            public bool HasClip => clip != null;
        }

        [Header("Mode")]
        public SourceType mode = SourceType.Keys;

        [Header("Entries")]
        public List<Entry> entries = new List<Entry>();

        [Header("Policy")]
        [Tooltip("If true, random pick by weight; otherwise sequential.")]
        public bool shuffle = true;

        /// <summary>Pick a next entry index (weighted if shuffle).</summary>
        public int PickIndex(System.Random rng, int currentIndex)
        {
            if (entries == null || entries.Count == 0) return -1;

            if (!shuffle)
            {
                var next = currentIndex + 1;
                if (next >= entries.Count) next = 0;
                return next;
            }

            // weighted random
            double sum = 0;
            foreach (var e in entries) sum += Mathf.Max(0f, e.weight);
            if (sum <= 0) return UnityEngine.Random.Range(0, entries.Count);

            double pick = (rng?.NextDouble() ?? UnityEngine.Random.value) * sum;
            double acc = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                acc += Mathf.Max(0f, entries[i].weight);
                if (pick <= acc) return i;
            }
            return entries.Count - 1;
        }

        /// <summary>
        /// Convenience to apply this IdleSet to a SignPlaybackController:
        /// - If there is exactly one clip-only entry → set as idleClip.
        /// - Else → push all keys into idleKeys.
        /// </summary>
        public void ApplyToPlayer(MonoBehaviour playbackController)
        {
            if (playbackController == null) return;
            var t = playbackController.GetType();

            // single direct clip?
            AnimationClip single = null;
            int clipCount = 0; int keyCount = 0;
            foreach (var e in entries)
            {
                if (e.HasClip) { clipCount++; if (clipCount == 1) single = e.clip; }
                if (e.HasKey) keyCount++;
            }

            if (clipCount == 1 && keyCount == 0 && single != null)
            {
                var f = t.GetField("idleClip");
                if (f != null) f.SetValue(playbackController, single);
                // ensure array cleared if present
                var f2 = t.GetField("idleKeys");
                if (f2 != null) f2.SetValue(playbackController, Array.Empty<string>());
                return;
            }

            // otherwise send keys array (normalized)
            var keys = new List<string>();
            foreach (var e in entries)
            {
                if (e.HasKey)
                {
                    var k = GlossNormalizer.Normalize(e.key);
                    if (!string.IsNullOrEmpty(k) && !keys.Contains(k)) keys.Add(k);
                }
            }
            var fi = t.GetField("idleKeys");
            if (fi != null) fi.SetValue(playbackController, keys.ToArray());
        }
    }
}
