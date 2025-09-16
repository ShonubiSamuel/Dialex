using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SignRegistry", menuName = "Signs/Sign Registry", order = 10)]
public class SignRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;                 // normalized key (e.g., "ask_out")
        public AnimationClip clip;         // direct reference to the clip
    }

    [Tooltip("List of sign entries (key -> AnimationClip). Keys should already be normalized.")]
    public List<Entry> entries = new List<Entry>();

    private Dictionary<string, AnimationClip> _map;

    private void OnEnable()
    {
        BuildMap();
    }

    private void BuildMap()
    {
        _map = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.key) || e.clip == null) continue;
            var key = e.key.Trim();
            if (!_map.ContainsKey(key))
                _map.Add(key, e.clip);
        }
    }

    public bool TryGetClip(string normalizedKey, out AnimationClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(normalizedKey))
            return false;

        if (_map == null) BuildMap();
        return _map != null && _map.TryGetValue(normalizedKey, out clip);
    }

    public bool Contains(string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey)) return false;
        if (_map == null) BuildMap();
        return _map != null && _map.ContainsKey(normalizedKey);
    }

    // public IEnumerable<string> Keys
    // {
    //     get
    //     {
    //         if (_map == null) BuildMap();
    //         return _map?.Keys ?? Array.Empty<string>();
    //     }
    //}
}