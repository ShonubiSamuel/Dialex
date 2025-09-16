using System;
using System.Collections;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Translation
{
    /// <summary>Generic translator interface for the pipeline.</summary>
    public interface ITranslator
    {
        /// <summary>Translate srcText from srcLang → dstLang.</summary>
        IEnumerator TranslateAsync(
            string srcText,
            string srcLang,
            string dstLang,
            Action<string> onDone,
            Action<Exception> onError = null);
    }
}