using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SignEntry { public string key; public string path; }
[System.Serializable]
public class SignEntryList { public List<SignEntry> entries; }

public class SignMapLoader : MonoBehaviour
{
    public TextAsset signMapListJson; // drag in Assets/SignMap_List.json

    private Dictionary<string, string> _map;

    void Awake()
    {
        var list = JsonUtility.FromJson<SignEntryList>(signMapListJson.text);
        _map = new Dictionary<string, string>();
        foreach (var e in list.entries)
            if (!_map.ContainsKey(e.key)) _map[e.key] = e.path;
    }

    public bool TryGetClipPath(string word, out string path)
    {
        var key = word.Trim().ToLowerInvariant().Replace(' ', '_');
        return _map.TryGetValue(key, out path);
    }
}