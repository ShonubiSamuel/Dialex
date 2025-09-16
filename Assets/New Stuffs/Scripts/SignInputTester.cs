using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Dev console for your sign system.
/// Type a sentence/glosses, press Enter or click Play, and it enqueues them on SignPlaybackController.
/// - Multi-word glosses: wrap in quotes, e.g. "Ask Out"
/// - Pre-normalized: use underscores, e.g. ask_out
/// - Numbers/decimals/letters work (7.2, D 2)
/// </summary>
public class SignInputTester : MonoBehaviour
{
    [Header("References")]
    public SignPlaybackController player;
    public SignResolver resolver; // optional, used for Contains() validation

    [Header("Options")]
    [Tooltip("Clear any queued items before enqueueing a new sequence.")]
    public bool clearBeforePlay = true;

    [Tooltip("Check each key exists via resolver.Contains(key) before enqueueing.")]
    public bool validateWithResolver = true;

    [Tooltip("Log any keys that fail resolver.Contains(key). (Ignored if validateWithResolver is off)")]
    public bool logMissingKeys = true;

    [Tooltip("Normalize input using GlossNormalizer before enqueueing.")]
    public bool normalizeInput = true;

    [Header("UI")]
    public string startText = "Alaska 10 \"Ask Out\" 7.2 D 2";
    public KeyCode playKey = KeyCode.Return;
    public KeyCode clearKey = KeyCode.Backspace;

    // runtime
    private string _input;
    private List<string> _lastKeys = new List<string>();
    private Vector2 _scroll;

    private void Reset()
    {
        TryAutoWire();
    }

    private void OnValidate()
    {
        TryAutoWire();
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(_input)) _input = startText;
        TryAutoWire();
    }

    private void Update()
    {
        if (Input.GetKeyDown(playKey))
        {
            PlayFromInput();
        }
        if (Input.GetKeyDown(clearKey))
        {
            if (player != null) player.ClearQueue();
            Debug.Log("[SignInputTester] Queue cleared.");
        }
    }

    private void OnGUI()
    {
        const int pad = 8;
        int w = Mathf.Min(640, Screen.width - pad * 2);
        int x = pad;
        int y = pad;

        GUILayout.BeginArea(new Rect(x, y, w, 240), GUI.skin.window);
        GUILayout.Label("<b>Sign Input Tester</b>", GetRichLabel());

        GUILayout.Space(3);
        GUILayout.Label("Type glosses/words. Use quotes for phrases (\"Ask Out\"). Press Enter to Play.", GetMiniLabel());

        GUILayout.Space(6);
        _input = GUILayout.TextField(_input, GUILayout.Height(24));

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Play  ↵", GUILayout.Height(26))) PlayFromInput();
        if (GUILayout.Button("Clear ⌫", GUILayout.Height(26)))
        {
            if (player != null) player.ClearQueue();
            Debug.Log("[SignInputTester] Queue cleared.");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        clearBeforePlay     = GUILayout.Toggle(clearBeforePlay,     " Clear before play");
        validateWithResolver= GUILayout.Toggle(validateWithResolver," Validate with resolver");
        normalizeInput      = GUILayout.Toggle(normalizeInput,      " Normalize input");
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Resolved keys (preview):", GetMiniLabel());

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(80));
        if (_lastKeys.Count == 0) GUILayout.Label("<i>(none)</i>", GetMiniLabel());
        else GUILayout.Label(string.Join(", ", _lastKeys), GetMiniLabel());
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private void PlayFromInput()
    {
        if (player == null)
        {
            Debug.LogError("[SignInputTester] No SignPlaybackController assigned.");
            return;
        }

        var keys = ParseInputToKeys(_input, normalizeInput);
        _lastKeys = keys;

        if (keys.Count == 0)
        {
            Debug.LogWarning("[SignInputTester] No keys parsed from input.");
            return;
        }

        if (clearBeforePlay) player.ClearQueue();

        int enq = 0, miss = 0;
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key)) continue;

            if (validateWithResolver && resolver != null)
            {
                if (!resolver.Contains(key))
                {
                    miss++;
                    if (logMissingKeys)
                        Debug.LogWarning($"[SignInputTester] Key not found in map: '{key}'");
                    // still enqueue? Your loader may have a fallback "signs/<key>" convention.
                    // Comment the next 'continue' if you want to try anyway.
                    continue;
                }
            }

            player.Enqueue(key);
            enq++;
        }

        Debug.Log($"[SignInputTester] Enqueued {enq} key(s). Skipped missing: {miss}.");
    }

    // --- parsing ---

    /// <summary>
    /// Parses a free-form line into keys.
    /// Supports:
    ///   - quoted phrases: "ask out"
    ///   - plain tokens: alaska 10 7.2 d 2
    ///   - underscores as pre-normalized phrases: ask_out
    /// </summary>
    public static List<string> ParseInputToKeys(string text, bool normalize)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // Pull out quoted phrases OR single tokens.
        // Pattern: " ... "  OR  non-space+
        var rx = new Regex("\"([^\"]+)\"|(\\S+)", RegexOptions.CultureInvariant);
        var matches = rx.Matches(text);

        foreach (Match m in matches)
        {
            string token = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(token)) continue;

            string key = normalize ? GlossNormalizer.Normalize(token) : token.Trim();
            if (!string.IsNullOrEmpty(key))
                result.Add(key);
        }

        return result;
    }

    // --- helpers ---
    private void TryAutoWire()
    {
        if (player == null) player = FindObjectOfType<SignPlaybackController>();
        if (resolver == null) resolver = FindObjectOfType<SignResolver>();
    }

    private static GUIStyle GetRichLabel()
    {
        var s = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
        return s;
    }
    private static GUIStyle GetMiniLabel()
    {
        var s = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = true };
        return s;
    }
}
