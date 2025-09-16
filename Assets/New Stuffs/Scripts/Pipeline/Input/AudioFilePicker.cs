using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YourApp.Signs.Pipeline.Input
{
    /// <summary>
    /// Picks an audio file (Android/iOS via Native File Picker; Editor via EditorUtility)
    /// and loads it as an AudioClip via UnityWebRequestMultimedia.
    /// Supports WAV/MP3/OGG/AAC/M4A, local "file://" and http(s) URLs.
    /// </summary>
    public class AudioFilePicker : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, creates a streaming AudioClip (lower memory).")]
        public bool streamAudio = false;

        [Tooltip("If true, downmixes to mono before invoking OnPcmReady.")]
        public bool forceMono = true;

        [Tooltip("iOS only: copy picked file to persistentDataPath to keep it after app closes.")]
        public bool copyPickedFileToPersistentOnIOS = false;

        public event Action<AudioClip> OnAudioLoaded;
        public event Action<float[], int> OnPcmReady; // float[] mono PCM + sampleRate
        public event Action<string> OnError;

        // Cached per-platform file type filters for NativeFilePicker
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private string[] _audioTypes;
#endif

        private void Awake()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try
            {
                // Use specific extensions so the native sheet stays focused on audio
                // (ConvertExtensionToFileType returns MIME on Android, UTI on iOS)
                _audioTypes = new string[]
                {
                    NativeFilePicker.ConvertExtensionToFileType("wav"),
                    NativeFilePicker.ConvertExtensionToFileType("mp3"),
                    NativeFilePicker.ConvertExtensionToFileType("ogg"),
                    NativeFilePicker.ConvertExtensionToFileType("aac"),
                    NativeFilePicker.ConvertExtensionToFileType("m4a")
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("Audio type conversion failed, will fall back to all files. " + e.Message);
                _audioTypes = null; // Passing null allows all file types
            }
#endif
        }

        /// <summary>
        /// Opens the platform-native file picker (Android/iOS) or Editor file panel
        /// to choose a single audio file and loads it.
        /// </summary>
        public void PickAudio()
        {
#if UNITY_EDITOR
            PickInEditor();
#elif (UNITY_ANDROID || UNITY_IOS)
            if (NativeFilePicker.IsFilePickerBusy())
                return;

            // NativeFilePicker handles permission internally for PickFile
            NativeFilePicker.PickFile(OnNativeFilePicked,
                // allowed types can be null to allow all; we try to constrain to audio
                _audioTypes);
#else
            OnError?.Invoke("Native file picking is supported only on Android/iOS (Editor uses context menu).");
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Pick Audio (Editor)")]
        public void PickInEditor()
        {
            string path = EditorUtility.OpenFilePanel("Pick audio", "", "wav,mp3,ogg,aac,m4a");
            if (!string.IsNullOrEmpty(path))
                StartCoroutine(LoadFromPath(path));
        }
#endif

        /// <summary>
        /// Still available for direct URLs or absolute file paths.
        /// </summary>
        public void LoadFromUrl(string urlOrPath)
        {
            StartCoroutine(LoadFromPath(urlOrPath));
        }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void OnNativeFilePicked(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                // user cancelled
                return;
            }

#if UNITY_IOS
            // On iOS, the system may delete imported files after app close.
            // If you need it to persist, copy to persistentDataPath.
            if (copyPickedFileToPersistentOnIOS)
            {
                try
                {
                    string fileName = Path.GetFileName(path);
                    string dest = Path.Combine(Application.persistentDataPath, fileName);

                    // Avoid overwriting an identical file unnecessarily
                    if (!File.Exists(dest))
                        File.Copy(path, dest, overwrite: false);

                    path = dest;
                }
                catch (Exception e)
                {
                    OnError?.Invoke($"Failed to copy file to persistentDataPath: {e.Message}");
                    return;
                }
            }
#endif
            StartCoroutine(LoadFromPath(path));
        }
#endif

        private IEnumerator LoadFromPath(string urlOrPath)
        {
            string url = urlOrPath;

            // If not http(s) or file://, assume absolute local path and prefix file://
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                url = "file://" + url;
            }

            var type = GuessAudioType(urlOrPath);

            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                var dha = (DownloadHandlerAudioClip)req.downloadHandler;
                dha.streamAudio = streamAudio;

                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    OnError?.Invoke($"Load error: {req.error}");
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null)
                {
                    OnError?.Invoke("Decoder returned null clip.");
                    yield break;
                }

                OnAudioLoaded?.Invoke(clip);

                // Extract PCM (mono mix if requested)
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);

                float[] mono = (forceMono && clip.channels > 1)
                    ? DownmixToMono(data, clip.channels)
                    : data;

                OnPcmReady?.Invoke(mono, clip.frequency);
            }
        }

        private static float[] DownmixToMono(float[] interleaved, int channels)
        {
            int frames = interleaved.Length / channels;
            var mono = new float[frames];
            int idx = 0;
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < channels; c++) sum += interleaved[idx++];
                mono[f] = (float)(sum / channels);
            }
            return mono;
        }

        private static AudioType GuessAudioType(string pathOrUrl)
        {
            string ext = Path.GetExtension(pathOrUrl)?.ToLowerInvariant();
            switch (ext)
            {
                case ".wav": return AudioType.WAV;
                case ".mp3": return AudioType.MPEG;
                case ".ogg": return AudioType.OGGVORBIS;
#if UNITY_2020_3_OR_NEWER
                case ".aac": return AudioType.ACC;     // Unity 2020.3+ alias
                case ".m4a": return AudioType.MPEG;    // Unity typically routes m4a via MPEG
#endif
                default:     return AudioType.UNKNOWN; // UWR will try to guess
            }
        }
    }
}
