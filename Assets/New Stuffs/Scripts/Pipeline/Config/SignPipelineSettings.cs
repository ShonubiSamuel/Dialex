using System;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Config
{
    /// <summary>
    /// Authoring-time master settings for the sign pipeline.
    /// Create via: Assets → Create → Sign Pipeline → Sign Pipeline Settings
    /// </summary>
    [CreateAssetMenu(menuName = "Sign Pipeline/Sign Pipeline Settings", fileName = "SignPipelineSettings")]
    public class SignPipelineSettings : ScriptableObject
    {
        public enum InputMode { LiveMic, AudioFile, Text }

        [Header("Input Source")]
        public InputMode inputMode = InputMode.Text;

        [Tooltip("Optional: default microphone device name (empty = system default).")]
        public string defaultMicDevice;

        [Tooltip("If true, show a basic input selector in dev builds.")]
        public bool showDevInputSwitcher = true;

        [Header("Performance")]
        [Tooltip("Target framerate for the app. Set 0 to keep project default.")]
        public int targetFrameRate = 60;

        [Tooltip("If >0, override vSyncCount (0 = off, 1 = every vsync, 2 = every second vsync...)")]
        public int vSyncCount = 0;

        [Header("Memory / Loading")]
        [Tooltip("Soft memory budget for content caches (MB). Used by your loaders to decide when to evict.")]
        public int memoryBudgetMB = 256;

        [Tooltip("How many clips the loader may request concurrently.")]
        public int maxConcurrentLoads = 4;

        [Tooltip("Addressables: use addressables pipeline for content? (runtime systems read this flag)")]
        public bool useAddressables = true;

        [Tooltip("Optional Addressables label to scope loads (e.g., 'mobile' or 'en-US').")]
        public string addressablesLabel;

        [Header("Apply On Startup (optional)")]
        [Tooltip("If true, a tiny runtime applier will enforce FPS/vSync on startup when this asset is in Resources/Config.")]
        public bool autoApplyOnStartup = true;

        /// <summary>
        /// Apply the performance knobs immediately (safe to call at runtime).
        /// </summary>
        public void ApplyPerformance()
        {
            if (vSyncCount >= 0) QualitySettings.vSyncCount = vSyncCount;
            if (targetFrameRate >= 0) Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>Convenience to read a settings asset from Resources/Config/.</summary>
        public static SignPipelineSettings LoadFromResources(string name = "SignPipelineSettings")
        {
            return Resources.Load<SignPipelineSettings>($"Config/{name}");
        }
    }

    /// <summary>
    /// Optional tiny runner that auto-applies <see cref="SignPipelineSettings"/> on startup.
    /// Place the settings asset under Resources/Config/ and enable 'autoApplyOnStartup'.
    /// </summary>
    public sealed class SignPipelineSettingsBootstrap : MonoBehaviour
    {
        [Tooltip("If empty, we look for Resources/Config/SignPipelineSettings.asset")]
        public string resourceName = "SignPipelineSettings";

        private void Awake()
        {
            var s = SignPipelineSettings.LoadFromResources(resourceName);
            if (s != null && s.autoApplyOnStartup) s.ApplyPerformance();
        }
    }
}
