using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PhraseRule
{
    [Tooltip("If true, we require exact text match (case-insensitive). If false, we accept substring match.")]
    public bool exactMatch = true;

    [Tooltip("Phrase or fragment to match in the input.")]
    public string phrase;

    [Header("Action")]
    public bool useLibraryKey = true;     // if true, call playback.Enqueue(key); else play clip
    public string libraryKey;             // normalized key (e.g., 'hello')
    public AnimationClip clip;            // or a direct clip
    public bool clearQueueFirst = true;   // for clean demo
}

public class DemoPhraseRouter : MonoBehaviour
{
    public SignPlaybackController playback;

    [Tooltip("Define phrases that should trigger specific animations and skip the pipeline.")]
    public List<PhraseRule> rules = new List<PhraseRule>();

    /// <summary>Return true if the input was handled (pipeline should be skipped)</summary>
    public bool TryHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || playback == null) return false;

        string txt = input.Trim();
        foreach (var r in rules)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.phrase)) continue;

            bool match = r.exactMatch
                ? string.Equals(txt, r.phrase.Trim(), StringComparison.OrdinalIgnoreCase)
                : txt.IndexOf(r.phrase, StringComparison.OrdinalIgnoreCase) >= 0;

            if (!match) continue;

            if (r.clearQueueFirst) playback.ClearQueue();

            if (r.useLibraryKey && !string.IsNullOrWhiteSpace(r.libraryKey))
            {
                playback.Enqueue(GlossNormalizer.Normalize(r.libraryKey));
            }
            else if (r.clip != null)
            {
                playback.PlayOneShot(r.clip);
            }
            else
            {
                Debug.LogWarning("[DemoPhraseRouter] Rule matched but no key/clip configured.");
                return false;
            }

            return true; // consumed
        }

        return false;
    }
}
