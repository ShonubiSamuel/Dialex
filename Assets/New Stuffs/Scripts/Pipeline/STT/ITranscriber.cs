using System;
using System.Collections;
using UnityEngine;

namespace YourApp.Signs.Pipeline.STT
{
    /// <summary>
    /// STT interface for your pipeline.
    /// Note: Implementations ALSO implement SignPipelineController.ITranscriber for drop-in compatibility.
    /// </summary>
    public interface ITranscriber
    {
        /// <param name="audio">Mono or multi-channel AudioClip.</param>
        /// <param name="langHint">ISO code like "en","yo","ha","ig" (can be null).</param>
        /// <param name="onDone">Returns SignPipelineController.TranscriptionResult.</param>
        /// <param name="onError">Error callback (optional).</param>
        IEnumerator TranscribeAsync(
            AudioClip audio,
            string langHint,
            Action<SignPipelineController.TranscriptionResult> onDone,
            Action<Exception> onError = null);
    }
}