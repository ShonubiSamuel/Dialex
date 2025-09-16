using System;
using System.Collections;
using System.Collections.Generic;

namespace YourApp.Signs.Pipeline.Gloss
{
    /// <summary>Extract ordered glosses from ENGLISH text.</summary>
    public interface IGlossExtractor
    {
        IEnumerator ExtractAsync(
            string englishText,
            System.Action<System.Collections.Generic.List<string>> onDone,
            System.Action<System.Exception> onError = null);
    }
}