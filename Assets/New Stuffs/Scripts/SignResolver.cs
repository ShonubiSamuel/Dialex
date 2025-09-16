using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Looks up a normalized key in a Sign Map and returns either:
/// - a Resources path key (derived from an asset path containing "/Resources/")
/// - an Addressables key (if you choose to store address keys in the map "path" field)
/// - a direct AnimationClip (manual override in inspector)
///
/// Expectation: you provide a JSON list generated from your library, e.g.:
/// {
///   "entries": [ { "key": "ask_out", "path": "Assets/Signs/Ask Out 3.fbx" }, ... ]
/// }
/// Drag that JSON (TextAsset) into the inspector.
///
/// Tips:
/// - If you use Resources workflow, put assets under a Resources folder.
///   Example: "Assets/Resources/Signs/Ask Out 3.fbx"
///   This class will convert that asset path to "Signs/Ask Out 3" for Resources.Load.
/// - If you use Addressables, let "path" in JSON be your address key (or label).
/// </summary>
public class SignResolver : MonoBehaviour
{
    [Header("Sign Map (JSON List)")]
    [Tooltip("JSON produced as a list (JsonUtility-friendly): {\"entries\":[{\"key\":\"ask_out\",\"path\":\"Assets/...\"}]}")]
    public TextAsset signMapListJson;

    [Header("Manual Clip Overrides (optional)")]
    [Tooltip("Keys that should return a direct AnimationClip instead of a path.")]
    public string[] manualKeys;
    public AnimationClip[] manualClips;

    [Header("Path Semantics")]
    [Tooltip("If ON: The 'path' from JSON is treated as an Addressables key.\nIf OFF: We try to derive a Resources key from an asset path containing '/Resources/'.")]
    public bool treatMapPathAsAddressableKey = false;

    // Internal lookup table from JSON list.
    private readonly Dictionary<string, string> _keyToPath = new Dictionary<string, string>();
    // Manual direct-clip overrides.
    private readonly Dictionary<string, AnimationClip> _manual = new Dictionary<string, AnimationClip>();

    [Serializable]
    private class SignEntry { public string key; public string path; }

    [Serializable]
    private class SignEntryList { public List<SignEntry> entries = new List<SignEntry>(); }

    private void Awake()
    {
        BuildManual();
        BuildFromJson();
    }

    private void BuildManual()
    {
        _manual.Clear();
        if (manualKeys == null || manualClips == null) return;

        int n = Mathf.Min(manualKeys.Length, manualClips.Length);
        for (int i = 0; i < n; i++)
        {
            string k = GlossNormalizer.Normalize(manualKeys[i]);
            var clip = manualClips[i];
            if (string.IsNullOrEmpty(k) || clip == null) continue;
            if (_manual.ContainsKey(k)) continue;
            _manual.Add(k, clip);
        }
    }

    private void BuildFromJson()
    {
        _keyToPath.Clear();

        if (signMapListJson == null || string.IsNullOrWhiteSpace(signMapListJson.text))
        {
            Debug.LogWarning($"{nameof(SignResolver)}: No JSON list assigned.");
            return;
        }

        try
        {
            var list = JsonUtility.FromJson<SignEntryList>(signMapListJson.text);
            if (list?.entries == null) return;

            foreach (var e in list.entries)
            {
                if (e == null) continue;
                string key = GlossNormalizer.Normalize(e.key);
                if (string.IsNullOrEmpty(key)) continue;
                if (_keyToPath.ContainsKey(key)) continue; // keep first
                _keyToPath.Add(key, e.path ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"{nameof(SignResolver)}: Failed parsing JSON list. {ex.Message}");
        }
    }

    /// <summary>True if there is a direct manual clip for this key (inspector override).</summary>
    public bool TryGetDirectClip(string normalizedKey, out AnimationClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(normalizedKey)) return false;
        return _manual.TryGetValue(normalizedKey, out clip);
    }

    /// <summary>
    /// If treatMapPathAsAddressableKey = true, returns the path string as an addressable key.
    /// </summary>
    public bool TryGetAddressableKey(string normalizedKey, out string addressKey)
    {
        addressKey = null;
        if (!treatMapPathAsAddressableKey) return false;
        if (!_keyToPath.TryGetValue(normalizedKey, out var p)) return false;
        if (string.IsNullOrWhiteSpace(p)) return false;
        addressKey = p;
        return true;
    }

    /// <summary>
    /// Attempts to derive a Resources.Load key from an 'Assets/.../Resources/.../File.ext' asset path.
    /// Example:
    ///   Asset path: Assets/Resources/Signs/Ask Out 3.fbx
    ///   Returns:    "Signs/Ask Out 3"
    /// </summary>
    public bool TryGetResourcesKey(string normalizedKey, out string resourcesKey)
    {
        resourcesKey = null;
        if (!_keyToPath.TryGetValue(normalizedKey, out var assetPath)) return false;
        if (string.IsNullOrWhiteSpace(assetPath)) return false;

        int idx = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;

        string after = assetPath.Substring(idx + "/Resources/".Length);
        if (after.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            after = after.Substring(0, after.Length - 4);
        if (after.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            after = after.Substring(0, after.Length - 5);

        if (string.IsNullOrWhiteSpace(after)) return false;
        resourcesKey = after;
        return true;
    }

    /// <summary>Return true if the key exists in any form.</summary>
    public bool Contains(string normalizedKey)
    {
        return _manual.ContainsKey(normalizedKey) || _keyToPath.ContainsKey(normalizedKey);
    }
}
