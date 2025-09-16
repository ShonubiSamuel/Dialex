using System;
using System.Collections;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Translation
{
    /// <summary>
    /// Wrapper that short-circuits when translation isn't needed
    /// and delegates to a real translator otherwise.
    /// Implemented as a translator itself for easy wiring.
    /// </summary>
    public class TranslatorRouter : MonoBehaviour,
        ITranslator, SignPipelineController.ITranslator
    {
        [Header("Delegates")]
        [Tooltip("Translator to use when srcLang != dstLang.")]
        public MonoBehaviour translatorBehaviour; // ITranslator

        [Header("Options")]
        public bool treatNullSrcAsNeedsDetect = true; // if true and src null, still call translator
        public bool trimOutput = true;

        private ITranslator _translator;

        private void Awake()
        {
            _translator = translatorBehaviour as ITranslator;
            if (_translator == null && translatorBehaviour != null)
                Debug.LogError("[TranslatorRouter] translatorBehaviour does not implement ITranslator.");
        }

        public IEnumerator TranslateAsync(
            string srcText, string srcLang, string dstLang,
            Action<string> onDone, Action<Exception> onError = null)
        {
            // No work needed?
            if (!string.IsNullOrWhiteSpace(srcText) &&
                !string.IsNullOrEmpty(dstLang) &&
                !string.IsNullOrEmpty(srcLang) &&
                string.Equals(srcLang, dstLang, StringComparison.OrdinalIgnoreCase))
            {
                onDone?.Invoke(trimOutput ? srcText.Trim() : srcText);
                yield break;
            }

            if (_translator == null)
            {
                // If we don't have a delegate, best-effort passthrough
                onDone?.Invoke(trimOutput ? srcText?.Trim() : srcText);
                yield break;
            }

            bool needTranslate = true;
            if (!treatNullSrcAsNeedsDetect && string.IsNullOrEmpty(srcLang))
                needTranslate = false;

            if (!needTranslate)
            {
                onDone?.Invoke(trimOutput ? srcText?.Trim() : srcText);
                yield break;
            }

            string result = null; Exception ex = null; bool done = false;
            yield return _translator.TranslateAsync(srcText, srcLang, dstLang,
                r => { result = r; done = true; },
                e => { ex = e; done = true; });

            if (ex != null)
            {
                onError?.Invoke(ex);
                yield break;
            }

            onDone?.Invoke(trimOutput ? (result ?? "").Trim() : (result ?? ""));
        }
    }
}
