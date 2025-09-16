using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace YourApp.Signs.Pipeline.STT
{
    /// <summary>
    /// Runs a local Whisper binary (e.g., whisper.cpp or OpenAI-whisper) on desktop/editor.
    /// Writes a temp WAV, executes process, reads a .txt result.
    /// Not supported on mobile/web.
    /// </summary>
    public class WhisperLocalTranscriber : MonoBehaviour,
        SignPipelineController.ITranscriber, ITranscriber
    {
        [Header("Executable")]
        [Tooltip("Path to whisper executable (whisper.cpp main, or python 'whisper' wrapper).")]
        public string executablePath;

        [Tooltip("Arguments template. Tokens: {input} {lang} {outdir}. Example for whisper.cpp:\n" +
                 "--model base.en --language {lang} --output-txt --output-dir \"{outdir}\" \"{input}\"")]
        public string argsTemplate = "--model base.en --language {lang} --output-txt --output-dir \"{outdir}\" \"{input}\"";

        [Header("Limits")]
        public int    maxSeconds = 300;
        public string languageFallback = "en";

        public IEnumerator TranscribeAsync(
            AudioClip audio, string langHint,
            Action<SignPipelineController.TranscriptionResult> onDone,
            Action<Exception> onError = null)
        {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
            if (audio == null) { onError?.Invoke(new ArgumentNullException(nameof(audio))); yield break; }
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            { onError?.Invoke(new FileNotFoundException("Whisper executable not found.", executablePath)); yield break; }

            // Extract WAV temp file
            var inter = TranscriptionCache.GetInterleaved(audio);
            var mono  = TranscriptionCache.DownmixToMono(inter, audio.channels);
            var wav   = VendorTranscriberAdapter.WavEncoder.EncodeToWavBytes(mono, 1, audio.frequency);

            var tmpDir = Path.Combine(Application.temporaryCachePath, "whisper_local");
            if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
            var inPath  = Path.Combine(tmpDir, $"in_{Guid.NewGuid():N}.wav");
            var outBase = Path.GetFileNameWithoutExtension(inPath);
            var outTxt  = Path.Combine(tmpDir, outBase + ".txt");

            File.WriteAllBytes(inPath, wav);

            var lang = string.IsNullOrEmpty(langHint) ? languageFallback : langHint;
            var args = argsTemplate
                        .Replace("{input}", inPath.Replace("\\", "/"))
                        .Replace("{lang}",  lang)
                        .Replace("{outdir}", tmpDir.Replace("\\", "/"));

            // Run process
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            Exception error = null;
            string stdout = "", stderr = "";

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout += e.Data + "\n"; };
                proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr += e.Data + "\n"; };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                yield break;
            }

            // Wait with a timeout
            var start = Time.realtimeSinceStartup;
            while (!proc.HasExited)
            {
                if (Time.realtimeSinceStartup - start > maxSeconds)
                {
                    try { proc.Kill(); } catch {}
                    onError?.Invoke(new TimeoutException("Whisper process timeout."));
                    yield break;
                }
                yield return null;
            }

            if (proc.ExitCode != 0)
            {
                onError?.Invoke(new Exception($"Whisper exit {proc.ExitCode}\n{stderr}"));
                yield break;
            }

            string text = "";
            try
            {
                if (File.Exists(outTxt))
                    text = File.ReadAllText(outTxt);
                else
                    text = stdout; // fallback to stdout
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                yield break;
            }

            var res = new SignPipelineController.TranscriptionResult
            {
                text = text ?? "",
                language = lang,
                duration = audio.samples / (float)audio.frequency
            };
            onDone?.Invoke(res);
#else
            onError?.Invoke(new PlatformNotSupportedException("WhisperLocalTranscriber is desktop/editor only."));
            yield break;
#endif
        }
    }
}
