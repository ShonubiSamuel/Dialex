using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified map provider:
/// - Source = Registry: direct key -> AnimationClip (best UX)
/// - Source = JsonList: key -> path (used for Resources or Addressables)
/// You can switch sources without changing higher-level code.
/// </summary>
public class SignMapProvider : MonoBehaviour
{
    public enum SourceType { Registry, JsonList }

    [Header("Source Selection")]
    public SourceType source = SourceType.Registry;

    [Header("Registry Source")]
    public SignRegistry registry;

    [Header("JSON List Source")]
    [Tooltip("JsonUtility-friendly list: {\"entries\":[{\"key\":\"ask_out\",\"path\":\"Assets/...\"}]}")]
    public TextAsset signMapListJson;

    [Tooltip("If ON, treat JSON 'path' as Addressables key; else we derive a Resources key from an asset path containing '/Resources/'.")]
    public bool jsonPathIsAddressableKey = false;

    // For JsonList mode:
    private readonly Dictionary<string, string> _jsonKeyToPath = new Dictionary<string, string>(StringComparer.Ordinal);

    [Serializable]
    private class SignEntry { public string key; public string path; }

    [Serializable]
    private class SignEntryList { public List<SignEntry> entries = new List<SignEntry>(); }

    private void Awake()
    {
        if (source == SourceType.JsonList)
            BuildJsonMap();
    }

    private void OnValidate()
    {
        if (source == SourceType.JsonList)
            BuildJsonMap();
    }

    private void BuildJsonMap()
    {
        _jsonKeyToPath.Clear();

        if (signMapListJson == null || string.IsNullOrWhiteSpace(signMapListJson.text))
        {
            Debug.LogWarning("[SignMapProvider] No JSON assigned for JsonList source.");
            return;
        }

        try
        {
            var list = JsonUtility.FromJson<SignEntryList>(signMapListJson.text);
            if (list?.entries == null) return;

            foreach (var e in list.entries)
            {
                if (e == null) continue;
                var k = GlossNormalizer.Normalize(e.key);
                if (string.IsNullOrEmpty(k)) continue;
                if (!_jsonKeyToPath.ContainsKey(k))
                    _jsonKeyToPath.Add(k, e.path ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignMapProvider] Failed to parse JSON: {ex.Message}");
        }
    }

    /// <summary>True if the key exists in the selected source.</summary>
    public bool Contains(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey)) return false;

        switch (source)
        {
            case SourceType.Registry:
                return registry != null && registry.Contains(normalizedKey);
            case SourceType.JsonList:
                return _jsonKeyToPath.ContainsKey(normalizedKey);
            default:
                return false;
        }
    }

    /// <summary>Registry mode only: direct clip lookup.</summary>
    public bool TryGetDirectClip(string normalizedKey, out AnimationClip clip)
    {
        clip = null;
        if (source != SourceType.Registry || registry == null) return false;
        return registry.TryGetClip(normalizedKey, out clip);
    }

    /// <summary>JSON mode (Addressables): get address key (if configured).</summary>
    public bool TryGetAddressableKey(string normalizedKey, out string addressKey)
    {
        addressKey = null;
        if (source != SourceType.JsonList || !jsonPathIsAddressableKey) return false;
        if (!_jsonKeyToPath.TryGetValue(normalizedKey, out var p)) return false;
        if (string.IsNullOrWhiteSpace(p)) return false;
        addressKey = p;
        return true;
    }

    /// <summary>
    /// JSON mode (Resources): derive Resources.Load key from an asset path like
    /// "Assets/Resources/Signs/Ask Out 3.fbx" -> "Signs/Ask Out 3".
    /// </summary>
    public bool TryGetResourcesKey(string normalizedKey, out string resourcesKey)
    {
        resourcesKey = null;
        if (source != SourceType.JsonList || jsonPathIsAddressableKey) return false;
        if (!_jsonKeyToPath.TryGetValue(normalizedKey, out var assetPath)) return false;
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

    /// <summary>
    /// Convenience: Try to get anything usable for loading/playing in this priority:
    /// 1) Direct clip (Registry)
    /// 2) Addressables key (JSON when jsonPathIsAddressableKey = true)
    /// 3) Resources key (JSON path within Resources)
    /// </summary>
    public bool TryGetBestHandle(string normalizedKey, out AnimationClip clip, out string addressableKey, out string resourcesKey)
    {
        clip = null; addressableKey = null; resourcesKey = null;

        // 1) Registry clip
        if (source == SourceType.Registry && registry != null && registry.TryGetClip(normalizedKey, out clip))
            return true;

        // 2) Addressables key
        if (source == SourceType.JsonList && jsonPathIsAddressableKey && TryGetAddressableKey(normalizedKey, out addressableKey))
            return true;

        // 3) Resources key
        if (source == SourceType.JsonList && !jsonPathIsAddressableKey && TryGetResourcesKey(normalizedKey, out resourcesKey))
            return true;

        return false;
    }
}
