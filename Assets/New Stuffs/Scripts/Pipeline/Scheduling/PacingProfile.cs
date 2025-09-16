using UnityEngine;

namespace YourApp.Signs.Pipeline.Scheduling
{
    /// <summary>
    /// Authorable pacing defaults for sign playback & scheduling.
    /// Create via: Assets → Create → Sign Pipeline → Pacing Profile
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/Pacing Profile", fileName = "PacingProfile")]
    public class PacingProfile : ScriptableObject
    {
        [Header("Crossfade & Timing (seconds)")]
        [Tooltip("Preferred overlap between consecutive signs (scheduler hint).")]
        public float interSignCrossfade = 0.16f;

        [Tooltip("Trim off the tail of each clip before handoff (reduces end pops).")]
        public float endCrop = 0.02f;

        [Tooltip("Start the next clip slightly 'in' to skip stiff first key.")]
        public float nextStartOffset = 0.02f;

        [Tooltip("Minimum gap injected if two clips are extremely short (rare).")]
        public float minGap = 0.00f;

        [Header("Playback Defaults (mirrors player fields)")]
        public float defaultBlendIn = 0.18f;
        public float defaultBlendOut = 0.12f;
        [Range(0.1f, 2f)] public float playbackSpeed = 1.0f;

        [Header("Idle Policy")]
        public bool playIdleWhenEmpty = true;
        public bool idleShuffle = true;
        public float idleBlendIn = 0.18f;

        /// <summary>
        /// Best-effort apply to a SignPlaybackController. Uses reflection for optional fields.
        /// Safe to call even if some fields don't exist in your current controller version.
        /// </summary>
        public void ApplyToPlayer(MonoBehaviour playbackController)
        {
            if (playbackController == null) return;
            var t = playbackController.GetType();

            TrySetField(t, playbackController, "defaultBlendIn",  defaultBlendIn);
            TrySetField(t, playbackController, "defaultBlendOut", defaultBlendOut);
            TrySetField(t, playbackController, "playbackSpeed",   playbackSpeed);
            TrySetField(t, playbackController, "playIdleWhenEmpty", playIdleWhenEmpty);
            TrySetField(t, playbackController, "idleShuffle", idleShuffle);
            TrySetField(t, playbackController, "idleBlendIn", idleBlendIn);

            // Optional smoothing fields if your controller exposes them
            TrySetField(t, playbackController, "endCrop",         endCrop);
            TrySetField(t, playbackController, "nextStartOffset", nextStartOffset);
        }

        private static void TrySetField(System.Type type, object target, string name, object value)
        {
            var f = type.GetField(name);
            if (f != null && f.FieldType == value.GetType()) f.SetValue(target, value);
        }
    }
}
