using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public class SignPlaybackController : MonoBehaviour
{
    [Header("Dependencies")]
    public SignLibraryLoader library;

    [Header("Playback Defaults")]
    public float defaultBlendIn = 0.18f;
    public float defaultBlendOut = 0.12f;
    [Range(0.1f, 2.0f)] public float playbackSpeed = 1.0f;
    
    [Header("Speed Control")]
    public UnityEngine.UI.Button speedButton;          // assign in Inspector
    public TMPro.TMP_Text speedLabel;                  // optional UI label "0.5x / 1.0x / 1.5x"
    public float[] speedSteps = new float[] { 0.5f, 1f, 1.5f };

    private int _speedIndex = 1; // default to 1.0x (index 1 in speedSteps)


    [Header("Seamless Queue")]
    public bool seamlessQueue = true;
    public float interSignCrossfade = 0.12f;

    [Header("Idle (multiple clips only)")]
    public bool playIdleWhenEmpty = true;
    public AnimationClip neutralIdleClip; 
    [Tooltip("Clips to cycle when nothing is queued.")]
    public AnimationClip[] idleClips;
    [Tooltip("Optional gap between idle clips (seconds).")]
    [Range(0f, 3f)] public float idleCycleGap = 0f;
    [Tooltip("Blend duration when returning to idle/base.")]
    public float idleBlendIn = 0.18f;
    [Tooltip("Crossfade time between idle clips during cycling.")]
    public float idleCrossfade = 0.20f;

    [Tooltip("If true, we rebuild Base to the new idle after the crossfade (keeps continuity).")]
    public bool idleRebaseAfterCrossfade = true;


    [Header("Blend Tuning")]
    [Tooltip("Seconds to skip at the start of each incoming clip to avoid stiff first key.")]
    public float nextStartOffset = 0.02f;   // 1–2 frames
    [Tooltip("Seconds to trim from the end of the outgoing clip before the crossfade.")]
    public float endCrop = 0.02f;           // 1–2 frames
    
    // ---- add fields ----
    private bool _foregroundPlaying = false;


    private Animator _animator;
    private PlayableGraph _graph;
    private AnimationPlayableOutput _output;
    // mixer: [0]=base (idle cycling lives here), [1]=slotA, [2]=slotB
    private AnimationMixerPlayable _mixer;
    private AnimationClipPlayable _base;     // persistent base/idle
    private AnimationClipPlayable _slotA;    // dynamic sign
    private AnimationClipPlayable _slotB;    // dynamic sign
    private int _activeSlot = -1;            // -1 none, 1=A, 2=B
    private bool _graphInitialized;

    private readonly Queue<string> _keyQueue = new Queue<string>();
    private bool _queueRunning;

    // Idle cycle state
    private System.Random _rng;
    private Coroutine _idleCycleCo;
    private int _idleClipsIndex;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rng = new System.Random();
        EnsureGraph();
    }

    private void OnEnable()
    {
        if (_graphInitialized) _graph.Play();
        if (playIdleWhenEmpty && !_queueRunning && _activeSlot < 0)
            StartIdleCycleIfNeeded();
 
        if (_graphInitialized) _graph.Play();
        if (playIdleWhenEmpty && !_queueRunning && _activeSlot < 0)
            StartIdleCycleIfNeeded();

        if (speedButton) speedButton.onClick.AddListener(CyclePlaybackSpeed);

        // Initialize label to current speed
        SnapSpeedIndexTo(playbackSpeed);
        UpdateSpeedLabel();
    }

    private void OnDisable()
    {
        StopIdleCycle();
        if (_graphInitialized) _graph.Stop();

        if (speedButton) speedButton.onClick.RemoveListener(CyclePlaybackSpeed);
   
    }

    private void OnDestroy()
    {
        StopIdleCycle();
        if (!_graphInitialized) return;

        if (_slotA.IsValid()) _slotA.Destroy();
        if (_slotB.IsValid()) _slotB.Destroy();
        if (_base.IsValid()) _base.Destroy();

        _mixer.Destroy();
        _graph.Destroy();
    }

    private void EnsureGraph()
    {
        if (_graphInitialized) return;

        _graph = PlayableGraph.Create("SignPlaybackGraph");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _mixer = AnimationMixerPlayable.Create(_graph, 3, true);

        // Pick a real idle for Base immediately (no empty clip).
        var baseIdle = neutralIdleClip != null 
            ? neutralIdleClip 
            : (idleClips != null && idleClips.Length > 0 ? idleClips[0] : null);

        if (baseIdle == null)
        {
            Debug.LogWarning("[SignPlaybackController] No neutralIdleClip / idleClips set. Expect pose pops.");
            baseIdle = new AnimationClip { name = "FallbackEmpty" }; // last resort
        }

        _base = AnimationClipPlayable.Create(_graph, baseIdle);
        _base.SetApplyFootIK(true);              // consistent IK (see §3)
        _base.SetApplyPlayableIK(false);
        _base.SetSpeed(playbackSpeed);
        _base.SetDuration(double.PositiveInfinity);
        _base.SetTime(0.0);                      // prewarm at start of clip

        _graph.Connect(_base, 0, _mixer, 0);
        _mixer.SetInputWeight(0, 1f);            // show base from the first frame
        _mixer.SetInputWeight(1, 0f);
        _mixer.SetInputWeight(2, 0f);

        _output = AnimationPlayableOutput.Create(_graph, "SignOutput", _animator);
        _output.SetSourcePlayable(_mixer);

        _graph.Evaluate(0f);                     // PREWARM graph (eliminates one-frame pops)
        _graph.Play();
        _graphInitialized = true;
    }

    private void EnsureBaseHasIdlePose(double syncTime = 0.0)
    {
        var idle = neutralIdleClip != null 
            ? neutralIdleClip 
            : (idleClips != null && idleClips.Length > 0 ? idleClips[0] : null);

        if (!idle) return;

        // Replace Base only when its weight is ~0 to avoid visible pop.
        if (_base.IsValid()) { _mixer.DisconnectInput(0); _base.Destroy(); }

        _base = AnimationClipPlayable.Create(_graph, idle);
        _base.SetApplyFootIK(true);
        _base.SetApplyPlayableIK(false);
        _base.SetSpeed(playbackSpeed);

        // Clamp to clip length
        double len = Mathf.Max(0.0001f, idle.length);
        _base.SetTime(syncTime % len);

        _graph.Connect(_base, 0, _mixer, 0);
        _mixer.SetInputWeight(0, 0f);
        _graph.Evaluate(0f);
    }


    // ---------- Public API ----------
    public void PlayLoop(AnimationClip clip, float? blendIn = null)
    {
        BeginForeground();                 // <- NEW
        StartCoroutine(CrossfadeToSlot(clip, blendIn ?? defaultBlendIn, loop:true));
    }

    public void HoldPose(AnimationClip clip, float? blendIn = null)
    {
        BeginForeground();                 // <- NEW
        StartCoroutine(PlayAndHoldRoutine(clip, blendIn ?? defaultBlendIn));
    }

    public void PlayOneShot(AnimationClip clip, float? blendIn = null, float? blendOut = null)
    {
        BeginForeground();                 // <- NEW
        StartCoroutine(PlayOneShotRoutine(clip, blendIn ?? defaultBlendIn, blendOut ?? defaultBlendOut));
    }
    public void Enqueue(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey)) return;
        BeginForeground();                 // <- NEW
        _keyQueue.Enqueue(normalizedKey);
        if (!_queueRunning) StartCoroutine(seamlessQueue ? QueueRunnerSeamless() : QueueRunnerLegacy());
    }


    public void ClearQueue() => _keyQueue.Clear();

    // ---------- Idle cycle ----------
    private void StartIdleCycleIfNeeded()
    {
        if (!playIdleWhenEmpty) return;
        if (_foregroundPlaying) return;   // <- NEW: don’t start while demo/queue is foreground
        if (_idleCycleCo != null) return;
        _idleCycleCo = StartCoroutine(IdleCycleRoutine());
    }


    private void StopIdleCycle()
    {
        if (_idleCycleCo != null)
        {
            StopCoroutine(_idleCycleCo);
            _idleCycleCo = null;
        }

        if (_graphInitialized)
        {
            // If a foreground slot will take over, mute Base; otherwise keep it visible.
            _mixer.SetInputWeight(0, _activeSlot < 0 ? 1f : 0f);
        }
    }

    
    private void BeginForeground()
    {
        _foregroundPlaying = true;
        StopIdleCycle();               // make sure idle can’t interfere
    }

    private void EndForeground()
    {
        _foregroundPlaying = false;
        // only restart idle when *truly* idle again
        if (playIdleWhenEmpty && !_queueRunning && _activeSlot < 0)
            StartIdleCycleIfNeeded();
    }



    private IEnumerator IdleCycleRoutine()
    {
        // If nothing else is active, Base is already showing — no need to crossfade.
        if (_activeSlot >= 0)
            yield return CrossfadeActiveToBase(idleBlendIn);

        var first = PickNextIdleClip();
        if (!first) { _idleCycleCo = null; yield break; }

        // Keep Base at weight 1 if no slot is active
        ReplaceBaseClip(first, 0.0);
        _mixer.SetInputWeight(0, _activeSlot < 0 ? 1f : 0f);
        _graph.Evaluate(0f);
        yield return null;

        // Continue as you had...
        while (playIdleWhenEmpty && !_queueRunning && _activeSlot < 0)
        {
            var next = PickNextIdleClip();
            if (!next) { _idleCycleCo = null; yield break; }

            yield return CrossfadeToSlot(next, Mathf.Max(0.01f, idleCrossfade), loop:false);

            float clipLen = Mathf.Max(0f, next.length / Mathf.Max(0.0001f, playbackSpeed));
            float playWindow = Mathf.Max(0f, clipLen - idleCrossfade);
            yield return WaitSeconds(playWindow);

            var active = GetActivePlayable();
            double curTime = active.IsValid() ? active.GetTime() : 0.0;

            if (idleRebaseAfterCrossfade)
                ReplaceBaseClip(next, curTime);

            yield return CrossfadeActiveToBase(idleCrossfade);

            if (idleCycleGap > 0f)
                yield return WaitSeconds(idleCycleGap);

            if (_queueRunning || _activeSlot >= 0) break;
        }

        _idleCycleCo = null;
    }



    private AnimationClip PickNextIdleClip()
    {
        if (idleClips == null || idleClips.Length == 0) return null;
        var c = idleClips[_idleClipsIndex % idleClips.Length];
        _idleClipsIndex++;
        return c;
    }

    private void ReplaceBaseClip(AnimationClip c, double syncTime)
    {
        if (!c) return;

        bool noActiveSlot = _activeSlot < 0;

        // Swap the Base playable while Base is showing (or muted if a slot is active).
        if (_base.IsValid()) { _mixer.DisconnectInput(0); _base.Destroy(); }

        _base = AnimationClipPlayable.Create(_graph, c);
        _base.SetApplyFootIK(true);
        _base.SetApplyPlayableIK(false);
        _base.SetSpeed(playbackSpeed);
        _base.SetDuration(double.PositiveInfinity);

        double len = Math.Max(0.0001, c.length);
        double t = syncTime % len;
        _base.SetTime(t);

        _graph.Connect(_base, 0, _mixer, 0);

        // IMPORTANT: keep Base visible if no slot is active
        _mixer.SetInputWeight(0, noActiveSlot ? 1f : 0f);

        _graph.Evaluate(0f);
    }


    // ---------- Routines ----------
    private IEnumerator PlayOneShotRoutine(AnimationClip clip, float blendIn, float blendOut)
    {
        if (!clip) yield break;

        // base -> clip -> base
        yield return CrossfadeToSlot(clip, blendIn, loop:false);

        float playLen = Mathf.Max(0f, ((clip.length - endCrop) / playbackSpeed) - blendOut);
        yield return WaitSeconds(playLen);

        // In PlayOneShotRoutine, before CrossfadeActiveToBase(...)
        var active = GetActivePlayable();
        double curTime = active.IsValid() ? active.GetTime() : 0.0;
        EnsureBaseHasIdlePose(curTime);
        yield return CrossfadeActiveToBase(blendOut);

        EndForeground(); // will restart idle only if safe

    }

    private IEnumerator PlayAndHoldRoutine(AnimationClip clip, float blendIn)
    {
        if (!clip) yield break;

        yield return CrossfadeToSlot(clip, blendIn, loop:false);

        // Freeze last frame
        var active = GetActivePlayable();
        if (active.IsValid())
        {
            active.SetSpeed(0f);
            active.SetTime(clip.length);
        }
    }

    // Legacy queue (keeps tiny idle flash between signs)
    private IEnumerator QueueRunnerLegacy()
    {
        _queueRunning = true;

        while (_keyQueue.Count > 0)
        {
            if (!library)
            {
                Debug.LogError("[SignPlaybackController] No SignLibraryLoader assigned.");
                _keyQueue.Clear(); break;
            }

            string key = _keyQueue.Dequeue();
            AnimationClip clip = null; bool done = false;
            library.LoadClip(key, c => { clip = c; done = true; });
            while (!done) yield return null;

            if (clip != null) yield return PlayOneShotRoutine(clip, defaultBlendIn, defaultBlendOut);
        }

        _queueRunning = false;
        if (playIdleWhenEmpty) StartIdleCycleIfNeeded();
    }

    // Seamless queue: direct crossfades 1↔2; only end → base.
    private IEnumerator QueueRunnerSeamless()
    {
        _queueRunning = true;

        if (!library)
        {
            Debug.LogError("[SignPlaybackController] No SignLibraryLoader assigned.");
            _keyQueue.Clear();
            _queueRunning = false;
            yield break;
        }

        if (_keyQueue.Count == 0) { _queueRunning = false; yield break; }

        AnimationClip current = null;
        yield return LoadNextClip(_keyQueue.Dequeue(), c => current = c);

        if (!current)
        {
            _queueRunning = false;
            if (playIdleWhenEmpty) StartIdleCycleIfNeeded();
            yield break;
        }

        // Base -> first
        yield return CrossfadeToSlot(current, defaultBlendIn, loop:false);

        while (true)
        {
            if (_keyQueue.Count == 0) break;

            string nextKey = _keyQueue.Dequeue();
            AnimationClip next = null;
            bool loaded = false;
            StartCoroutine(LoadNextClip(nextKey, c => { next = c; loaded = true; }));

            float overlap = Mathf.Max(0.01f, interSignCrossfade);
            float wait = Mathf.Max(0f, (current.length - endCrop) / playbackSpeed - overlap);
            yield return WaitSeconds(wait);

            if (!loaded)
            {
                float grace = Mathf.Min(0.1f, overlap * 0.5f);
                yield return WaitSeconds(grace);
            }

            if (next)
            {
                yield return CrossfadeActiveToNextSlot(next, overlap);
                current = next;
            }
            else
            {
                yield return CrossfadeActiveToBase(defaultBlendOut);
                _queueRunning = false;
                if (playIdleWhenEmpty) StartIdleCycleIfNeeded();
                yield break;
            }
        }

        // No more items; fade last to base
        yield return WaitSeconds(Mathf.Max(0f, (current.length / playbackSpeed) - defaultBlendOut));
        yield return CrossfadeActiveToBase(defaultBlendOut);

        _queueRunning = false;
        if (playIdleWhenEmpty) StartIdleCycleIfNeeded();
    }

    private IEnumerator LoadNextClip(string key, Action<AnimationClip> done)
    {
        AnimationClip clip = null; bool finished = false;
        library.LoadClip(key, c => { clip = c; finished = true; });
        while (!finished) yield return null;
        done?.Invoke(clip);
    }

    // ---------- Playables helpers (3-input mixer) ----------
    private IEnumerator CrossfadeToSlot(AnimationClip clip, float blendIn, bool loop)
    {
        EnsureGraph();

        int target = (_activeSlot == 1) ? 2 : 1;
        _mixer.SetInputWeight(target, 0.001f);
        _graph.Evaluate(0f);
        _mixer.SetInputWeight(target, 0f);

        var playable = AnimationClipPlayable.Create(_graph, clip);
        playable.SetApplyFootIK(true);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(playbackSpeed);
        playable.SetTime(Mathf.Max(0f, nextStartOffset));
        playable.SetDuration(loop ? double.PositiveInfinity : clip.length);

        if (target == 1)
        {
            if (_slotA.IsValid()) _slotA.Destroy();
            _slotA = playable;
            if (_mixer.GetInput(1).IsValid()) _mixer.DisconnectInput(1);
            _graph.Connect(_slotA, 0, _mixer, 1);
            _mixer.SetInputWeight(1, 0f);
        }
        else
        {
            if (_slotB.IsValid()) _slotB.Destroy();
            _slotB = playable;
            if (_mixer.GetInput(2).IsValid()) _mixer.DisconnectInput(2);
            _graph.Connect(_slotB, 0, _mixer, 2);
            _mixer.SetInputWeight(2, 0f);
        }

        float t = 0f;
        if (_activeSlot < 0)
        {
            while (t < blendIn)
            {
                t += Time.deltaTime;
                float wT = EaseInOut01(t / Mathf.Max(0.0001f, blendIn));
                _mixer.SetInputWeight(0, 1f - wT);
                _mixer.SetInputWeight(target, wT);
                yield return null;
            }
            _mixer.SetInputWeight(0, 0f);
        }
        else
        {
            int from = _activeSlot;
            while (t < blendIn)
            {
                t += Time.deltaTime;
                float wT = EaseInOut01(t / Mathf.Max(0.0001f, blendIn));
                _mixer.SetInputWeight(from, 1f - wT);
                _mixer.SetInputWeight(target, wT);
                yield return null;
            }
            _mixer.SetInputWeight(from, 0f);
        }
        _mixer.SetInputWeight(target, 1f);
        _activeSlot = target;
    }

    private IEnumerator CrossfadeActiveToNextSlot(AnimationClip nextClip, float duration)
    {
        int target = (_activeSlot == 1) ? 2 : 1;
        _mixer.SetInputWeight(target, 0.001f);
        _graph.Evaluate(0f);
        _mixer.SetInputWeight(target, 0f);

        var playable = AnimationClipPlayable.Create(_graph, nextClip);
        playable.SetApplyFootIK(true);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(playbackSpeed);
        playable.SetTime(Mathf.Max(0f, nextStartOffset));
        playable.SetDuration(nextClip.length);

        if (target == 1)
        {
            if (_slotA.IsValid()) _slotA.Destroy();
            _slotA = playable;
            if (_mixer.GetInput(1).IsValid()) _mixer.DisconnectInput(1);
            _graph.Connect(_slotA, 0, _mixer, 1);
            _mixer.SetInputWeight(1, 0f);
        }
        else
        {
            if (_slotB.IsValid()) _slotB.Destroy();
            _slotB = playable;
            if (_mixer.GetInput(2).IsValid()) _mixer.DisconnectInput(2);
            _graph.Connect(_slotB, 0, _mixer, 2);
            _mixer.SetInputWeight(2, 0f);
        }

        float t = 0f;
        int from = _activeSlot;
        while (t < duration)
        {
            t += Time.deltaTime;
            float wT = EaseInOut01(t / Mathf.Max(0.0001f, duration));
            _mixer.SetInputWeight(from, 1f - wT);
            _mixer.SetInputWeight(target, wT);
            yield return null;
        }
        _mixer.SetInputWeight(from, 0f);
        _mixer.SetInputWeight(target, 1f);

        if (from == 1 && _slotA.IsValid()) { _mixer.DisconnectInput(1); _slotA.Destroy(); }
        if (from == 2 && _slotB.IsValid()) { _mixer.DisconnectInput(2); _slotB.Destroy(); }

        _activeSlot = target;
    }

    private IEnumerator CrossfadeActiveToBase(float duration)
    {
        if (_activeSlot < 0) yield break;

        float t = 0f;
        int from = _activeSlot;
        while (t < duration)
        {
            t += Time.deltaTime;
            float w0 = EaseInOut01(t / Mathf.Max(0.0001f, duration));
            _mixer.SetInputWeight(0, w0);
            _mixer.SetInputWeight(from, 1f - w0);
            yield return null;
        }
        _mixer.SetInputWeight(0, 1f);
        _mixer.SetInputWeight(from, 0f);

        if (from == 1 && _slotA.IsValid()) { _mixer.DisconnectInput(1); _slotA.Destroy(); }
        if (from == 2 && _slotB.IsValid()) { _mixer.DisconnectInput(2); _slotB.Destroy(); }
        _activeSlot = -1;
    }

    private AnimationClipPlayable GetActivePlayable()
    {
        if (_activeSlot == 1) return _slotA;
        if (_activeSlot == 2) return _slotB;
        return default;
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; yield return null; }
    }

    private static float EaseInOut01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t); // smoothstep
    }
    
    private void CyclePlaybackSpeed()
    {
        if (speedSteps == null || speedSteps.Length == 0) return;
        _speedIndex = (_speedIndex + 1) % speedSteps.Length;
        SetPlaybackSpeed(speedSteps[_speedIndex]);
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, 0.1f, 2f);

        // Apply to all active playables
        ApplyPlaybackSpeedToActivePlayables();

        UpdateSpeedLabel();
    }

    private void ApplyPlaybackSpeedToActivePlayables()
    {
        if (_base.IsValid())  _base.SetSpeed(playbackSpeed);
        if (_slotA.IsValid()) _slotA.SetSpeed(playbackSpeed);
        if (_slotB.IsValid()) _slotB.SetSpeed(playbackSpeed);
    }

    private void UpdateSpeedLabel()
    {
        if (!speedLabel) return;
        // Show as "0.5x", "1.0x", etc.
        speedLabel.text = playbackSpeed.ToString("0.0") + "x";
    }

    private void SnapSpeedIndexTo(float current)
    {
        if (speedSteps == null || speedSteps.Length == 0) { _speedIndex = 0; return; }
        int closest = 0;
        float best = Mathf.Abs(speedSteps[0] - current);
        for (int i = 1; i < speedSteps.Length; i++)
        {
            float d = Mathf.Abs(speedSteps[i] - current);
            if (d < best) { best = d; closest = i; }
        }
        _speedIndex = closest;
    }
    



}
