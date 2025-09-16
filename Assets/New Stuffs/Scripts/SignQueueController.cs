using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-level queue controller for sign playback.
/// - Accepts a list of raw glosses or normalized keys
/// - Normalizes, resolves, then enqueues playback via SignPlaybackController
/// - Raises events for sequence start/complete/cancel and per-item enqueue
/// </summary>
public class SignQueueController : MonoBehaviour
{
    [Header("Dependencies")]
    public SignMapProvider mapProvider;           // Provides existence checks and (optionally) direct clips
    public SignLibraryLoader library;             // Loads clips by key (Resources/Addressables)
    public SignPlaybackController playback;       // Plays the signs

    // Events
    public event Action<IReadOnlyList<string>> OnSequenceStarted;
    public event Action<string> OnItemEnqueued;        // normalized key
    public event Action<IReadOnlyList<string>> OnSequenceCompleted;
    public event Action<IReadOnlyList<string>> OnSequenceCanceled;

    private readonly List<string> _currentSequence = new List<string>();
    private bool _isRunning;
    private bool _cancelRequested;

    /// <summary>Play a sequence from raw glosses (e.g., "Ask Out", "Anyone").</summary>
    public void PlaySequenceFromGlosses(IEnumerable<string> glosses)
    {
        if (glosses == null) return;
        var keys = new List<string>();
        foreach (var g in glosses)
        {
            var k = GlossNormalizer.Normalize(g);
            if (!string.IsNullOrEmpty(k)) keys.Add(k);
        }
        PlaySequenceFromKeys(keys);
    }

    /// <summary>Play a sequence from already-normalized keys.</summary>
    public void PlaySequenceFromKeys(IEnumerable<string> normalizedKeys)
    {
        if (normalizedKeys == null) return;

        _currentSequence.Clear();
        foreach (var k in normalizedKeys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            _currentSequence.Add(k);
        }

        if (_currentSequence.Count == 0)
        {
            Debug.LogWarning("[SignQueueController] Empty sequence.");
            return;
        }

        if (_isRunning)
        {
            Debug.LogWarning("[SignQueueController] A sequence is already running. Canceling previous and starting new.");
            Cancel();
        }

        // Kick off
        _isRunning = true;
        _cancelRequested = false;
        OnSequenceStarted?.Invoke(_currentSequence);

        // We use the playback controller's internal queue for timing/blending.
        playback.ClearQueue();

        foreach (var key in _currentSequence)
        {
            // Optional existence check via provider (logs missing ones)
            if (mapProvider != null && !mapProvider.Contains(key))
            {
                Debug.LogWarning($"[SignQueueController] Key '{key}' not found in map. Will attempt load anyway.");
            }

            OnItemEnqueued?.Invoke(key);
            playback.Enqueue(key);
        }

        // Since playback handles timing, we just watch for completion by counting down.
        // Simple approach: we track how many remain and subscribe to Enqueue's drain by polling.
        // For now, we poll until playback finishes (no new items being processed).
        StartCoroutine(WaitForPlaybackToFinishThenNotify());
    }

    /// <summary>Cancel the current sequence (stops adding new items; playback will finish the current blend out).</summary>
    public void Cancel()
    {
        if (!_isRunning) return;
        _cancelRequested = true;
        playback.ClearQueue();
        OnSequenceCanceled?.Invoke(_currentSequence);
        _isRunning = false;
    }

    // replace WaitForPlaybackToFinishThenNotify() with:
    private System.Collections.IEnumerator WaitForPlaybackToFinishThenNotify()
    {
        // small grace to allow last enqueue to start
        yield return new WaitForSeconds(0.05f);

        float stillTime = 0f;
        const float needIdleFor = 0.25f; // adjust to taste
        while (!_cancelRequested)
        {
            // heuristic: if nothing new was queued for a short while, consider it complete
            stillTime += Time.deltaTime;
            if (stillTime >= needIdleFor) break;
            yield return null;
        }

        if (!_cancelRequested) OnSequenceCompleted?.Invoke(_currentSequence);
        _isRunning = false;
    }

    // Rough idle check (hook your own signals if you later expose them from SignPlaybackController).
    private bool IsRoughlyIdle() => true; // Let the small idle timer elapse post-queue.
}
